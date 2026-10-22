using System;
using System.Linq;
using System.Collections.Generic;
using EscapeGame.Session.Contracts;

namespace EscapeGame.Session
{
    internal sealed class ClientSessionState
    {
        public string SessionId { get; private set; }
        public string PlayerId { get; private set; }
        public string Role { get; private set; }
        public string ParticipantToken { get; private set; }
        public bool IsJoined => !string.IsNullOrEmpty(SessionId);
        public ClientSessionPhase? Phase { get; private set; }
        public bool IsSceneLoaded { get; private set; }
        public int ParticipantCount { get; private set; }
        private int[] lastUnlockedRooms;
        public event Action<System.Collections.Generic.IReadOnlyList<int>> RoomsChanged;
        public event Action SnapshotApplied;
        public event Action Joined;
        // 이전 단계와 현재 단계를 전달합니다.
        // 첫 상태 반영은 이전 단계가 null입니다.
        public event Action<ClientSessionPhase?, ClientSessionPhase> PhaseChanged;
        public void Clear()
        {
            SessionId = PlayerId = Role = ParticipantToken = null;
            Phase = null; lastUnlockedRooms = null;
            IsSceneLoaded = false;
            ParticipantCount = 0;
        }

        public void ApplyJoin(SessionJoinData response)
        {
            if (IsJoined)
                throw new InvalidOperationException("이미 세션에 입장했습니다.");

            if (response == null ||
            string.IsNullOrWhiteSpace(response.SessionId) ||
            response.Player == null ||
            string.IsNullOrWhiteSpace(response.Player.Id) ||
            string.IsNullOrWhiteSpace(response.ParticipantToken) ||
            (response.Player.Role != "A" && response.Player.Role != "B"))
            {
                throw new ArgumentException("유요하지 않은 입장응답 입니다. ");
            }

            SessionId = response.SessionId;
            PlayerId = response.Player.Id;
            Role = response.Player.Role;
            ParticipantToken = response.ParticipantToken;

            Joined?.Invoke();
        }
        public void ApplySnapshot(SessionSnapshotData snapshot)
        {
            if (!IsJoined)
                throw new InvalidOperationException("먼저 세션에 입장해야 합니다.");

            if (snapshot == null ||
                snapshot.SessionId != SessionId ||
                snapshot.Players == null ||
                snapshot.Players.Count < 1 ||
                snapshot.Players.Count > 2)
            {
                throw new ArgumentException("유효하지 않은 세션 스냅샷입니다.");
            }

            ClientSessionPhase nextPhase = snapshot.Phase;

            var playerIds = new HashSet<string>();
            var roles = new HashSet<string>();

            SessionPlayerData localPlayer = null;
            bool allLoaded = true;

            foreach (var player in snapshot.Players)
            {
                if (player == null ||
                    string.IsNullOrWhiteSpace(player.Id) ||
                    (player.Role != "A" && player.Role != "B") ||
                    !playerIds.Add(player.Id) ||
                    !roles.Add(player.Role))
                {
                    throw new ArgumentException("유효하지 않은 참가자 목록입니다.");
                }

                allLoaded &= player.SceneLoaded;

                if (player.Id == PlayerId)
                    localPlayer = player;
            }

            if (localPlayer == null || localPlayer.Role != Role)
                throw new ArgumentException("본인의 참가 정보가 일치하지 않습니다.");

            // 현재 서버의 두 명 정원 규칙과 응답이 일치하는지 확인합니다.
            ClientSessionPhase expectedPhase =
                snapshot.Players.Count < 2
                    ? ClientSessionPhase.Waiting
                    : allLoaded
                        ? ClientSessionPhase.Playing
                        : ClientSessionPhase.Loading;

            if (nextPhase != expectedPhase)
                throw new ArgumentException("참가 상태와 세션 단계가 일치하지 않습니다.");

            if (snapshot.UnlockedRoomIds != null)
            {
                var ids = new HashSet<int>();
                foreach (int id in snapshot.UnlockedRoomIds)
                    if (id <= 0 || !ids.Add(id)) throw new ArgumentException("잘못된 방 해금 목록입니다.");
            }
            var prevPhase = Phase;
            Phase = nextPhase;
            IsSceneLoaded = localPlayer.SceneLoaded;
            ParticipantCount = snapshot.Players.Count;

            SnapshotApplied?.Invoke();
            if (snapshot.UnlockedRoomIds != null && (lastUnlockedRooms == null || !new HashSet<int>(lastUnlockedRooms).SetEquals(snapshot.UnlockedRoomIds)))
            {
                lastUnlockedRooms = snapshot.UnlockedRoomIds.ToArray();
                RoomsChanged?.Invoke(Array.AsReadOnly(lastUnlockedRooms));
            }

            if(prevPhase != nextPhase)
            {
                PhaseChanged?.Invoke(prevPhase, nextPhase);
            }
        }
    }
}
