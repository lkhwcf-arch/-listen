using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace EscapeGame.World
{
    // 결과 프리팹과 독립적인 임시 협동 완료 대기 화면입니다.
    public sealed class EndingWaitingView : IDisposable
    {
        private GameObject root;
        private TMP_Text label;
        public EndingWaitingView(Scene scene, TMP_FontAsset font)
        {
            root = new GameObject("EndingWaitingCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(Image));
            SceneManager.MoveGameObjectToScene(root, scene);
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 4000;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            root.GetComponent<Image>().color = new Color(.04f, .06f, .09f, .96f);
            var text = new GameObject("EndingWaitingMessage", typeof(RectTransform), typeof(TextMeshProUGUI));
            text.transform.SetParent(root.transform, false);
            var rect = (RectTransform)text.transform; rect.sizeDelta = new Vector2(1400, 280);
            label = text.GetComponent<TMP_Text>(); label.font = font; label.fontSize = 38;
            label.alignment = TextAlignmentOptions.Center; label.color = Color.white;
            root.SetActive(false);
        }
        public void Render(bool visible, string message)
        { if (root != null) { label.text = message; root.SetActive(visible); } }
        public void Dispose()
        {
            if (root != null) { root.SetActive(false); Object.Destroy(root); }
            root = null; label = null;
        }
    }
}
