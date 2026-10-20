using System;
using UnityEngine;

namespace EscapeGame.Player
{
    // 월드 메시의 앞/뒷면과 무관하게 화면 모델이 놓일 수 있는 거리만 계산하는 물리 어댑터입니다.
    public sealed class SolidSurfaceQuery
    {
        private readonly RaycastHit[] hits = new RaycastHit[32];
        private readonly Collider[] overlaps = new Collider[32];
        private readonly Transform owner;
        public SolidSurfaceQuery(Transform owner) => this.owner = owner != null ? owner : throw new ArgumentNullException(nameof(owner));

        public float AllowedDistance(Vector3 origin, float radius, Vector3 direction, float desiredDistance, float margin)
        {
            int count = Physics.OverlapSphereNonAlloc(origin, radius, overlaps, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count == overlaps.Length) return 0;
            for (int i = 0; i < count; i++) if (Foreign(overlaps[i])) return 0;
            float allowed = desiredDistance;
            count = Physics.SphereCastNonAlloc(origin, radius, direction, hits, desiredDistance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count == hits.Length) return 0;
            for (int i = 0; i < count; i++) if (Foreign(hits[i].collider))
                allowed = Mathf.Min(allowed, Mathf.Max(0, hits[i].distance - margin));

            // MeshCollider의 한쪽 면을 놓치는 경우를 반대편에서 보완합니다.
            // 역방향 구 중심은 표면의 먼 쪽에 닿으므로 지름(2r)을 빼서 눈 쪽의 안전 거리로 환산합니다.
            float reach = desiredDistance + 2 * radius;
            count = Physics.SphereCastNonAlloc(origin + direction * reach, radius, -direction, hits, reach,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count == hits.Length) return 0;
            for (int i = 0; i < count; i++) if (Foreign(hits[i].collider))
                allowed = Mathf.Min(allowed, Mathf.Max(0, reach - hits[i].distance - 2 * radius - margin));
            return allowed;
        }
        private bool Foreign(Collider collider) => collider != null && !collider.transform.IsChildOf(owner);
    }
}
