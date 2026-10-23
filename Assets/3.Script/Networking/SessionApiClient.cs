using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EscapeGame.Data;
using EscapeGame.Data.Csv;
using EscapeGame.Protocol.V1;
using EscapeGame.Session;
using EscapeGame.Session.Contracts;

namespace EscapeGame.Networking
{
    // Protobuf에서 세션 코어 모델로 단 한 번 변환합니다. 기존 JSON DTO는 사용하지 않습니다.
    internal sealed class SessionApiClient
    {
        private readonly IGameApi api;
        internal SessionApiClient(IGameApi api) => this.api = api ?? throw new ArgumentNullException(nameof(api));
        public async Task<SessionJoinData> JoinAsync(CancellationToken token)
        {
            var result = (await api.JoinSessionAsync(token)).Joined ?? throw Invalid();
            return new SessionJoinData(result.SessionId, MapPlayer(result.Player), result.ParticipantToken);
        }
        public async Task<SessionSnapshotData> ReportSceneLoadedAsync(string sessionId, string participantToken, CancellationToken token)
        {
            ValidateParticipant(sessionId, participantToken);
            return MapSnapshot((await api.ReportSceneLoadedAsync(sessionId, participantToken, token)).Snapshot);
        }
        public async Task<SessionSnapshotData> QuerySessionAsync(string sessionId, string participantToken, CancellationToken token)
        {
            ValidateParticipant(sessionId, participantToken);
            return MapSnapshot((await api.QuerySessionAsync(sessionId, participantToken, token)).Snapshot);
        }
        public async Task HeartbeatAsync(string sessionId, string participantToken, CancellationToken token)
        {
            ValidateParticipant(sessionId, participantToken);
            await api.HeartbeatAsync(sessionId, participantToken, token);
        }
        public async Task LeaveAsync(string sessionId, string participantToken, CancellationToken token)
        {
            ValidateParticipant(sessionId, participantToken);
            await api.LeaveSessionAsync(sessionId, participantToken, token);
        }
        public async Task<CsvDataSet> DownloadDataAsync(string sessionId, string participantToken, CancellationToken token)
        {
            ValidateParticipant(sessionId, participantToken);
            var result = (await api.DownloadDataAsync(sessionId, participantToken, token)).Data ?? throw Invalid();
            if (string.IsNullOrWhiteSpace(result.Version) || result.Tables.Count == 0)
                throw Invalid();
            var parser = new CsvParser();
            var documents = new System.Collections.Generic.Dictionary<string, CsvDocument>(StringComparer.Ordinal);
            foreach (var table in result.Tables)
            {
                if (string.IsNullOrWhiteSpace(table.Name) || documents.ContainsKey(table.Name))
                    throw Invalid();
                documents.Add(table.Name, parser.Read(table.Csv, table.Name));
            }
            return new CsvDataSet(documents);
        }
        public async Task<SessionPuzzleSubmissionData> SubmitPuzzleAsync(string sessionId, string participantToken, int puzzleId, string[] answers, CancellationToken token)
        {
            ValidateParticipant(sessionId, participantToken);
            ValidateAnswers(puzzleId, answers);
            var command = new PuzzleCommandRequest();
            command.Answers.Add(answers);
            var result = (await api.SubmitPuzzleAsync(sessionId, puzzleId, participantToken, command, token)).Puzzle ?? throw Invalid();
            return new SessionPuzzleSubmissionData(MapSnapshot(result.Snapshot), PuzzleSubmissionResult.FromDecision(result.Accepted, result.Correct));
        }
        public async Task<SessionRoomDecisionData> DecideRoomAsync(string sessionId, string participantToken, int roomNo, string[] answers, CancellationToken token)
        {
            ValidateParticipant(sessionId, participantToken);
            if (roomNo < 1 || (answers != null && answers.Any(answer => answer == null)))
                throw new ArgumentException("방 입력이 유효하지 않습니다.");
            var command = new RoomCommandRequest { RoomNo = roomNo };
            if (answers != null)
                command.Answers.Add(answers);
            var reply = answers == null ? await api.CheckRoomDoorAsync(sessionId, participantToken, command, token)
                : await api.SubmitRoomAnswerAsync(sessionId, participantToken, command, token);
            var result = reply.Room;
            if (result == null || result.RoomNo != roomNo || (answers == null && result.Value != result.DoorAllowed))
                throw Invalid();
            return new SessionRoomDecisionData(MapSnapshot(result.Snapshot), new RoomDecision(result.RoomNo, result.Value, result.DoorAllowed));
        }
        private static void ValidateParticipant(string id, string token)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(token))
                throw new ArgumentException("세션과 참가 토큰이 필요합니다.");
        }
        private static void ValidateAnswers(int id, string[] answers)
        {
            if (id < 1 || answers == null || answers.Any(answer => answer == null))
                throw new ArgumentException("퍼즐 입력이 유효하지 않습니다.");
        }
        private static SessionResultException Invalid() => new SessionResultException(SessionFailureCode.InvalidResponse);
        private static SessionPlayerData MapPlayer(PlayerSnapshot player)
        {
            if (player == null)
                throw Invalid();
            return new SessionPlayerData(player.PlayerId, player.Role switch
            {
                PlayerRole.RoleA => "A",
                PlayerRole.RoleB => "B",
                _ => throw Invalid()
            }, player.SceneLoaded);
        }
        internal static SessionSnapshotData MapSnapshot(SessionSnapshot snapshot)
        {
            if (snapshot == null)
                throw Invalid();
            var phase = snapshot.Phase switch
            {
                SessionPhase.Waiting => ClientSessionPhase.Waiting,
                SessionPhase.Loading => ClientSessionPhase.Loading,
                SessionPhase.Playing => ClientSessionPhase.Playing,
                _ => throw Invalid()
            };
            return new SessionSnapshotData(snapshot.SessionId, phase, snapshot.Players.Select(MapPlayer).ToArray(), snapshot.UnlockedRoomIds.ToArray());
        }
    }
}
