using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EscapeGame.Interaction
{
    // Closeup·후속 정답/문 화면이 공유하는 표현 생성기입니다. 게임 데이터와 HTTP를 참조하지 않습니다.
    public static class InteractionUiFactory
    {
        public static RectTransform Rect(string name, Transform parent, int width, int height, int x, int y)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x, y);
            return rect;
        }
        public static TMP_Text Text(string name, Transform parent, TMP_FontAsset font, string text, int width, int height, int x, int y, int size)
        {
            var label = Rect(name, parent, width, height, x, y).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.text = text;
            label.fontSize = size;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            return label;
        }
        public static Button Button(string name, Transform parent, TMP_FontAsset font, string text, int width, int height, int x, int y)
        {
            var rect = Rect(name, parent, width, height, x, y);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(.04f, .45f, .4f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            Text("Label", rect, font, text, width - 12, height, 0, 0, 24);
            return button;
        }
    }
}
