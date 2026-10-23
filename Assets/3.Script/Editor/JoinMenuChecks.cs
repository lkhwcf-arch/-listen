#if UNITY_EDITOR
using System;
using System.Linq;
using EscapeGame.Session.Presentation;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace EscapeGame.Editor
{
    public sealed class JoinMenuChecks : IPreprocessBuildWithReport
    {
        public int callbackOrder => -90;
        public void OnPreprocessBuild(BuildReport report)
        {
            try
            {
                VerifyWiring();
            }
            catch (Exception error)
            {
                throw new BuildFailedException(error.Message);
            }
        }
        [MenuItem("Tools/Game/시작·대기 UI 연결 검사")]
        public static void VerifyWiring()
        {
            if (Application.isPlaying)
                throw new InvalidOperationException("씬 연결 검사는 Play를 종료한 뒤 실행하세요.");
            var scene = SceneManager.GetSceneByPath("Assets/1.Scene/Join.unity");
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened)
                scene = EditorSceneManager.OpenScene("Assets/1.Scene/Join.unity", OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                var menus = roots.SelectMany(root => root.GetComponentsInChildren<JoinMenuScreen>(true)).ToArray();
                if (menus.Length != 1 || !menus[0].isActiveAndEnabled)
                    throw new InvalidOperationException("Join에 활성 JoinMenuScreen이 하나 필요합니다.");
                menus[0].ValidateConfiguration();
                if (roots.SelectMany(root => root.GetComponentsInChildren<SessionConnectionScreen>(true)).Any(view => view.isActiveAndEnabled))
                    throw new InvalidOperationException("기존 자동 접속 화면은 비활성화해야 합니다.");
                if (roots.SelectMany(root => root.GetComponentsInChildren<EventSystem>(true)).Count(view => view.isActiveAndEnabled) != 1)
                    throw new InvalidOperationException("Join의 활성 EventSystem은 하나여야 합니다.");
                Debug.Log("[시작 메뉴] 씬 수명·이벤트 시스템·프리팹·버튼·대기 텍스트 연결 검사 통과.");
            }
            finally
            {
                if (opened)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }
        [MenuItem("Tools/Game/시작·대기 UI 표시 검사")]
        public static void VerifyPresentation()
        {
            if (!Application.isPlaying)
                throw new InvalidOperationException("Join Play에서 실행하세요.");
            var source = UnityEngine.Object.FindFirstObjectByType<JoinMenuView>();
            if (source == null)
                throw new InvalidOperationException("활성 Join 메뉴가 없습니다.");
            var host = new GameObject("시작 메뉴 격리 검사");
            host.SetActive(false);
            var view = host.AddComponent<JoinMenuView>();
            EditorUtility.CopySerialized(source, view);
            try
            {
                host.SetActive(true);
                view.Initialize();
                view.Render(new SessionConnectionPresentation("로딩 중", "B", "2", "Ingame 로딩 중", false, false));
                if (view.HasWaitingRoom)
                    throw new InvalidOperationException("두 번째 참가자는 대기 UI를 생성하면 안 됩니다.");
                view.Render(new SessionConnectionPresentation("대기", "A", "1", "상대방을 기다립니다.", false, false, showWaitingRoom: true));
                if (!view.HasWaitingRoom)
                    throw new InvalidOperationException("첫 참가자의 대기 UI가 생성되지 않았습니다.");
                view.Render(new SessionConnectionPresentation("입장 선택", "-", "0", "재시도", true, false));
                if (view.HasWaitingRoom)
                    throw new InvalidOperationException("재시도에 이전 대기 UI가 남았습니다.");
                Debug.Log("[시작 메뉴] 두 번째 참가자 생성 생략·첫 참가자 대기 생성·재시도 정리 검사 통과.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }
    }
}
#endif
