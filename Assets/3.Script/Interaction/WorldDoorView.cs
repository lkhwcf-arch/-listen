using System;
using System.Threading;
using System.Threading.Tasks;
using EscapeGame.Data.Tables;
using EscapeGame.World;
using UnityEngine;

namespace EscapeGame.Interaction
{
    // CSV Door 기능을 가진 인스턴스만 구성합니다. 원래 프리팹의 피벗으로 문과 충돌체를 함께 회전합니다.
    public sealed class WorldDoorView : MonoBehaviour, IAnchorDataReceiver
    {
        private int roomNo, openAngleMilliDegrees;
        private Quaternion closedRotation;
        private DoorInteractionController controller;
        public bool IsOpen => controller != null && controller.IsOpen;
        public bool IsBusy => controller != null && controller.IsBusy;
        public void Initialize(AnchorData anchor, CsvGameData data)
        {
            if (!(anchor is AnchorInteractData) || !anchor.Active || !new PrefabFeaturePlan(data, anchor.PrefabId).Has(FeatureScripts.Door))
                throw new InvalidOperationException("활성 Door 앵커만 문으로 구성합니다.");
            roomNo = anchor.RoomNo; closedRotation = transform.localRotation;
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
            transform.localRotation = IsOpen ? closedRotation * Quaternion.AngleAxis(openAngleMilliDegrees / 1000f, Vector3.up) : closedRotation;
            Physics.SyncTransforms();
        }
        private void OnDestroy()
        {
            if (controller == null) return;
            controller.Changed -= ApplyPose; controller.Dispose(); controller = null;
        }
    }
}
