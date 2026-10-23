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
        private readonly IReadOnlyList<RoomData> roomView;
        private readonly Dictionary<int, RoomData> roomsById;
        private readonly Dictionary<int, IReadOnlyList<AnchorData>> anchorsByRoomNo = new Dictionary<int, IReadOnlyList<AnchorData>>();
        private readonly Dictionary<int, string> prefabPaths;
        private RoomData first;
        public string Role
        {
            get;
        }
        public IReadOnlyList<RoomData> Rooms => roomView;
        private IReadOnlyList<RoomData> ordered;
        public IReadOnlyList<RoomData> OrderedRooms
        {
            get
            {
                if (ordered != null)
                    return ordered;
                var list = new List<RoomData>();
                var seen = new HashSet<int>();
                var row = First;
                while (row != null)
                {
                    if (!seen.Add(row.Id))
                        throw new InvalidOperationException("방 연결에 순환이 있습니다.");
                    list.Add(row);
                    row = row.Next == 0 ? null : Find(row.Next) ?? throw new InvalidOperationException("다음 방이 없습니다.");
                }
                return ordered = list.AsReadOnly();
            }
        }
        public RoomLoadPlan(CsvGameData data, string role)
        {
            this.data = data ?? throw new ArgumentNullException(nameof(data));
            if (role != "A" && role != "B")
                throw new ArgumentException("역할은 A 또는 B입니다.", nameof(role));
            Role = role;
            rooms = (role == "A" ? data.RoomsA.Cast<RoomData>() : data.RoomsB.Cast<RoomData>()).Where(room => room.Active).ToArray();
            roomView = Array.AsReadOnly(rooms);
            roomsById = rooms.ToDictionary(room => room.Id);
            prefabPaths = data.Prefabs.ToDictionary(prefab => prefab.Id, prefab => prefab.Path);
        }
        public RoomData First
        {
            get
            {
                if (first != null)
                    return first;
                var destinations = new HashSet<int>(rooms.Select(room => room.Next));
                RoomData start = null;
                foreach (var room in rooms)
                {
                    if (destinations.Contains(room.Id))
                        continue;
                    if (start != null)
                        throw new InvalidOperationException("활성 ROOM 시작 행은 정확히 하나여야 합니다.");
                    start = room;
                }
                return first = start ?? throw new InvalidOperationException("활성 ROOM 시작 행은 정확히 하나여야 합니다.");
            }
        }
        public RoomData Find(int id) => roomsById.TryGetValue(id, out var room) ? room : null;
        public IReadOnlyList<AnchorData> Anchors(RoomData room)
        {
            if (room == null)
                throw new ArgumentNullException(nameof(room));
            if (anchorsByRoomNo.TryGetValue(room.RoomNo, out var anchors))
                return anchors;
            anchors = BuildAnchors(room.RoomNo);
            anchorsByRoomNo.Add(room.RoomNo, anchors);
            return anchors;
        }
        // 캐시 적중 경로에서 LINQ 캡처 객체까지 생성되지 않도록 구성 경로를 분리합니다.
        private IReadOnlyList<AnchorData> BuildAnchors(int roomNo) => Array.AsReadOnly(data.InteractAnchors.Cast<AnchorData>().Concat(data.NonInteractAnchors)
            .Where(anchor => anchor.Active && anchor.RoomNo == roomNo && (anchor.PlayerType == "ALL" || anchor.PlayerType == Role))
            .OrderBy(anchor => anchor.Id).ToArray());
        public string PrefabPath(int id) => prefabPaths.TryGetValue(id, out var path) ? path
            : throw new InvalidOperationException("프리팹 정의가 없습니다: " + id);
    }
}
