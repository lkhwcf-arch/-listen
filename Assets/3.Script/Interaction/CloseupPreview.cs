using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace EscapeGame.Interaction
{
    // 표시용 Mesh/Material만 복사합니다. 원본 입력·물리·통신 스크립트는 복제하지 않습니다.
    public sealed class CloseupPreview : IDisposable
    {
        private readonly List<Mesh> ownedMeshes = new List<Mesh>();
        private GameObject root;
        private Camera worldCamera;
        private int savedMask;
        public RenderTexture Texture { get; private set; }
        public CloseupPreview(WorldInteractionTarget target, Camera worldCamera, InteractionPresentationSettings settings)
        {
            this.worldCamera = worldCamera; savedMask = worldCamera.cullingMask;
            try
            {
                root = new GameObject("CloseupVisualPreview"); root.SetActive(false); SceneManager.MoveGameObjectToScene(root, worldCamera.gameObject.scene);
                root.transform.position = new Vector3(10000, 10000, 10000);
                worldCamera.cullingMask &= ~(1 << settings.PreviewLayer);
                var visual = Copy(target.transform, root.transform, settings.PreviewLayer);
                visual.localPosition = Vector3.zero; visual.rotation = target.transform.rotation; visual.localScale = target.transform.lossyScale;
                var renderers = visual.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0) throw new InvalidOperationException("CloseUpUI 대상에 표시할 메시가 없습니다.");
                root.SetActive(true); // 활성화 후 갱신된 월드 bounds로 카메라를 배치합니다.
                var bounds = renderers[0].bounds; foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                Texture = new RenderTexture(settings.PreviewSizePixels, settings.PreviewSizePixels, 24, RenderTextureFormat.ARGB32) { name = "CloseupPreview" }; Texture.Create();
                var cameraObject = new GameObject("CloseupCamera"); cameraObject.transform.SetParent(root.transform, false);
                var camera = cameraObject.AddComponent<Camera>(); camera.cullingMask = 1 << settings.PreviewLayer; camera.targetTexture = Texture;
                camera.fieldOfView = settings.PreviewFovMilliDegrees / 1000f; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.04f, .05f, .07f);
                cameraObject.AddComponent<UniversalAdditionalCameraData>();
                var lightObject = new GameObject("PreviewLight"); lightObject.transform.SetParent(root.transform, false);
                var light = lightObject.AddComponent<Light>(); light.type = LightType.Directional; light.cullingMask = 1 << settings.PreviewLayer;
                lightObject.transform.rotation = Quaternion.Euler(35, -30, 0);
                int[] raw = target.Features.CloseupCameraOffsetMm();
                var forward = InteractionPresentationGeometry.HorizontalForward(worldCamera.transform.forward, worldCamera.transform.right);
                var offset = InteractionPresentationGeometry.PreviewOffset(raw, worldCamera.transform.forward, worldCamera.transform.right);
                float radius = bounds.extents.magnitude;
                float distance = Mathf.Max(offset.magnitude, radius / Mathf.Sin(camera.fieldOfView * Mathf.Deg2Rad / 2) * 1.1f);
                camera.nearClipPlane = Mathf.Max(.001f, distance - radius * 1.5f); camera.farClipPlane = distance + radius * 2 + 1;
                camera.transform.position = bounds.center + offset.normalized * distance; camera.transform.LookAt(bounds.center, Mathf.Abs(offset.normalized.y) > .99f ? forward : Vector3.up);
                lightObject.transform.rotation = camera.transform.rotation * Quaternion.Euler(25, -25, 0);
                root.SetActive(true);
            }
            catch { Dispose(); throw; }
        }
        private Transform Copy(Transform source, Transform parent, int layer)
        {
            var node = new GameObject(source.name).transform; node.SetParent(parent, false); node.gameObject.layer = layer;
            node.localPosition = source.localPosition; node.localRotation = source.localRotation; node.localScale = source.localScale;
            var filter = source.GetComponent<MeshFilter>(); var renderer = source.GetComponent<MeshRenderer>();
            Mesh mesh = null; Material[] materials = null;
            if (filter != null && renderer != null && renderer.enabled) { mesh = filter.sharedMesh; materials = renderer.sharedMaterials; }
            var skinned = source.GetComponent<SkinnedMeshRenderer>();
            if (skinned != null && skinned.enabled) { mesh = new Mesh(); skinned.BakeMesh(mesh); ownedMeshes.Add(mesh); materials = skinned.sharedMaterials; }
            if (mesh != null)
            {
                node.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                var clone = node.gameObject.AddComponent<MeshRenderer>(); clone.sharedMaterials = materials; clone.shadowCastingMode = ShadowCastingMode.Off; clone.receiveShadows = false;
            }
            foreach (Transform child in source) if (child.gameObject.activeSelf) Copy(child, node, layer);
            return node;
        }
        public void Dispose()
        {
            if (worldCamera != null) { worldCamera.cullingMask = savedMask; worldCamera = null; }
            if (root != null) { root.SetActive(false); InteractionObjectLifetime.Destroy(root); root = null; }
            if (Texture != null) { Texture.Release(); InteractionObjectLifetime.Destroy(Texture); Texture = null; }
            foreach (var mesh in ownedMeshes) InteractionObjectLifetime.Destroy(mesh); ownedMeshes.Clear();
        }
    }
}
