using System;
using System.Threading;
using System.Threading.Tasks;
using EscapeGame.Session;
using EscapeGame.Session.Contracts;

namespace EscapeGame.Networking
{
    // Wire DTO와 HTTP 오류를 세션 처리용 데이터/오류로 변환하는 어댑터입니다.
    internal sealed class HttpSessionGateway : ISessionGateway, ISessionContentGateway, ISessionRoomGateway
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

        public async Task LeaveAsync(string sessionId, string participantToken, CancellationToken token)
        {
            await ExecuteAsync(async () =>
            {
                var response = await api.LeaveAsync(sessionId, participantToken, token);
                EnsureSuccess(response.success, response.code);
                return true;
            });
        }

        public async Task HeartbeatAsync(string sessionId, string participantToken, CancellationToken token)
        {
            await ExecuteAsync(async () =>
            {
                var response = await api.HeartbeatAsync(sessionId, participantToken, token);
                EnsureSuccess(response.success, response.code);
                return true;
            });
        }

        public Task<EscapeGame.Data.CsvDataSet> DownloadDataAsync(string sessionId, string participantToken, CancellationToken token) => ExecuteAsync(async () =>
        {
            var response = await api.DownloadDataAsync(sessionId, participantToken, token);
            EnsureSuccess(response.success, response.code);
            return response.Read();
        });

        public Task<SessionPuzzleSubmissionData> SubmitPuzzleAsync(string sessionId, string participantToken, int puzzleId, string[] answers, CancellationToken token) => ExecuteAsync(async () =>
        {
            var response = await api.SubmitPuzzleAsync(sessionId, participantToken, puzzleId, answers, token);
            EnsureSuccess(response.success, response.code);
            return new SessionPuzzleSubmissionData(MapSnapshot(response.snapshot),
                PuzzleSubmissionResult.FromDecision(response.accepted, response.correct));
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

        public Task<SessionRoomDecisionData> DecideRoomAsync(string sessionId, string participantToken, int roomNo, string[] answers, CancellationToken token) => ExecuteAsync(async () =>
        {
            var result = await api.DecideRoomAsync(sessionId, participantToken, roomNo, answers, token);
            return new SessionRoomDecisionData(MapSnapshot(SessionApiClient.MapSnapshot(result.Snapshot)),
                new RoomDecision(result.RoomNo, result.Value, result.DoorAllowed));
        });

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
            return new SessionSnapshotData(snapshot.sessionId, SessionPhaseParser.Parse(snapshot.phase), players, snapshot.unlockedRoomIds);
        }
    }
}
