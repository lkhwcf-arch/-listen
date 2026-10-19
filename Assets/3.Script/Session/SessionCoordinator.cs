using System;
using System.Threading;
using System.Threading.Tasks;
using EscapeGame.Session.Contracts;

namespace EscapeGame.Session
{
    internal sealed class SessionCoordinator
    {
        private readonly ISessionGateway gateway;
        private readonly ClientSessionState session;
        private readonly SessionRequestRunner runner;

        internal SessionCoordinator(ISessionGateway gateway, ClientSessionState session, SessionRequestRunner runner)
        {
            this.gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.runner = runner ?? throw new ArgumentNullException(nameof(runner));
        }

        internal Task JoinAsync(CancellationToken token) => runner.RunAsync(async ct =>
        {
            if (session.IsJoined) throw new InvalidOperationException("이미 세션에 입장했습니다.");
            var response = await gateway.JoinAsync(ct);
            ct.ThrowIfCancellationRequested();
            ApplyReceived(() => session.ApplyJoin(response));
        }, token);

        internal Task ReportSceneLoadedAsync(CancellationToken token) => runner.RunAfterCurrentAsync(async ct =>
        {
            EnsureJoined();
            var response = await gateway.ReportSceneLoadedAsync(session.SessionId, session.ParticipantToken, ct);
            ct.ThrowIfCancellationRequested();
            ApplyReceived(() => session.ApplySnapshot(response));
            if (!session.IsSceneLoaded) throw new SessionResultException(SessionFailureCode.InvalidResponse);
        }, token);

        internal Task QueryAsync(CancellationToken token) => runner.RunAsync(async ct =>
        {
            EnsureJoined();
            var response = await gateway.QueryAsync(session.SessionId, session.ParticipantToken, ct);
            ct.ThrowIfCancellationRequested();
            ApplyReceived(() => session.ApplySnapshot(response));
        }, token);

        internal Task LeaveAsync(CancellationToken token)
        {
            EnsureJoined();
            string sessionId = session.SessionId;
            string participantToken = session.ParticipantToken;
            return runner.RunExclusiveAsync(ct => gateway.LeaveAsync(sessionId, participantToken, ct), token);
        }

        internal Task HeartbeatAsync(CancellationToken token) => runner.RunHeartbeatAsync(async ct =>
        {
            EnsureJoined();
            await gateway.HeartbeatAsync(session.SessionId, session.ParticipantToken, ct);
            ct.ThrowIfCancellationRequested();
        }, token);

        internal Task<EscapeGame.Data.CsvDataSet> DownloadDataAsync(CancellationToken token) => DownloadCoreAsync(token);
        private async Task<EscapeGame.Data.CsvDataSet> DownloadCoreAsync(CancellationToken token)
        {
            EscapeGame.Data.CsvDataSet data = null;
            await runner.RunAfterCurrentAsync(async ct =>
            {
                EnsureJoined();
                data = await ((ISessionContentGateway)gateway).DownloadDataAsync(session.SessionId, session.ParticipantToken, ct);
                ct.ThrowIfCancellationRequested();
                // 최초 데이터와 진행 상태를 같은 요청 경로에서 동기화하여 자동 조회와 경합하지 않습니다.
                var snapshot = await gateway.QueryAsync(session.SessionId, session.ParticipantToken, ct);
                ct.ThrowIfCancellationRequested();
                ApplyReceived(() => session.ApplySnapshot(snapshot));
            }, token);
            return data;
        }

        internal async Task<PuzzleSubmissionResult> SubmitPuzzleAsync(int puzzleId, string[] answers, CancellationToken token)
        {
            PuzzleSubmissionResult result = null;
            await runner.RunAfterCurrentAsync(async ct =>
            {
                EnsureJoined();
                var response = await ((ISessionContentGateway)gateway).SubmitPuzzleAsync(session.SessionId, session.ParticipantToken, puzzleId, answers, ct);
                ct.ThrowIfCancellationRequested();
                ApplyReceived(() => session.ApplySnapshot(response.Snapshot));
                result = response.Result;
            }, token);
            return result;
        }

        private void EnsureJoined()
        {
            if (!session.IsJoined) throw new InvalidOperationException("먼저 서버 세션에 입장하세요.");
        }
        private static void ApplyReceived(Action apply)
        {
            try { apply(); }
            catch (ArgumentException exception) { throw new SessionResultException(SessionFailureCode.InvalidResponse, exception); }
        }
    }
}
