using System;
using System.Collections.Generic;
using EscapeGame.Networking;

namespace EscapeGame.Session
{
    public sealed class ClientSessionState
    {
        public string SessionId { get; private set; }
        public string PlayerId { get; private set; }
        public string Role { get; private set; }
        public string ParticipantToken { get; private set; }
        public bool IsJoined => !string.IsNullOrEmpty(SessionId);
        public ClientSessionPhase? Phase { get; private set; }
        public bool IsSceneLoaded { get; private set; }
        public int ParticipantCount { get; private set; }
        public event Action SnapshotApplied;
        public event Action Joined;
        public void ApplyJoin(JoinSessionResponse response)
        {
            if (IsJoined)
                throw new InvalidOperationException("이미 세션에 입장했습니다.");

            if (response == null || !response.success ||
            string.IsNullOrWhiteSpace(response.sessionId) ||
            response.player == null ||
            string.IsNullOrWhiteSpace(response.player.id) ||
            string.IsNullOrWhiteSpace(response.participantToken) ||
            (response.player.role != "A" && response.player.role != "B"))
            {
                throw new ArgumentException("유요하지 않은 입장응답 입니다. ");
            }

            SessionId = response.sessionId;
            PlayerId = response.player.id;
            Role = response.player.role;
            ParticipantToken = response.participantToken;

            Joined?.Invoke();
        }
        public void ApplySnapshot(SessionSnapshotDto snapshot)
        {
            if (!IsJoined)
                throw new InvalidOperationException("먼저 세션에 입장해야 합니다.");

            if (snapshot == null ||
                snapshot.sessionId != SessionId ||
                snapshot.players == null ||
                snapshot.players.Length < 1 ||
                snapshot.players.Length > 2)
            {
                throw new ArgumentException("유효하지 않은 세션 스냅샷입니다.");
            }

            ClientSessionPhase nextPhase = SessionPhaseParser.Parse(snapshot.phase);

            var playerIds = new HashSet<string>();
            var roles = new HashSet<string>();

            PlayerDto localPlayer = null;
            bool allLoaded = true;

            foreach (var player in snapshot.players)
            {
                if (player == null ||
                    string.IsNullOrWhiteSpace(player.id) ||
                    (player.role != "A" && player.role != "B") ||
                    !playerIds.Add(player.id) ||
                    !roles.Add(player.role))
                {
                    throw new ArgumentException("유효하지 않은 참가자 목록입니다.");
                }

                allLoaded &= player.ready;

                if (player.id == PlayerId)
                    localPlayer = player;
            }

            if (localPlayer == null || localPlayer.role != Role)
                throw new ArgumentException("본인의 참가 정보가 일치하지 않습니다.");

            // 현재 서버의 두 명 정원 규칙과 응답이 일치하는지 확인합니다.
            ClientSessionPhase expectedPhase =
                snapshot.players.Length < 2
                    ? ClientSessionPhase.Waiting
                    : allLoaded
                        ? ClientSessionPhase.Playing
                        : ClientSessionPhase.Loading;

            if (nextPhase != expectedPhase)
                throw new ArgumentException("참가 상태와 세션 단계가 일치하지 않습니다.");

            Phase = nextPhase;
            IsSceneLoaded = localPlayer.ready;
            ParticipantCount = snapshot.players.Length;

            SnapshotApplied?.Invoke();
        }
    }
}

