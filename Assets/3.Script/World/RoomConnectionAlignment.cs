using System;
using System.Linq;
using UnityEngine;

namespace EscapeGame.World
{
    // Unity 공간 배치 전용 모듈입니다. 클리어·CSV·서버 권한을 소유하지 않습니다.
    public static class RoomConnectionAlignment
    {
        public static Transform FindEntry(Transform root, int roomNo) => FindPoint(root, roomNo, "EntryPoint");
        public static Transform FindExit(Transform root, int roomNo) => FindPoint(root, roomNo, "DoorPoint");

        private static Transform FindPoint(Transform root, int roomNo, string suffix)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            if (roomNo < 1) throw new ArgumentOutOfRangeException(nameof(roomNo));
            string name = $"R{roomNo:00}_{suffix}";
            var matches = root.GetComponentsInChildren<Transform>(true).Where(item => item.name == name).ToArray();
            if (matches.Length != 1)
                throw new InvalidOperationException($"방 연결점 {name}이 정확히 1개 필요합니다. 현재 {matches.Length}개입니다. 문턱 중앙에 배치하고 파란색 Z+ 축을 방 바깥쪽으로 맞춰 주세요.");
            return matches[0];
        }

        public static void Align(Transform nextRoomRoot, Transform nextEntry, Transform currentExit)
        {
            if (nextRoomRoot == null || nextEntry == null || currentExit == null)
                throw new ArgumentNullException("방 루트와 입구·출구 연결점이 필요합니다.");
            if (nextEntry == nextRoomRoot || !nextEntry.IsChildOf(nextRoomRoot))
                throw new InvalidOperationException("입구 연결점은 다음 방 루트의 자식이어야 합니다.");
            if (currentExit == nextRoomRoot || currentExit.IsChildOf(nextRoomRoot))
                throw new InvalidOperationException("현재 방 출구와 다음 방은 서로 분리된 계층이어야 합니다.");
            // 비균등 스케일의 부모 아래에서 회전하면 모델이 찌그러질 수 있으므로 추측하지 않습니다.
            for (var parent = nextRoomRoot; parent != null; parent = parent.parent)
            {
                var scale = parent.localScale;
                if (scale.x <= 0 || !Mathf.Approximately(scale.x, scale.y) || !Mathf.Approximately(scale.x, scale.z))
                    throw new InvalidOperationException("연결할 방 루트와 부모는 양수의 균등 스케일을 사용해야 합니다.");
            }
            // 양쪽 Z+는 각 방의 바깥 방향입니다. 접합하면 서로 반대 방향이어야 합니다.
            var targetEntryRotation = currentExit.rotation * Quaternion.AngleAxis(180, Vector3.up);
            var delta = targetEntryRotation * Quaternion.Inverse(nextEntry.rotation);
            var rotation = delta * nextRoomRoot.rotation;
            var position = currentExit.position - delta * (nextEntry.position - nextRoomRoot.position);
            nextRoomRoot.SetPositionAndRotation(position, rotation);
        }
    }
}
