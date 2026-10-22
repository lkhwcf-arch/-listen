using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EscapeGame.Data.Tables;
using EscapeGame.World;
using UnityEngine;

namespace EscapeGame.Interaction
{
    // CSV Door 기능과 프리팹의 DoorHinge를 연결합니다. 문짝과 충돌체가 경첩을 중심으로 함께 이동합니다.
    public sealed class WorldDoorView : MonoBehaviour, IAnchorDataReceiver
    {
        public const string HingeName = "DoorHinge";
        private Transform hinge;
        private Vector3 closedPosition;
        private int roomNo, openAngleMilliDegrees;
        private Quaternion closedRotation;
        private DoorInteractionController controller;
        public Transform DoorAnchor { get; private set; }
        public Bounds ClosedBoundsInAnchor { get; private set; }
        public bool IsOpen => controller != null && controller.IsOpen;
        public bool IsBusy => controller != null && controller.IsBusy;
        public void Initialize(AnchorData anchor, CsvGameData data)
        {
            if (!(anchor is AnchorInteractData) || !anchor.Active || !new PrefabFeaturePlan(data, anchor.PrefabId).Has(FeatureScripts.Door))
                throw new InvalidOperationException("활성 Door 앵커만 문으로 구성합니다.");
            var points = GetComponentsInChildren<Transform>(true).Where(point => point != transform && point.name == HingeName).ToArray();
            if (points.Length != 1) throw new InvalidOperationException("Door 프리팹에는 DoorHinge 기준점이 정확히 하나 필요합니다: " + anchor.PrefabId);
            hinge = points[0];
            DoorAnchor = transform.parent != null ? transform.parent : transform;
            bool hasBounds = false; Bounds closedBounds = default;
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                var box = renderer.localBounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = box.center + Vector3.Scale(box.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var point = DoorAnchor.InverseTransformPoint(renderer.transform.TransformPoint(corner));
                    if (!hasBounds) { closedBounds = new Bounds(point, Vector3.zero); hasBounds = true; } else closedBounds.Encapsulate(point);
                }
            }
            ClosedBoundsInAnchor = closedBounds;
            roomNo = anchor.RoomNo; closedPosition = transform.localPosition; closedRotation = transform.localRotation;
        }
        public Task<bool> ToggleAsync(IRoomDoorService service, int angleMilliDegrees, CancellationToken token)
        {
            if (service == null) throw new InvalidOperationException("문 판정 서비스가 연결되지 않았습니다.");
            if (angleMilliDegrees == 0 || angleMilliDegrees < -180000 || angleMilliDegrees > 180000)
                throw new ArgumentOutOfRangeException(nameof(angleMilliDegrees));
            if (controller == null)
            {
                openAngleMilliDegrees = angleMilliDegrees;
                controller = new DoorInteractionController(roomNo, service);
                controller.Changed += ApplyPose;
            }
            return controller.ToggleAsync(token);
        }
        private void ApplyPose()
        {
            // 닫힌 자세에서 다시 계산하여 개폐 오차가 누적되지 않도록 합니다.
            transform.SetLocalPositionAndRotation(closedPosition, closedRotation);
            if (IsOpen)
            {
                var parentWorld = transform.parent != null ? transform.parent.rotation : Quaternion.identity;
                var openPosition = InteractionPresentationGeometry.DoorWorldPosition(transform.position, hinge.position, openAngleMilliDegrees);
                var openRotation = parentWorld * InteractionPresentationGeometry.DoorLocalRotation(closedRotation, parentWorld, openAngleMilliDegrees);
                transform.SetPositionAndRotation(openPosition, openRotation);
            }
            Physics.SyncTransforms();
        }
        private void OnDestroy()
        {
            if (controller == null) return;
            controller.Changed -= ApplyPose; controller.Dispose(); controller = null;
        }
    }
}
