using System;
using System.Threading;
using System.Threading.Tasks;
using EscapeGame.Session;
using EscapeGame.Session.Contracts;

namespace EscapeGame.Networking
{
    internal sealed class HttpSessionGatewayFactory : ISessionGatewayFactory
    {
        public ISessionGateway Create(string serverAddress) => new HttpSessionGateway(new SessionApiClient(serverAddress));
    }

    // Wire DTO와 HTTP 오류를 세션 처리용 데이터/오류로 변환하는 어댑터입니다.
    internal sealed class HttpSessionGateway : ISessionGateway
    {
        private readonly SessionApiClient api;
        internal HttpSessionGateway(SessionApiClient api) => this.api = api ?? throw new ArgumentNullException(nameof(api));

        public Task<SessionJoinData> JoinAsync(CancellationToken token) => ExecuteAsync(async () =>
        {
            var response = await api.JoinAsync(token);
            EnsureSuccess(response.success, response.code);
            return new SessionJoinData(response.sessionId, MapPlayer(response.player), response.participantToken);
        });

        public Task<SessionSnapshotData> ReportSceneLoadedAsync(string sessionId, string participantToken, CancellationToken token) => ExecuteAsync(async () =>
        {
            var response = await api.ReportSceneLoadedAsync(sessionId, participantToken, token);
            EnsureSuccess(response.success, response.code);
            return MapSnapshot(response.snapshot);
        });

        public Task<SessionSnapshotData> QueryAsync(string sessionId, string participantToken, CancellationToken token) => ExecuteAsync(async () =>
        {
            var response = await api.QuerySessionAsync(sessionId, participantToken, token);
            EnsureSuccess(response.success, response.code);
            return MapSnapshot(response.snapshot);
        });

        private static async Task<T> ExecuteAsync<T>(Func<Task<T>> operation)
        {
            try { return await operation(); }
            catch (OperationCanceledException) { throw; }
            catch (SessionResultException) { throw; }
            catch (SessionTransportException exception)
            { throw new SessionResultException(SessionFailureCode.ConnectionFailed, exception); }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            { throw new SessionResultException(SessionFailureCode.InvalidResponse, exception); }
        }

        private static void EnsureSuccess(bool success, string code)
        {
            if (success) return;
            var failure = code switch
            {
                SessionResultCodes.SessionFull => SessionFailureCode.SessionFull,
                SessionResultCodes.SessionNotFound => SessionFailureCode.SessionNotFound,
                SessionResultCodes.InvalidParticipant => SessionFailureCode.InvalidParticipant,
                SessionResultCodes.ParticipantTokenRequired => SessionFailureCode.ParticipantTokenRequired,
                _ => SessionFailureCode.InvalidResponse
            };
            throw new SessionResultException(failure);
        }

        private static SessionPlayerData MapPlayer(PlayerDto player) => player == null ? null :
            new SessionPlayerData(player.id, player.role, player.ready);

        private static SessionSnapshotData MapSnapshot(SessionSnapshotDto snapshot)
        {
            if (snapshot == null || snapshot.players == null)
                throw new SessionResultException(SessionFailureCode.InvalidResponse);
            var players = new SessionPlayerData[snapshot.players.Length];
            for (int i = 0; i < players.Length; i++) players[i] = MapPlayer(snapshot.players[i]);
            return new SessionSnapshotData(snapshot.sessionId, SessionPhaseParser.Parse(snapshot.phase), players);
        }
    }
}
