#if UNITY_EDITOR
using System;
using System.Linq;
using EscapeGame.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace EscapeGame.Editor
{
    public static class PlayerLookAndCollisionChecks
    {
        [MenuItem("Tools/Game/Inspect Player Collision")]
        public static void Inspect()
        {
            if (!Application.isPlaying) { Debug.LogWarning("벽/문에 접근한 Play 모드에서 실행하세요."); return; }
            var rig = Object.FindObjectsByType<FirstPersonPlayerRig>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Single(item => item.gameObject.scene == SceneManager.GetActiveScene());
            var body = rig.GetComponent<CharacterController>(); var probe = rig.GetComponent<PlayerCollisionProbe>();
            var collider = probe.LastBlockingCollider;
            string PathOf(Transform node) => node.parent == null ? node.name : PathOf(node.parent) + "/" + node.name;
            string contact = collider == null ? "아직 측면 접촉 없음" : PathOf(collider.transform) + " (" + collider.GetType().Name + ")";
            string detail = "";
            if (collider is MeshCollider mesh)
            {
                var filter = mesh.GetComponent<MeshFilter>();
                detail = $"\nsharedMesh={mesh.sharedMesh?.name}, visible mesh와 동일={filter != null && filter.sharedMesh == mesh.sharedMesh}, convex={mesh.convex}";
            }
            Debug.Log($"[Player Collision] radius={Mathf.RoundToInt(body.radius * 1000)}mm, skin={Mathf.RoundToInt(body.skinWidth * 1000)}mm" +
                $"\n최근 측면 접촉={contact}{detail}\n접촉점={probe.LastPoint}, 법선={probe.LastNormal}" +
                $"\n손 모델 보정 중={rig.GetComponent<ViewModelObstructionDriver>().IsObstructed}. 최근 접촉 기록이며 현재 거리 측정은 아닙니다.", collider);
        }

        [MenuItem("Tools/Game/Verify Player Look And Collision")]
        public static void Verify()
        {
            if (!Application.isPlaying) { Debug.LogWarning("Play 모드에서 실행하세요."); return; }
            int checks = 0;
            void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); checks++; }
            var options = Resources.Load<PlayerPresentationSettings>("PlayerPresentation"); options.Validate();
            var originalLook = options.InputActions.FindAction("Player/Look", true);
            bool wasEnabled = originalLook.enabled;
            using (var pointer = new InputSystemPointerInput(options.InputActions))
            {
                pointer.SetEnabled(true); pointer.SetLookEnabled(true);
                Check(originalLook.enabled == wasEnabled, "source look action ownership is preserved");
                pointer.SetLookEnabled(false); Check(pointer.ReadLook().RightMilliPixels == 0 && pointer.ReadLook().UpMilliPixels == 0, "disabled look is idle");
            }
            Check(originalLook.enabled == wasEnabled, "dispose preserves source action");
            var source = originalLook.bindings;
            Check(source.Any(binding => binding.path == "<Pointer>/delta" && binding.groups.Contains("Keyboard&Mouse")), "mouse delta binding exists");
            var room = new GameObject("[Temporary] Model Obstruction Check"); room.transform.position = new Vector3(10000, 10000, 10000);
            try
            {
                var cameraObject = new GameObject("Camera"); cameraObject.transform.SetParent(room.transform, false);
                var view = cameraObject.AddComponent<Camera>(); view.enabled = false; view.nearClipPlane = 0.03f;
                var visual = new GameObject("Visual").transform; visual.SetParent(cameraObject.transform, false); visual.localPosition = new Vector3(0.18f, -0.2f, 0.5f);
                var model = GameObject.CreatePrimitive(PrimitiveType.Cube); model.transform.SetParent(visual, false); model.transform.localScale = Vector3.one * 0.18f;
                Object.DestroyImmediate(model.GetComponent<Collider>());
                var driver = room.AddComponent<ViewModelObstructionDriver>(); driver.Initialize(view, visual, options.ModelCollisionMarginMm);
                Physics.SyncTransforms(); driver.SendMessage("LateUpdate");
                Check(model.GetComponent<Renderer>().enabled && visual.localPosition.z == 0.5f, "unobstructed model keeps authored pose");
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.name = "Wall"; wall.transform.SetParent(room.transform, false);
                wall.transform.localPosition = new Vector3(0, 0, 0.4f); wall.transform.localScale = new Vector3(4, 4, 0.1f);
                Physics.SyncTransforms(); driver.SendMessage("LateUpdate");
                Check(driver.IsObstructed && visual.localPosition.z < 0.5f, "model retracts before wall");
                wall.transform.localPosition = new Vector3(0, 0, 0.1f); Physics.SyncTransforms(); driver.SendMessage("LateUpdate");
                Check(!model.GetComponent<Renderer>().enabled, "model hidden when it cannot fit before near plane");
                wall.SetActive(false); Physics.SyncTransforms(); driver.SendMessage("LateUpdate");
                Check(model.GetComponent<Renderer>().enabled && visual.localPosition.z == 0.5f, "moving away restores original renderer and pose");
                driver.enabled = false;
                Check(model.GetComponent<Renderer>().enabled && visual.localPosition.z == 0.5f, "disable restores owned presentation state");
                Debug.Log($"[Player Look/Collision] {checks} Unity checks passed. 실제 방/커서 조작은 별도 수동 검사입니다.");
            }
            finally { Object.DestroyImmediate(room); }
        }
    }
}
#endif
