using System;
using UnityEngine;

namespace EscapeGame.Interaction
{
    // 정수 설정을 Unity 좌표로 바꾸는 표현 경계입니다. 모델 임포트 축과 관찰자 축을 구분합니다.
    public static class InteractionPresentationGeometry
    {
        public static Quaternion DoorLocalRotation(Quaternion closedLocal, Quaternion parentWorld, int angleMilliDegrees)
        {
            var worldTurn = Quaternion.AngleAxis(angleMilliDegrees / 1000f, Vector3.up);
            return Quaternion.Inverse(parentWorld) * worldTurn * parentWorld * closedLocal;
        }

        public static Vector3 DoorWorldPosition(Vector3 closedWorld, Vector3 hingeWorld, int angleMilliDegrees)
        {
            return hingeWorld + Quaternion.AngleAxis(angleMilliDegrees / 1000f, Vector3.up) * (closedWorld - hingeWorld);
        }

        public static Vector3 HorizontalForward(Vector3 viewForward, Vector3 viewRight)
        {
            var forward = Vector3.ProjectOnPlane(viewForward, Vector3.up);
            if (forward.sqrMagnitude < .000001f)
                forward = Vector3.Cross(Vector3.ProjectOnPlane(viewRight, Vector3.up), Vector3.up);
            return forward.sqrMagnitude < .000001f ? Vector3.forward : forward.normalized;
        }

        public static Vector3 PreviewOffset(int[] offsetMm, Vector3 viewForward, Vector3 viewRight)
        {
            if (offsetMm == null || offsetMm.Length != 3)
                throw new ArgumentException("미리보기 오프셋은 정수 mm 3개여야 합니다.");
            var forward = HorizontalForward(viewForward, viewRight);
            var right = Vector3.Cross(Vector3.up, forward).normalized;
            return (right * offsetMm[0] + Vector3.up * offsetMm[1] + forward * offsetMm[2]) / 1000f;
        }
    }
}
