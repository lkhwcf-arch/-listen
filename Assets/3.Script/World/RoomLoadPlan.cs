using System;
using System.Collections.Generic;
using System.Linq;
using EscapeGame.Data.Tables;

namespace EscapeGame.World
{
    // Unity 오브젝트와 독립적인 선택 규칙입니다. 씬과 서버 데이터의 연결은 이 계획을 사용합니다.
    public sealed class RoomLoadPlan
    {
        private readonly CsvGameData data;
        private readonly RoomData[] rooms;
        public string Role { get; }
        public IReadOnlyList<RoomData> Rooms => Array.AsReadOnly(rooms);
        private IReadOnlyList<RoomData> ordered;
        public IReadOnlyList<RoomData> OrderedRooms
        {
            get
            {
                if (ordered != null) return ordered;
                var list = new List<RoomData>(); var seen = new HashSet<int>(); var row = First;
                while (row != null)
                {
                    if (!seen.Add(row.Id)) throw new InvalidOperationException("방 연결에 순환이 있습니다.");
                    list.Add(row); row = row.Next == 0 ? null : Find(row.Next) ?? throw new InvalidOperationException("다음 방이 없습니다.");
                }
                return ordered = list.AsReadOnly();
            }
        }
        public RoomLoadPlan(CsvGameData data, string role)
        {
            this.data = data ?? throw new ArgumentNullException(nameof(data));
            if (role != "A" && role != "B") throw new ArgumentException("역할은 A 또는 B입니다.", nameof(role));
            Role = role;
            rooms = (role == "A" ? data.RoomsA.Cast<RoomData>() : data.RoomsB.Cast<RoomData>()).Where(room => room.Active).ToArray();
        }
        public RoomData First
        {
            get
            {
                var starts = rooms.Where(room => !rooms.Any(previous => previous.Next == room.Id)).ToArray();
                if (starts.Length != 1) throw new InvalidOperationException("활성 ROOM 시작 행은 정확히 하나여야 합니다.");
                return starts[0];
            }
        }
        public RoomData Find(int id) => rooms.FirstOrDefault(room => room.Id == id);
        public IReadOnlyList<AnchorData> Anchors(RoomData room) => Array.AsReadOnly(data.InteractAnchors.Cast<AnchorData>()
            .Concat(data.NonInteractAnchors).Where(anchor => anchor.Active && anchor.RoomNo == room.RoomNo &&
                (anchor.PlayerType == "ALL" || anchor.PlayerType == Role)).OrderBy(anchor => anchor.Id).ToArray());
        public string PrefabPath(int id) => data.Prefabs.Single(prefab => prefab.Id == id).Path;
    }
}
