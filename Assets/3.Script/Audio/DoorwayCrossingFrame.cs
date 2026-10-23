using System;
using EscapeGame.World;
using UnityEngine;

namespace EscapeGame.Audio
{
    // 문을 닫았을 때의 공간을 보관합니다. 임포트 축·경첩 회전과 통과 판정의 축을 분리합니다.
    public sealed class DoorwayCrossingFrame
    {
        private readonly Vector3 center, normal, right;
        private readonly double halfWidth, halfHeight;
        private readonly DoorwayCrossing crossing;
        public DoorwayCrossingFrame(Matrix4x4 anchorToWorld, Bounds closedBounds, double margin)
        {
            var axes = new[] { anchorToWorld.MultiplyVector(Vector3.right), anchorToWorld.MultiplyVector(Vector3.up), anchorToWorld.MultiplyVector(Vector3.forward) };
            int up = 0;
            for (int i = 1; i < 3; i++)
                if (Mathf.Abs(Vector3.Dot(axes[i].normalized, Vector3.up)) > Mathf.Abs(Vector3.Dot(axes[up].normalized, Vector3.up)))
                    up = i;
            int thin = -1;
            for (int i = 0; i < 3; i++)
                if (i != up && (thin < 0 || closedBounds.size[i] * axes[i].magnitude < closedBounds.size[thin] * axes[thin].magnitude))
                    thin = i;
            normal = Vector3.ProjectOnPlane(axes[thin], Vector3.up).normalized;
            if (normal.sqrMagnitude < .9f)
                throw new InvalidOperationException("문 앞뒤 방향을 계산할 수 없습니다. 문 모델의 축·크기를 확인하세요.");
            right = Vector3.Cross(Vector3.up, normal).normalized;
            center = anchorToWorld.MultiplyPoint3x4(closedBounds.center);
            for (int i = 0; i < 8; i++)
            {
                var corner = closedBounds.center + Vector3.Scale(closedBounds.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var delta = anchorToWorld.MultiplyPoint3x4(corner) - center;
                halfWidth = Math.Max(halfWidth, Math.Abs(Vector3.Dot(delta, right)));
                halfHeight = Math.Max(halfHeight, Math.Abs(delta.y));
            }
            crossing = new DoorwayCrossing(margin); // 월드 미터: 부모 스케일과 별개인 중립 구간입니다.
        }
        public void Reset() => crossing.Reset();
        public bool Crossed(Vector3 previous, Vector3 current, bool open)
        {
            var a = previous - center;
            var b = current - center;
            return crossing.Tick(Vector3.Dot(a, right), a.y, Vector3.Dot(a, normal),
                Vector3.Dot(b, right), b.y, Vector3.Dot(b, normal), halfWidth, halfHeight, open);
        }
    }
}
