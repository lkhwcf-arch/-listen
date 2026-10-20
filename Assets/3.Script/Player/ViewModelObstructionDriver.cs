using System;
using UnityEngine;

namespace EscapeGame.Player
{
    // 카메라 앞 모델의 표현만 조정합니다. 플레이어 캡슐/방 충돌체를 바꾸지 않습니다.
    public sealed class ViewModelObstructionDriver : MonoBehaviour
    {
        private SolidSurfaceQuery surfaces;
        private Renderer[] renderers;
        private bool[] originallyEnabled;
        private Camera view;
        private Transform visual;
        private Vector3 restPosition;
        private int marginMm;
        public bool IsObstructed { get; private set; }
        public void Initialize(Camera view, Transform visual, int marginMm)
        {
            if (this.view != null) throw new InvalidOperationException("모델 보정은 한 번만 초기화합니다.");
            if (marginMm < 1 || marginMm > 100) throw new ArgumentOutOfRangeException(nameof(marginMm));
            this.view = view != null ? view : throw new ArgumentNullException(nameof(view));
            this.visual = visual != null ? visual : throw new ArgumentNullException(nameof(visual));
            this.marginMm = marginMm;
            surfaces = new SolidSurfaceQuery(transform);
            restPosition = visual.localPosition;
            renderers = visual.GetComponentsInChildren<Renderer>(true);
            originallyEnabled = new bool[renderers.Length];
            for (int i = 0; i < renderers.Length; i++) originallyEnabled[i] = renderers[i].enabled;
        }
        private void LateUpdate()
        {
            if (view == null || visual == null) return;
            float radius = 0;
            // 원점 중심의 보수적인 구: 회전된 큐브·손 모델도 보호합니다. 배열/메시를 매 프레임 생성하지 않습니다.
            for (int i = 0; i < renderers.Length; i++)
            {
                if (!originallyEnabled[i] || renderers[i] == null) continue;
                var bounds = renderers[i].localBounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    var point = bounds.center + Vector3.Scale(bounds.extents, new Vector3(
                        (corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                    radius = Mathf.Max(radius, visual.InverseTransformPoint(renderers[i].transform.TransformPoint(point)).magnitude);
                }
            }
            radius *= Mathf.Max(Mathf.Abs(visual.lossyScale.x), Mathf.Abs(visual.lossyScale.y), Mathf.Abs(visual.lossyScale.z));
            radius += marginMm / 1000f;
            var offset = visual.parent.TransformVector(restPosition);
            float distance = offset.magnitude;
            if (distance < 0.001f) { SetVisible(false); return; }
            var direction = offset / distance;
            float allowed = surfaces.AllowedDistance(view.transform.position, radius, direction, distance, marginMm / 1000f);
            float forward = Vector3.Dot(view.transform.forward, direction);
            bool visible = forward > 0 && allowed * forward - radius > view.nearClipPlane;
            IsObstructed = allowed < distance;
            visual.localPosition = restPosition * (allowed / distance);
            SetVisible(visible);
        }
        private void SetVisible(bool visible)
        { for (int i = 0; i < renderers.Length; i++) if (renderers[i] != null) renderers[i].enabled = originallyEnabled[i] && visible; }
        private void OnDisable()
        { if (visual != null) visual.localPosition = restPosition; if (renderers != null) SetVisible(true); IsObstructed = false; }
    }
}
