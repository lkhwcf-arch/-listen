using UnityEngine;

namespace EscapeGame.Player
{
    // 충돌 정책을 바꾸지 않는 관측 어댑터. 진단 메뉴가 실제로 맞닿은 메시를 식별합니다.
    public sealed class PlayerCollisionProbe : MonoBehaviour
    {
        public Collider LastBlockingCollider
        {
            get;
            private set;
        }
        public Vector3 LastPoint
        {
            get;
            private set;
        }
        public Vector3 LastNormal
        {
            get;
            private set;
        }
        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (Mathf.Abs(hit.normal.y) > 0.7f)
                return;
            LastBlockingCollider = hit.collider;
            LastPoint = hit.point;
            LastNormal = hit.normal;
        }
    }
}
