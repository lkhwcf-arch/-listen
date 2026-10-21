#if UNITY_EDITOR
using System;
using System.Linq;
using EscapeGame.Data;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EscapeGame.Editor
{
    public sealed class RoomClearTestWindow : EditorWindow
    {
        private RoomClearPreviewSession session;
        private int roleIndex;
        private Vector2 scroll;
        private string message = "Ingame 씬을 열고 Play한 뒤 미리보기를 시작하세요.";

        [MenuItem("Tools/Game/Room Clear Test")]
        public static void Open() => GetWindow<RoomClearTestWindow>("방 클리어 테스트");

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            SceneManager.activeSceneChanged += OnSceneChanged;
        }
        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            SceneManager.activeSceneChanged -= OnSceneChanged;
            Stop();
        }
        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode) Stop();
            Repaint();
        }
        private void OnSceneChanged(Scene previous, Scene next) { Stop(); Repaint(); }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("개발자 전용: CSV Next를 따라 다음 방 입구를 현재 방 출구에 맞춰 연결합니다. 방과 내부 오브젝트를 로컬 CSV로 생성합니다. 서버의 실제 클리어 상태는 변경하지 않습니다.", MessageType.Info);
            using (new EditorGUI.DisabledScope(session != null))
                roleIndex = EditorGUILayout.Popup("미리보기 역할", roleIndex, new[] { "A", "B" });
            using (new EditorGUI.DisabledScope(!Application.isPlaying || SceneManager.GetActiveScene().name != "Ingame" || session != null))
                if (GUILayout.Button("미리보기 시작")) Execute(StartPreview);
            using (new EditorGUI.DisabledScope(session == null))
                if (GUILayout.Button("미리보기 종료 / 상태 초기화")) { Stop(); message = "생성한 미리보기와 클리어 상태를 초기화했습니다."; }
            EditorGUILayout.HelpBox(message, MessageType.Info);
            if (session == null) return;
            using (new EditorGUI.DisabledScope(session.HasPlayer))
                if (GUILayout.Button("플레이어 생성 (카메라 + 손·무전기 임시 모델)")) Execute(() =>
                {
                    session.SpawnPlayer();
                    message = "플레이어 생성 완료. Game 뷰를 클릭하고 WASD로 이동하세요. 마우스로 시점을 회전할 수 있습니다. 생성된 방의 문은 좌클릭으로 열고 닫습니다. 다음 방 생성은 이 방 클리어 버튼을 사용하세요. 정답 UI는 Join의 클라이언트 테스트에서 확인하세요.";
                });
            scroll = EditorGUILayout.BeginScrollView(scroll);
            var current = session;
            foreach (var room in current.Progression.Rooms.OrderBy(row => row.RoomNo))
            {
                bool cleared = current.Progression.IsCleared(room);
                bool unlocked = current.Progression.IsUnlocked(room);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"R{room.RoomNo:00} / {current.Role} / {(cleared ? "클리어" : unlocked ? "개방" : "잠김")}", EditorStyles.boldLabel);
                using (new EditorGUI.DisabledScope(!unlocked || cleared))
                    if (GUILayout.Button("이 방 클리어 → 다음 방 자동 생성")) Execute(() =>
                    {
                        current.Clear(room.RoomNo);
                        message = room.Next == 0 ? $"R{room.RoomNo:00}: 마지막 방 클리어 완료" : $"R{room.RoomNo:00} 클리어 / Next={room.Next} 출구·입구 연결 완료";
                    });
                var target = current.FindPreview(room);
                using (new EditorGUI.DisabledScope(target == null))
                    if (GUILayout.Button("Scene 뷰에서 보기"))
                    {
                        Selection.activeGameObject = target;
                        SceneView.lastActiveSceneView?.FrameSelected();
                    }
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndScrollView();
        }

        private void StartPreview()
        {
            var assets = Resources.Load<GameContentAssets>("GameContent")
                ?? throw new InvalidOperationException("GameContent 에셋이 없습니다.");
            var store = new CsvDataStore();
            if (!store.TryRegister(assets.ReadLocalData(), assets.CreateValidator(), out var report))
                throw new InvalidOperationException("CSV 검증 실패: " + string.Join("\n", report.Issues.Take(5)));
            var next = new RoomClearPreviewSession(assets, new EscapeGame.Data.Tables.CsvGameData(store), roleIndex == 0 ? "A" : "B", SceneManager.GetActiveScene());
            try { next.Start(); }
            catch { next.Dispose(); throw; }
            session = next;
            session.Progression.Changed += Repaint;
            message = $"역할 {session.Role} / 첫 방과 CSV 오브젝트 준비. 원본 씬의 방은 복제하여 사용합니다. CSV 경고 {report.WarningCount}개.";
        }

        private void Execute(Action action)
        {
            try { action(); }
            catch (Exception error) { message = error.Message; Debug.LogWarning("[Room Clear Test] " + error.Message); }
        }
        private void Stop()
        {
            if (session == null) return;
            session.Progression.Changed -= Repaint;
            session.Dispose();
            session = null;
        }
    }
}
#endif
