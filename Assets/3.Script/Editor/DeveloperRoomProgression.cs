using System;
using System.Collections.Generic;
using System.Linq;
using EscapeGame.Data.Tables;
using EscapeGame.World;

namespace EscapeGame.Editor
{
    // Editor 폴더의 순수 C# 상태입니다. 서버 판정과 실제 게임 진행에는 사용하지 않습니다.
    public sealed class DeveloperRoomProgression
    {
        private readonly RoomLoadPlan plan;
        private readonly HashSet<int> unlocked = new HashSet<int>();
        private readonly HashSet<int> cleared = new HashSet<int>();
        public IReadOnlyList<RoomData> Rooms => plan.Rooms;
        public RoomData First => plan.First;
        public event Action Changed;

        public DeveloperRoomProgression(RoomLoadPlan plan)
        {
            this.plan = plan ?? throw new ArgumentNullException(nameof(plan));
            unlocked.Add(plan.First.Id);
        }

        public bool IsUnlocked(RoomData room) => room != null && unlocked.Contains(room.Id);
        public bool IsCleared(RoomData room) => room != null && cleared.Contains(room.Id);
        public RoomData FindRoom(int roomNo) => Rooms.FirstOrDefault(room => room.RoomNo == roomNo)
            ?? throw new InvalidOperationException("현재 역할의 활성 방이 없습니다: " + roomNo);

        // 생성 어댑터가 먼저 프리팹을 준비한 뒤 Complete를 호출합니다.
        public RoomData NextForClear(int roomNo)
        {
            var room = FindRoom(roomNo);
            if (!unlocked.Contains(room.Id))
                throw new InvalidOperationException("아직 개방되지 않은 방입니다: " + roomNo);
            if (cleared.Contains(room.Id) || room.Next == 0)
                return null;
            return plan.Find(room.Next) ?? throw new InvalidOperationException("Next에 연결된 활성 방이 없습니다: " + room.Next);
        }

        public bool Complete(int roomNo)
        {
            var next = NextForClear(roomNo);
            var room = FindRoom(roomNo);
            if (!cleared.Add(room.Id))
                return false;
            if (next != null)
                unlocked.Add(next.Id);
            Changed?.Invoke();
            return true;
        }
    }
}
