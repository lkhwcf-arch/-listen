using System;
using System.Threading;
using System.Threading.Tasks;
using EscapeGame.Session.Contracts;

namespace EscapeGame.Session
{
    // 외부는 공개 계약으로만 사용합니다. 내부 상태, 인증, 통신 포트를 이곳에 모읍니다.
    internal sealed class SessionClient : ISessionConnectionService, IDisposable
    {
        private readonly ISessionGatewayFactory gatewayFactory;
        private readonly ClientSessionState state = new ClientSessionState();
        private readonly SessionRequestRunner runner = new SessionRequestRunner();
        private readonly CancellationTokenSource lifetime;
        private readonly Action<Exception> diagnostic;
        private SessionCoordinator coordinator;
        private SessionFailureCode lastBackgroundFailure;
        private bool disposed;

        public SessionInfo State { get; private set; }
        public bool IsBusy => runner.IsBusy;
        public string ServerBaseUrl { get; private set; }
        public event Action<SessionInfo> StateChanged;
        public event Action<ClientSessionPhase?, ClientSessionPhase> PhaseChanged;
        public event Action<SessionFailureCode> BackgroundFailed;

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
            if (state.IsJoined) throw new InvalidOperationException("이미 입장했습니다.");
            if (runner.IsBusy) throw new SessionResultException(SessionFailureCode.Busy);
            var normalized = address?.Trim();
            var gateway = gatewayFactory.Create(normalized);
            coordinator = new SessionCoordinator(gateway, state, runner);
            await coordinator.JoinAsync(ct);
            ServerBaseUrl = normalized.TrimEnd('/');
        }, token, false);

        public Task<SessionCommandResult> ReportSceneLoadedAsync(CancellationToken token) => ExecuteAsync(
            ct => RequireCoordinator().ReportSceneLoadedAsync(ct), token, true);

        public Task<SessionCommandResult> QueryAsync(CancellationToken token) => ExecuteAsync(
            ct => RequireCoordinator().QueryAsync(ct), token, true);

        private SessionCoordinator RequireCoordinator()
        {
            if (coordinator == null || !state.IsJoined)
                throw new InvalidOperationException("먼저 서버 세션에 입장하세요.");
            return coordinator;
        }

        private async Task<SessionCommandResult> ExecuteAsync(Func<CancellationToken, Task> command,
            CancellationToken token, bool background)
        {
            if (disposed) return SessionCommandResult.Cancelled();
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.Token);
            Exception detail = null;
            var result = await SessionCommandExecutor.ExecuteAsync(command, linked.Token, error => detail = error);
            if (result.Outcome == SessionCommandOutcome.Failed && result.Code != SessionFailureCode.Busy)
            {
                if (!background || lastBackgroundFailure != result.Code)
                {
                    if (detail != null) diagnostic?.Invoke(detail);
                    if (background) BackgroundFailed?.Invoke(result.Code);
                }
                if (background) lastBackgroundFailure = result.Code;
            }
            else if (background && result.Outcome == SessionCommandOutcome.Completed)
                lastBackgroundFailure = SessionFailureCode.None;
            return result;
        }

        private SessionInfo CaptureState() => new SessionInfo(state.SessionId, state.PlayerId, state.Role,
            state.Phase, state.ParticipantCount, state.IsSceneLoaded);

        private void PublishState()
        {
            State = CaptureState();
            StateChanged?.Invoke(State);
        }

        private void PublishPhase(ClientSessionPhase? previous, ClientSessionPhase current) =>
            PhaseChanged?.Invoke(previous, current);

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            lifetime.Cancel();
            state.Joined -= PublishState;
            state.SnapshotApplied -= PublishState;
            state.PhaseChanged -= PublishPhase;
            lifetime.Dispose();
        }
    }
}
