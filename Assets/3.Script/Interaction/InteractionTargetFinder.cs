using UnityEngine;

namespace EscapeGame.Interaction
{
    // 첫 표면이 대상이 아니면 그 뒤의 대상도 선택하지 않습니다. 벽 너머 상호작용을 차단합니다.
    public sealed class InteractionTargetFinder
    {
        private readonly RaycastHit[] hits = new RaycastHit[32];
        private readonly Transform player;
        public InteractionTargetFinder(Transform player) => this.player = player;
        public WorldInteractionTarget Find(Ray ray, int distanceMm)
        {
            if (distanceMm <= 0) return null;
            int count = Physics.RaycastNonAlloc(ray, hits, distanceMm / 1000f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count == hits.Length) return null;
            Collider nearest = null; float distance = float.MaxValue;
            for (int i = 0; i < count; i++)
                if (!hits[i].collider.transform.IsChildOf(player) && hits[i].distance < distance)
                { nearest = hits[i].collider; distance = hits[i].distance; }
            var target = nearest != null ? nearest.GetComponentInParent<WorldInteractionTarget>() : null;
            return target != null && target.IsAvailable ? target : null;
        }
    }
}
