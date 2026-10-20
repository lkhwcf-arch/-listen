#if UNITY_EDITOR
using System;
using System.Linq;
using EscapeGame.Data;
using EscapeGame.Data.Csv;
using EscapeGame.Data.Tables;
using EscapeGame.Interaction;
using EscapeGame.Player;
using EscapeGame.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace EscapeGame.Editor
{
    public static class InteractionChecks
    {
        private static AnchorInteractData TestAnchor() => new AnchorInteractData(new CsvParser().Read(
            "ID,RoomNo,PlayerType,AnchorID,PrefabID,Active\n99001,1,ALL,TemporaryCloseupTarget,13005,Y").Rows[0]);
        private static GameObject Target(CsvGameData data, Vector3 position)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube); cube.name = "[Temporary] Closeup Target";
            cube.transform.position = position; cube.transform.localScale = Vector3.one * .3f;
            if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null)
                cube.GetComponent<Renderer>().sharedMaterial = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.defaultMaterial;
            var row = TestAnchor(); AnchorFeatureFactory.Compose(cube, row, data);
            cube.AddComponent<AnchorInstance>().Initialize(row, data);
            return cube;
        }
        [MenuItem("Tools/Game/클로즈업 테스트 대상 생성")]
        public static void SpawnTestTarget()
        {
            if (!Application.isPlaying) { Debug.LogWarning("Join에서 클라이언트 테스트 A로 입장한 후 실행하세요."); return; }
            var owner = Object.FindFirstObjectByType<GameFlowBootstrapper>();
            var player = Object.FindFirstObjectByType<FirstPersonPlayerRig>();
            if (owner?.Service?.Data == null || player == null) throw new InvalidOperationException("게임 입장과 플레이어 준비가 필요합니다.");
            var cube = Target(owner.Service.Data, player.PlayerCamera.transform.position + player.PlayerCamera.transform.forward * 1.25f);
            SceneManager.MoveGameObjectToScene(cube, player.gameObject.scene);
            Debug.Log("[클로즈업] 임시 큐브에 실제 PREFAB 13005의 기능 선언을 적용했습니다. 원본 CSV/프리팹은 변경하지 않습니다.", cube);
        }
        [MenuItem("Tools/Game/데이터 기반 상호작용 검사")]
        public static void Verify()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Play를 종료한 뒤 검사하세요.");
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            int checks = 0;
            void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); checks++; }
            try
            {
                var assets = Resources.Load<GameContentAssets>("GameContent"); var store = new CsvDataStore();
                if (!store.TryRegister(assets.ReadLocalData(), assets.CreateValidator(), out _)) throw new InvalidOperationException("CSV 검증 실패");
                var data = new CsvGameData(store); var origin = new Vector3(-1000, 100, -1000);
                var player = new GameObject("Temporary Player"); player.transform.position = origin;
                var cube = Target(data, origin + Vector3.forward * 1.5f); var target = cube.GetComponent<WorldInteractionTarget>();
                Check(target != null && target.IsAvailable && target.SupportsCloseup, "Factory 구성 후 앵커 데이터 주입");
                Check(target.HighlightColor == new Color(1, 1, 0), "Params의 HTML 색 Yellow 적용");
                var finder = new InteractionTargetFinder(player.transform); var ray = new Ray(origin, Vector3.forward);
                Physics.SyncTransforms();
                Check(finder.Find(ray, 2000) == target && finder.Find(ray, 500) == null && finder.Find(ray, 0) == null, "정수 mm 거리 제한");
                var own = GameObject.CreatePrimitive(PrimitiveType.Cube); own.transform.SetParent(player.transform, false); own.transform.localPosition = Vector3.forward * .4f; own.transform.localScale = Vector3.one * .1f;
                Physics.SyncTransforms(); Check(finder.Find(ray, 2000) == target, "자기 몸 충돌체 제외");
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.position = origin + Vector3.forward * .8f; wall.transform.localScale = Vector3.one * .2f;
                Physics.SyncTransforms(); Check(finder.Find(ray, 2000) == null, "벽 뒤 대상 차단");
                wall.GetComponent<Collider>().isTrigger = true; Physics.SyncTransforms(); Check(finder.Find(ray, 2000) == target, "Trigger 제외");
                cube.SetActive(false); Check(!target.IsAvailable, "비활성 대상 선택 불가"); cube.SetActive(true);
                var renderer = cube.GetComponent<Renderer>(); var saved = new MaterialPropertyBlock(); saved.SetFloat("_FixtureValue", .5f); renderer.SetPropertyBlock(saved);
                using (new TargetHighlight(target)) { var tinted = new MaterialPropertyBlock(); renderer.GetPropertyBlock(tinted); Check(tinted.GetColor("_BaseColor") == new Color(1, 1, 0), "개별 속성으로 강조 적용"); }
                var restored = new MaterialPropertyBlock(); renderer.GetPropertyBlock(restored); Check(restored.GetFloat("_FixtureValue") == .5f && restored.GetColor("_BaseColor") == default, "기존 강조 속성 복원");
                var settings = Resources.Load<InteractionPresentationSettings>("InteractionPresentation"); settings.Validate();
                var camera = player.AddComponent<Camera>(); camera.enabled = false; int mask = camera.cullingMask;
                using (var preview = new CloseupPreview(target, camera, settings))
                {
                    Check(preview.Texture != null && preview.Texture.IsCreated(), "미리보기 렌더 텍스처 생성");
                    var previewRoot = scene.GetRootGameObjects().Single(item => item.name == "CloseupVisualPreview");
                    Check(previewRoot.GetComponentsInChildren<Collider>().Length == 0 && previewRoot.GetComponentsInChildren<WorldInteractionTarget>().Length == 0, "미리보기에 게임 기능 미복제");
                    var previewCamera = previewRoot.GetComponentInChildren<Camera>();
                    Check(previewCamera.targetTexture == preview.Texture && previewCamera.cullingMask == 1 << settings.PreviewLayer, "미리보기 카메라 격리");
                    var visual = previewRoot.GetComponentsInChildren<Renderer>().Single();
                    Check(visual.bounds.size.sqrMagnitude > 0 && Vector3.Dot(previewCamera.transform.forward, (visual.bounds.center - previewCamera.transform.position).normalized) > .99f, "실제 메시 범위로 미리보기 카메라 정렬");
                    Check((camera.cullingMask & (1 << settings.PreviewLayer)) == 0, "월드 카메라에서 미리보기 레이어 제외");
                }
                Check(camera.cullingMask == mask && !scene.GetRootGameObjects().Any(item => item.name == "CloseupVisualPreview"), "미리보기 해제와 카메라 복원");
                using (var panel = new CloseupPanelView(scene, settings))
                { Check(!panel.IsVisible, "패널 초기 비표시"); panel.Show(1); Check(panel.IsVisible, "패널 표시"); panel.Hide(); Check(!panel.IsVisible && panel.Preview.texture == null, "패널 닫기와 텍스처 해제"); }
                var controls = new PlayerControlState(); controls.SetActive(true); controls.CapturePointer();
                using (var closeup = new CloseupController(controls.BlockForUi))
                {
                    closeup.Open(target); Check(!controls.GameplayEnabled && !controls.CanCapture, "공통 UI 상태로 이동·시점·커서 획득 차단");
                    closeup.Close(); controls.CapturePointer(); Check(controls.GameplayEnabled, "닫기 후 UI 잠금 해제");
                }
                Object.DestroyImmediate(cube); Check(!target.IsAvailable, "제거한 Unity 대상의 안전한 가용성 판단");
                Debug.Log($"[상호작용] Unity 검사 {checks}개 통과. 실제 클릭과 UI 표시는 Play에서 별도 확인하세요.");
            }
            finally { EditorSceneManager.CloseScene(scene, true); if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous); }
        }
    }
}
#endif
