using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using Object = UnityEngine.Object;

namespace EscapeGame.World
{
    public sealed class EndingResultView : IDisposable
    {
        private GameObject root, eventSystem;
        private Button confirm;
        private readonly Action quit;
        public EndingResultView(GameObject prefab, Scene scene, TMP_FontAsset font, long elapsedMs, Action quit)
        {
            this.quit = quit ?? throw new ArgumentNullException(nameof(quit));
            if (prefab == null) throw new InvalidOperationException("GameFeedback에 Result_UI 프리팹을 연결하세요.");
            try
            {
                root = Object.Instantiate(prefab); root.name = "Result_UI"; SceneManager.MoveGameObjectToScene(root, scene);
                var canvas = root.GetComponent<Canvas>() ?? throw new InvalidOperationException("Result_UI 루트에 Canvas가 필요합니다.");
                canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 6000;
                var scaler = root.GetComponent<CanvasScaler>();
                if (scaler != null) { scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); }
                foreach (var text in root.GetComponentsInChildren<TMP_Text>(true)) text.font = font;
                SetField("Panel/Time_Text", "탈출 시간", font); SetField("Panel/Time_Space", EscapeRunClock.Format(elapsedMs), font);
                var title = root.transform.Find("Panel/Text (TMP)")?.GetComponent<TMP_Text>(); if (title != null) title.text = "탈출 성공";
                confirm = root.transform.Find("Panel/Button")?.GetComponent<Button>() ?? throw new InvalidOperationException("Result_UI의 Panel/Button이 필요합니다.");
                confirm.GetComponentInChildren<TMP_Text>(true).text = "확인"; confirm.onClick.AddListener(OnConfirm);
                if (EventSystem.current == null)
                {
                    eventSystem = new GameObject("ResultEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                    SceneManager.MoveGameObjectToScene(eventSystem, scene); eventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
                }
            }
            catch { Dispose(); throw; }
        }
        private void SetField(string path, string value, TMP_FontAsset font)
        {
            var field = root.transform.Find(path)?.GetComponent<TMP_InputField>() ?? throw new InvalidOperationException("결과 표시 영역을 찾지 못했습니다: " + path);
            field.readOnly = true; field.interactable = false;
            var colors = field.colors; colors.disabledColor = colors.normalColor; field.colors = colors;
            field.fontAsset = font; field.pointSize = 42; field.SetTextWithoutNotify(value);
            field.textComponent.alignment = TextAlignmentOptions.Center;
        }
        private void OnConfirm() => quit();
        public void SetVisible(bool visible) { if (root != null) root.SetActive(visible); }
        public void Dispose()
        {
            if (confirm != null) confirm.onClick.RemoveListener(OnConfirm);
            if (root != null) { root.SetActive(false); Object.Destroy(root); }
            if (eventSystem != null) Object.Destroy(eventSystem);
        }
    }
}
