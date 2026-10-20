using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace EscapeGame.Player
{
    public static class LocalPlayerFactory
    {
        public static FirstPersonPlayerRig Spawn(PlayerSpawnPlan plan, Transform roomRoot, PlayerPresentationSettings settings)
        {
            if (!Application.isPlaying) throw new InvalidOperationException("플레이어는 Play 모드에서 생성하세요.");
            if (plan == null || roomRoot == null || settings == null) throw new ArgumentNullException("플레이어 정의·방·표현 설정이 필요합니다.");
            settings.Validate();
            var anchors = roomRoot.GetComponentsInChildren<Transform>(true).Where(item => item.name == plan.Anchor.AnchorId).ToArray();
            if (anchors.Length != 1) throw new InvalidOperationException($"스폰 앵커 {plan.Anchor.AnchorId}는 정확히 하나여야 합니다. 현재 {anchors.Length}개입니다.");
            var scene = roomRoot.gameObject.scene;
            if (!scene.IsValid() || !scene.isLoaded || !roomRoot.gameObject.activeInHierarchy)
                throw new InvalidOperationException("플레이어를 생성할 방이 활성화되지 않았습니다.");
            if (Object.FindObjectsByType<FirstPersonPlayerRig>(FindObjectsInactive.Include, FindObjectsSortMode.None).Any(rig => rig.gameObject.scene == scene))
                throw new InvalidOperationException("현재 씬에 로컬 플레이어가 이미 있습니다.");
            var root = new GameObject("LocalPlayer_" + plan.Role);
            root.SetActive(false);
            SceneManager.MoveGameObjectToScene(root, scene);
            try
            {
                root.transform.SetPositionAndRotation(anchors[0].position, anchors[0].rotation);
                var controller = root.AddComponent<CharacterController>();
                PlayerBodyConfiguration.Apply(controller, plan.Character, settings.SkinWidthMm);
                root.AddComponent<PlayerCollisionProbe>();
                var cameraObject = new GameObject("FirstPersonCamera");
                cameraObject.tag = "MainCamera";
                cameraObject.transform.SetParent(root.transform, false);
                cameraObject.transform.localPosition = Vector3.up * (plan.EyeHeightMm / 1000f);
                var camera = cameraObject.AddComponent<Camera>();
                camera.fieldOfView = plan.FovMilliDegrees / 1000f;
                camera.nearClipPlane = settings.NearClipMm / 1000f;
                camera.farClipPlane = settings.FarClipMm / 1000f;
                cameraObject.AddComponent<UniversalAdditionalCameraData>();
                cameraObject.AddComponent<AudioListener>();
                var visual = new GameObject("VisualRoot").transform;
                visual.SetParent(cameraObject.transform, false);
                visual.localPosition = (Vector3)settings.ModelPositionMm / 1000f;
                visual.localRotation = Quaternion.Euler((Vector3)settings.ModelEulerMilliDegrees / 1000f);
                var prefab = settings.ModelFor(plan.Role);
                var model = prefab != null ? Object.Instantiate(prefab, visual, false)
                    : GameObject.CreatePrimitive(plan.UsesCylinder ? PrimitiveType.Cylinder : PrimitiveType.Cube);
                model.transform.SetParent(visual, false);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                model.name = prefab != null ? prefab.name : plan.UsesCylinder ? "RadioPlaceholder_B_Cylinder" : "RadioPlaceholder_A_Cube";
                if (prefab == null)
                {
                    var size = (Vector3)settings.ModelSizeMm / 1000f;
                    if (plan.UsesCylinder) size.y /= 2; // Unity의 기본 Cylinder 높이는 2입니다.
                    model.transform.localScale = size;
                }
                foreach (var collider in model.GetComponentsInChildren<Collider>(true))
                { collider.enabled = false; Object.Destroy(collider); }
                foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                    if (prefab == null && GraphicsSettings.currentRenderPipeline != null && GraphicsSettings.currentRenderPipeline.defaultMaterial != null)
                        renderer.sharedMaterial = GraphicsSettings.currentRenderPipeline.defaultMaterial;
                }
                var rig = root.AddComponent<FirstPersonPlayerRig>();
                rig.Initialize(plan.Role, camera, visual);
                var controls = new PlayerControlState();
                var movement = root.AddComponent<PlayerMovementDriver>();
                var input = new InputSystemMoveInput(settings.InputActions);
                try
                {
                    movement.Initialize(plan.Character, input, new EscapeGame.Timing.StopwatchMillisecondClock(),
                        settings.MaximumMovementFrameMs, settings.MaximumFallSpeedMmPerSecond, settings.GroundContactMm, controls);
                }
                catch { input.Dispose(); throw; }
                var pointerDriver = root.AddComponent<PlayerPointerDriver>();
                var look = root.AddComponent<PlayerLookDriver>();
                var pointer = new InputSystemPointerInput(settings.InputActions);
                try { pointerDriver.Initialize(pointer, controls); look.Initialize(camera.transform, plan.Character, settings.LookMilliDegreesPerPixelAtUnitSensitivity, pointerDriver); }
                catch { pointer.Dispose(); throw; }
                root.AddComponent<ViewModelObstructionDriver>().Initialize(camera, visual, settings.ModelCollisionMarginMm);
                root.SetActive(true);
                return rig;
            }
            catch { root.SetActive(false); Object.Destroy(root); throw; }
        }
    }
}
