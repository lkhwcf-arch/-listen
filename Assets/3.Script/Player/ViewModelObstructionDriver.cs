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
        private Vector3 restPosition, restCenterInParent;
        private float probeRadius;
        private int marginMm;
        public bool IsObstructed
        {
            get;
            private set;
        }
        public void Initialize(Camera view, Transform visual, int marginMm)
        {
            if (this.view != null)
                throw new InvalidOperationException("모델 보정은 한 번만 초기화합니다.");
            if (marginMm < 1 || marginMm > 100)
                throw new ArgumentOutOfRangeException(nameof(marginMm));
            this.view = view != null ? view : throw new ArgumentNullException(nameof(view));
            this.visual = visual != null ? visual : throw new ArgumentNullException(nameof(visual));
            this.marginMm = marginMm;
            surfaces = new SolidSurfaceQuery(transform);
            restPosition = visual.localPosition;
            renderers = visual.GetComponentsInChildren<Renderer>(true);
            originallyEnabled = new bool[renderers.Length];
            bool hasBounds = false;
            var bounds = new Bounds();
            for (int i = 0; i < renderers.Length; i++)
            {
                originallyEnabled[i] = renderers[i].enabled;
                if (!originallyEnabled[i])
                    continue;
                if (!hasBounds)
                {
                    bounds = renderers[i].bounds;
                    hasBounds = true;
                }
                else
                    bounds.Encapsulate(renderers[i].bounds);
            }
            // 팔 전체를 원점 중심 구로 감싸면 눈까지 구에 포함되어 모델이 항상 숨겨집니다.
            // 모델 중심과 두께를 초기화 시 캐시합니다. 애니메이션별 정밀 충돌 대신 표현 보정용 근사값입니다.
            restCenterInParent = visual.parent.InverseTransformPoint(hasBounds ? bounds.center : visual.position);
            probeRadius = hasBounds ? Mathf.Max(.001f, Mathf.Min(bounds.extents.x, bounds.extents.y, bounds.extents.z)) : .001f;
        }
        private void LateUpdate()
        {
            if (view == null || visual == null)
                return;
            var offset = visual.parent.TransformPoint(restCenterInParent) - view.transform.position;
            float distance = offset.magnitude;
            if (distance < .001f)
            {
                SetVisible(false);
                return;
            }
            float radius = probeRadius + marginMm / 1000f;
            var direction = offset / distance;
            float allowed = surfaces.AllowedDistance(view.transform.position, radius, direction, distance, marginMm / 1000f);
            float forward = Vector3.Dot(view.transform.forward, direction);
            bool visible = forward > 0 && allowed * forward - radius > view.nearClipPlane;
            IsObstructed = allowed < distance;
            visual.localPosition = restPosition + visual.parent.InverseTransformVector(direction * (allowed - distance));
            SetVisible(visible);
        }
        private void SetVisible(bool visible)
        {
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null)
                    renderers[i].enabled = originallyEnabled[i] && visible;
        }
        private void OnDisable()
        {
            if (visual != null)
                visual.localPosition = restPosition;
            if (renderers != null)
                SetVisible(true);
            IsObstructed = false;
        }
    }
}
