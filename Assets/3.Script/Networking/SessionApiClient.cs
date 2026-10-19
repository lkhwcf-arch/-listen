using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EscapeGame.Protocol.V1;
using EscapeGame.Session;
using EscapeGame.Session.Contracts;

namespace EscapeGame.Networking
{
    // Protobuf wire types are adapted to existing internal session values here.
    internal sealed class SessionApiClient
    {
        private readonly IGameApi api;
        internal SessionApiClient(IGameApi api) { this.api = api ?? throw new ArgumentNullException(nameof(api)); }

        public async Task<JoinSessionResponseDto> JoinAsync(CancellationToken token)
        {
            var reply = (await api.JoinSessionAsync(token)).Joined;
            if (reply == null) throw Invalid();
            return new JoinSessionResponseDto { success = true, sessionId = reply.SessionId,
                participantToken = reply.ParticipantToken, player = MapPlayer(reply.Player) };
        }
        public async Task<SceneLoadedResponseDto> ReportSceneLoadedAsync(string sessionId, string participantToken, CancellationToken token)
        {
            ValidateParticipant(sessionId, participantToken);
            return new SceneLoadedResponseDto { success = true,
                snapshot = MapSnapshot((await api.ReportSceneLoadedAsync(sessionId, participantToken, token)).Snapshot) };
        }
        public async Task<QuerySessionResponseDto> QuerySessionAsync(string sessionId, string participantToken, CancellationToken token)
        {
            ValidateParticipant(sessionId, participantToken);
            return new QuerySessionResponseDto { success = true,
                snapshot = MapSnapshot((await api.QuerySessionAsync(sessionId, participantToken, token)).Snapshot) };
        }
        public async Task<HeartbeatResponseDto> HeartbeatAsync(string sessionId, string participantToken, CancellationToken token)
        {
            ValidateParticipant(sessionId, participantToken);
            await api.HeartbeatAsync(sessionId, participantToken, token);
            return new HeartbeatResponseDto { success = true };
        }
        public async Task<LeaveResponseDto> LeaveAsync(string sessionId, string participantToken, CancellationToken token)
        {
            ValidateParticipant(sessionId, participantToken);
            await api.LeaveSessionAsync(sessionId, participantToken, token);
            return new LeaveResponseDto { success = true };
        }
        public async Task<CsvBundleDto> DownloadDataAsync(string sessionId, string participantToken, CancellationToken token)
        {
            ValidateParticipant(sessionId, participantToken);
            var data = (await api.DownloadDataAsync(sessionId, participantToken, token)).Data;
            if (data == null) throw Invalid();
            return new CsvBundleDto { success = true, version = data.Version,
                tables = data.Tables.Select(table => new CsvTextDto { name = table.Name, csv = table.Csv }).ToArray() };
        }
        public async Task<PuzzleSubmissionResponseDto> SubmitPuzzleAsync(string sessionId, string participantToken, int puzzleId, string[] answers, CancellationToken token)
        {
            ValidateParticipant(sessionId, participantToken);
            if (puzzleId < 1 || answers == null || answers.Any(answer => answer == null)) throw new ArgumentException("퍼즐 입력이 유효하지 않습니다.");
            var command = new PuzzleCommandRequest(); command.Answers.Add(answers);
            var result = (await api.SubmitPuzzleAsync(sessionId, puzzleId, participantToken, command, token)).Puzzle;
            if (result == null) throw Invalid();
            PuzzleSubmissionResult.FromDecision(result.Accepted, result.Correct);
            return new PuzzleSubmissionResponseDto { success = true, accepted = result.Accepted,
                correct = result.Correct, snapshot = MapSnapshot(result.Snapshot) };
        }
        public async Task<RoomDecisionReply> DecideRoomAsync(string sessionId, string participantToken, int roomNo, string[] answers, CancellationToken token)
        {
            ValidateParticipant(sessionId, participantToken);
            if (roomNo < 1 || (answers != null && answers.Any(answer => answer == null))) throw new ArgumentException("방 입력이 유효하지 않습니다.");
            var command = new RoomCommandRequest { RoomNo = roomNo };
            if (answers != null) command.Answers.Add(answers);
            var reply = answers == null ? await api.CheckRoomDoorAsync(sessionId, participantToken, command, token)
                : await api.SubmitRoomAnswerAsync(sessionId, participantToken, command, token);
            var result = reply.Room;
            if (result == null || result.RoomNo != roomNo || (answers == null && result.Value != result.DoorAllowed)) throw Invalid();
            return result;
        }
        private static void ValidateParticipant(string sessionId, string token)
        { if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(token)) throw new ArgumentException("세션과 참가 토큰이 필요합니다."); }
        private static SessionResultException Invalid() => new SessionResultException(SessionFailureCode.InvalidResponse);
        private static PlayerDto MapPlayer(PlayerSnapshot player)
        {
            if (player == null) throw Invalid();
            return new PlayerDto { id = player.PlayerId, ready = player.SceneLoaded, role = player.Role switch
                { PlayerRole.RoleA => "A", PlayerRole.RoleB => "B", _ => throw Invalid() } };
        }
        internal static SessionSnapshotDto MapSnapshot(SessionSnapshot snapshot)
        {
            if (snapshot == null) throw Invalid();
            return new SessionSnapshotDto { sessionId = snapshot.SessionId,
                phase = snapshot.Phase switch { SessionPhase.Waiting => "Waiting", SessionPhase.Loading => "Loading", SessionPhase.Playing => "Playing", _ => throw Invalid() },
                players = snapshot.Players.Select(MapPlayer).ToArray(), unlockedRoomIds = snapshot.UnlockedRoomIds.ToArray() };
        }
    }
}
