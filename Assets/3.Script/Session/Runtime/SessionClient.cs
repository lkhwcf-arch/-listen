using System;
using System.Threading;
using System.Threading.Tasks;
using EscapeGame.Session.Contracts;

namespace EscapeGame.Session
{
    internal sealed class SessionClient : ISessionConnectionService, ISessionContentService, IDisposable
    {
        private readonly ISessionGatewayFactory gatewayFactory;
        private readonly ClientSessionState state = new ClientSessionState();
        private readonly SessionRequestRunner runner = new SessionRequestRunner();
        private readonly CancellationTokenSource lifetime;
        private readonly Action<Exception> diagnostic;
        private CancellationTokenSource connectionLifetime;
        private SessionCoordinator coordinator;
        private SessionFailureCode lastBackgroundFailure;
        private SessionFailureCode disconnectReason;
        private bool disconnected;
        private bool leaving;
        private bool disposed;
        private int connectionVersion;

        public SessionInfo State { get; private set; }
        public bool IsBusy => runner.IsBusy || runner.IsHeartbeatRunning;
        public string ServerBaseUrl { get; private set; }
        public event Action<System.Collections.Generic.IReadOnlyList<int>> RoomsChanged
        { add { state.RoomsChanged += value; } remove { state.RoomsChanged -= value; } }
        public async Task<EscapeGame.Data.CsvDataSet> DownloadDataAsync(CancellationToken token)
        {
            EscapeGame.Data.CsvDataSet data = null;
            var result = await ExecuteAsync(async ct => data = await RequireCoordinator().DownloadDataAsync(ct), token, true);
            if (result.Outcome == SessionCommandOutcome.Cancelled) throw new OperationCanceledException(token);
            if (result.Outcome != SessionCommandOutcome.Completed) throw new SessionResultException(result.Code);
            return data;
        }
        public async Task<bool> SubmitPuzzleAsync(int puzzleId, string[] answers, CancellationToken token)
        {
            bool correct = false;
            var result = await ExecuteAsync(async ct => correct = await RequireCoordinator().SubmitPuzzleAsync(puzzleId, answers, ct), token, true);
            if (result.Outcome == SessionCommandOutcome.Cancelled) throw new OperationCanceledException(token);
            if (result.Outcome != SessionCommandOutcome.Completed) throw new SessionResultException(result.Code);
            return correct;
        }

        public event Action<SessionInfo> StateChanged;
        public event Action<ClientSessionPhase?, ClientSessionPhase> PhaseChanged;
        public event Action<SessionFailureCode> BackgroundFailed;
        public event Action<SessionFailureCode> Disconnected;

        internal SessionClient(ISessionGatewayFactory gatewayFactory, CancellationToken ownerLifetime,
            Action<Exception> diagnostic = null)
        {
            this.gatewayFactory = gatewayFactory ?? throw new ArgumentNullException(nameof(gatewayFactory));
            this.diagnostic = diagnostic;
            lifetime = CancellationTokenSource.CreateLinkedTokenSource(ownerLifetime);
            State = CaptureState();
            state.Joined += PublishState;
            state.SnapshotApplied += PublishState;
            state.PhaseChanged += PublishPhase;
        }

        public Task<SessionCommandResult> JoinAsync(string address, CancellationToken token) => ExecuteAsync(async ct =>
        {
            if (state.IsJoined || leaving) throw new InvalidOperationException("이미 입장했습니다.");
            if (IsBusy) throw new SessionResultException(SessionFailureCode.Busy);
            var normalized = address?.Trim();
            var gateway = gatewayFactory.Create(normalized);
            connectionLifetime?.Dispose();
            connectionLifetime = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
            connectionVersion++;
            disconnected = false;
            disconnectReason = lastBackgroundFailure = SessionFailureCode.None;
            PublishState();
            coordinator = new SessionCoordinator(gateway, state, runner);
            await coordinator.JoinAsync(ct);
            ServerBaseUrl = normalized.TrimEnd('/');
        }, token, false);

        public Task<SessionCommandResult> ReportSceneLoadedAsync(CancellationToken token) => ExecuteAsync(
            ct => RequireCoordinator().ReportSceneLoadedAsync(ct), token, true);
        public Task<SessionCommandResult> QueryAsync(CancellationToken token) => ExecuteAsync(
            ct => RequireCoordinator().QueryAsync(ct), token, true);
        public Task<SessionCommandResult> HeartbeatAsync(CancellationToken token) => ExecuteAsync(
            ct => RequireCoordinator().HeartbeatAsync(ct), token, true);

        public async Task<SessionCommandResult> LeaveAsync(CancellationToken token)
        {
            if (disposed) return SessionCommandResult.Cancelled();
            if (leaving) return SessionCommandResult.Failed(SessionFailureCode.Busy);
            if (!state.IsJoined) return SessionCommandResult.Failed(SessionFailureCode.InvalidState);
            var current = coordinator;
            leaving = true;
            connectionVersion++;
            PublishState();
            connectionLifetime?.Cancel();
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.Token);
            var result = await SessionCommandExecutor.ExecuteAsync(current.LeaveAsync, linked.Token, diagnostic);
            // 응답을 못 받아도 로컬은 종료하고 서버 만료가 남은 자리를 정리합니다.
            state.Clear();
            coordinator = null;
            leaving = disconnected = false;
            disconnectReason = lastBackgroundFailure = SessionFailureCode.None;
            if (!disposed) PublishState();
            if (result.Code == SessionFailureCode.InvalidParticipant || result.Code == SessionFailureCode.SessionNotFound)
                return SessionCommandResult.Completed();
            if (result.Outcome == SessionCommandOutcome.Failed)
                return SessionCommandResult.Failed(SessionFailureCode.LeaveUnconfirmed);
            return result;
        }

        public void Disconnect(SessionFailureCode reason)
        {
            if (reason == SessionFailureCode.None || !Enum.IsDefined(typeof(SessionFailureCode), reason))
                throw new ArgumentOutOfRangeException(nameof(reason));
            if (disposed || disconnected || leaving || !state.IsJoined) return;
            disconnected = true;
            disconnectReason = reason;
            connectionVersion++;
            state.Clear();
            coordinator = null;
            PublishState();
            connectionLifetime?.Cancel();
            Disconnected?.Invoke(reason);
        }

        private SessionCoordinator RequireCoordinator()
        {
            if (coordinator == null || !state.IsJoined)
                throw new InvalidOperationException("먼저 서버 세션에 입장하세요.");
            return coordinator;
        }

        private async Task<SessionCommandResult> ExecuteAsync(Func<CancellationToken, Task> command,
            CancellationToken token, bool authenticated)
        {
            if (disposed) return SessionCommandResult.Cancelled();
            int version = connectionVersion;
            using var linked = authenticated && connectionLifetime != null
                ? CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.Token, connectionLifetime.Token)
                : CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.Token);
            Exception detail = null;
            var result = await SessionCommandExecutor.ExecuteAsync(command, linked.Token, error => detail = error);
            // 이전 연결의 늦은 결과는 현재 상태나 알림을 덮어쓰지 않습니다.
            if (authenticated && version != connectionVersion)
            {
                if (!disposed && disconnected) PublishState();
                return SessionCommandResult.Cancelled();
            }
            if (result.Outcome == SessionCommandOutcome.Failed && result.Code != SessionFailureCode.Busy)
            {
                if (!authenticated || lastBackgroundFailure != result.Code)
                {
                    if (detail != null) diagnostic?.Invoke(detail);
                    if (authenticated && IsTerminalParticipantFailure(result.Code)) Disconnect(result.Code);
                    else if (authenticated) BackgroundFailed?.Invoke(result.Code);
                }
                if (authenticated) lastBackgroundFailure = result.Code;
            }
            else if (authenticated && result.Outcome == SessionCommandOutcome.Completed)
                lastBackgroundFailure = SessionFailureCode.None;
            if (!disposed && disconnected) PublishState();
            return result;
        }

        private static bool IsTerminalParticipantFailure(SessionFailureCode code) =>
            code == SessionFailureCode.InvalidParticipant || code == SessionFailureCode.SessionNotFound ||
            code == SessionFailureCode.ParticipantTokenRequired;

        private SessionInfo CaptureState() => new SessionInfo(state.SessionId, state.PlayerId, state.Role,
            state.Phase, state.ParticipantCount, state.IsSceneLoaded,
            leaving ? SessionConnectionStatus.Leaving : disconnected ? SessionConnectionStatus.Disconnected : state.IsJoined
                ? SessionConnectionStatus.Connected : SessionConnectionStatus.NotConnected,
            disconnectReason);

        private void PublishState() { State = CaptureState(); StateChanged?.Invoke(State); }
        private void PublishPhase(ClientSessionPhase? previous, ClientSessionPhase current) => PhaseChanged?.Invoke(previous, current);

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            lifetime.Cancel();
            connectionLifetime?.Dispose();
            state.Joined -= PublishState;
            state.SnapshotApplied -= PublishState;
            state.PhaseChanged -= PublishPhase;
            lifetime.Dispose();
        }
    }
}
