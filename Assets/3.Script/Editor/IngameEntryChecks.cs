#if UNITY_EDITOR
using System;
using System.Linq;
using EscapeGame.Player;
using EscapeGame.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EscapeGame.Editor
{
    public static class IngameEntryChecks
    {
        [MenuItem("Tools/Game/Verify Ingame Wiring")]
        public static void VerifyWiring()
        {
            if (Application.isPlaying) { Debug.LogWarning("씬 구성 검사는 Play를 종료한 뒤 실행하세요."); return; }
            const string path = "Assets/1.Scene/Ingame.unity";
            var scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            int count = 0;
            void Check(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); count++; }
            try
            {
                var roots = scene.GetRootGameObjects();
                var worlds = roots.SelectMany(root => root.GetComponentsInChildren<IngameWorldDriver>(true)).ToArray();
                Check(worlds.Length == 1 && worlds[0].isActiveAndEnabled, "활성 WorldRuntime 로더가 정확히 하나 필요합니다.");
                var rooms = new SerializedObject(worlds[0]).FindProperty("sceneRooms");
                Check(rooms.arraySize == 1, "R01 씬 방을 로더에 연결하세요.");
                var view = rooms.GetArrayElementAtIndex(0).objectReferenceValue as RoomSceneView;
                Check(view != null && view.RoomNo == 1 && view.gameObject.scene == scene, "첫 방의 씬 참조가 유효해야 합니다.");
                var prefab = view.transform.Find("Room_01_Final");
                Check(prefab != null && PrefabUtility.GetNearestPrefabInstanceRoot(prefab.gameObject) == prefab.gameObject,
                    "프리팹 자식 구조물이 아닌 Room_01_Final 루트를 연결해야 합니다.");
                var bindings = new SerializedObject(view).FindProperty("embedded");
                for (int i = 0; i < bindings.arraySize; i++)
                {
                    string modelPath = bindings.GetArrayElementAtIndex(i).FindPropertyRelative("objectPath").stringValue;
                    Check(view.transform.Find(modelPath) != null, "포함 모델 경로가 없습니다: " + modelPath);
                }
                Resources.Load<PlayerPresentationSettings>("PlayerPresentation").Validate();
                Debug.Log($"[Ingame Wiring] {count} 씬 참조 검사 통과. B LEDPanel 콘텐츠 배치 완료와는 별도입니다.");
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }

        [MenuItem("Tools/Game/Verify Gameplay Readiness")]
        public static void VerifyReadiness()
        {
            if (!Application.isPlaying) { Debug.LogWarning("Join에서 정상 입장한 Play 모드에서 검사하세요."); return; }
            var owner = UnityEngine.Object.FindFirstObjectByType<GameFlowBootstrapper>();
            if (owner == null || !owner.IsGameplayReady)
            { Debug.LogWarning("방·플레이어가 모두 준비된 뒤 검사하세요. 누락 콘텐츠를 준비 완료로 처리하지 않습니다."); return; }
            var player = UnityEngine.Object.FindObjectsByType<FirstPersonPlayerRig>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Single(rig => rig.gameObject.scene == SceneManager.GetActiveScene());
            try
            {
                player.gameObject.SetActive(false);
                if (owner.IsGameplayReady) throw new InvalidOperationException("플레이어 비활성 시 준비 완료가 해제되어야 합니다.");
            }
            finally { player.gameObject.SetActive(true); }
            if (!owner.IsGameplayReady) throw new InvalidOperationException("플레이어 복원 뒤 준비 상태가 복원되어야 합니다.");
            Debug.Log("[Gameplay Readiness] 플레이어 준비 조건 2개 검사 통과.");
        }
    }
}
#endif
