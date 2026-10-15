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
