using System;
using EscapeGame.Interaction;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EscapeGame.World
{
    // 게임 준비 중에는 로드된 월드를 가리고 대기 이유를 표시합니다.
    public sealed class SessionWaitingView : IDisposable
    {
        private readonly GameObject root;
        private readonly TMP_Text label;
        public SessionWaitingView(Scene scene, TMP_FontAsset font)
        {
            root = new GameObject("SessionWaitingCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(root, scene);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 3000;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            var background = new GameObject("WaitingBackground", typeof(RectTransform), typeof(Image));
            background.transform.SetParent(root.transform, false);
            var rect = (RectTransform)background.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            background.GetComponent<Image>().color = new Color(.04f, .06f, .09f, 1);
            label = InteractionUiFactory.Text("WaitingMessage", background.transform, font, "", 1400, 220, 0, 0, 36);
            root.SetActive(false);
        }
        public void Render(bool canPlay, string message)
        {
            label.text = message;
            root.SetActive(!canPlay);
        }
        public void Dispose()
        {
            if (root == null)
                return;
            root.SetActive(false);
            InteractionObjectLifetime.Destroy(root);
        }
    }
}
