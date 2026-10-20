#if UNITY_EDITOR
using System;
using System.Linq;
using EscapeGame.Data;
using EscapeGame.Data.Tables;
using EscapeGame.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace EscapeGame.Editor
{
    public static class PlayerSpawnChecks
    {
        [MenuItem("Tools/Game/Verify Player Spawn")]
        public static void Run()
        {
            if (!Application.isPlaying) { Debug.LogWarning("Play 모드에서 Verify Player Spawn을 실행하세요."); return; }
            var assets = Resources.Load<GameContentAssets>("GameContent");
            var store = new CsvDataStore();
            if (!store.TryRegister(assets.ReadLocalData(), assets.CreateValidator(), out var report))
                throw new InvalidOperationException("현재 CSV가 유효하지 않습니다.");
            var data = new CsvGameData(store);
            var settings = Resources.Load<PlayerPresentationSettings>("PlayerPresentation");
            var scene = SceneManager.CreateScene("[Temporary] Player Spawn Checks");
            var root = new GameObject("TemporaryRoom");
            SceneManager.MoveGameObjectToScene(root, scene);
            var cameraObject = new GameObject("PreviousCamera");
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            cameraObject.tag = "Untagged"; // 현재 Ingame 카메라처럼 태그가 없어도 소유권을 넘겨야 합니다.
            var camera = cameraObject.AddComponent<Camera>();
            var listener = cameraObject.AddComponent<AudioListener>();
            FirstPersonPlayerRig rig = null;
            int checks = 0;
            void Check(bool ok, string label) { if (!ok) throw new InvalidOperationException(label); checks++; }
            try
            {
                foreach (string role in new[] { "A", "B" })
                {
                    var plan = new PlayerSpawnPlan(data, role, settings.SpawnPrefabId, settings.CharacterConfigId);
                    var anchor = new GameObject(plan.Anchor.AnchorId).transform;
                    anchor.SetParent(root.transform, false);
                    anchor.localPosition = new Vector3(7, 3, -4);
                    anchor.localRotation = Quaternion.Euler(0, 73, 0);
                    rig = LocalPlayerFactory.Spawn(plan, root.transform, settings);
                    Check(Vector3.Distance(rig.transform.position, anchor.position) < 0.0001f, "spawn uses declared anchor position");
                    Check(Quaternion.Angle(rig.transform.rotation, anchor.rotation) < 0.01f, "spawn uses declared anchor rotation");
                    Check(Vector3.Distance(rig.PlayerCamera.transform.position, anchor.TransformPoint(Vector3.up * (plan.EyeHeightMm / 1000f))) < 0.0001f, "camera eye offset follows anchor");
                    Check(rig.VisualRoot.IsChildOf(rig.PlayerCamera.transform), "view model follows camera");
                    Check(rig.VisualRoot.childCount == 1, "one view model");
                    if (settings.ModelFor(role) == null)
                        Check(rig.VisualRoot.GetChild(0).name.Contains(role == "A" ? "Cube" : "Cylinder"), "placeholder matches role");
                    Check(!rig.VisualRoot.GetComponentsInChildren<Collider>(true).Any(c => c.enabled), "view model has no active collisions");
                    Check(!camera.enabled && !listener.enabled, "previous camera and listener relinquish ownership");
                    bool duplicate = false;
                    try { LocalPlayerFactory.Spawn(plan, root.transform, settings); } catch (InvalidOperationException) { duplicate = true; }
                    Check(duplicate, "duplicate player rejected");
                    Object.DestroyImmediate(rig.gameObject); rig = null;
                    Check(camera.enabled && listener.enabled, "camera and listener restored on removal");
                    Object.DestroyImmediate(anchor.gameObject);
                }
                Debug.Log($"[Player Spawn Checks] {checks} Unity checks passed.");
            }
            finally
            {
                if (rig != null) Object.DestroyImmediate(rig.gameObject);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(root);
                SceneManager.UnloadSceneAsync(scene);
            }
        }
    }
}
#endif
