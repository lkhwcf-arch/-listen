using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EscapeGame.Session.Presentation
{
    // 화면 생성/표시만 담당합니다. 통신 서비스나 인증 정보는 참조하지 않습니다.
    public sealed class SessionDisconnectDialog : MonoBehaviour
    {
        private TMP_Text message;
        private Button confirm;
        public static SessionDisconnectDialog Create(Transform parent, TMP_FontAsset font)
        {
            var overlay = new GameObject("SessionDisconnectDialog", typeof(RectTransform), typeof(Image));
            overlay.transform.SetParent(parent, false);
            var rect = (RectTransform)overlay.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            overlay.GetComponent<Image>().color = new Color(0, 0, 0, 0.7f);
            var dialog = overlay.AddComponent<SessionDisconnectDialog>();

            var panel = CreateRect("Panel", rect, 680, 340, 0, 0);
            panel.gameObject.AddComponent<Image>().color = new Color(0.11f, 0.14f, 0.19f);
            CreateText("Title", panel, font, "연결 종료", 30, 80);
            dialog.message = CreateText("Message", panel, font, "", 24, 0);
            var buttonRect = CreateRect("Confirm", panel, 220, 58, 0, -112);
            buttonRect.gameObject.AddComponent<Image>().color = new Color(0.06f, 0.48f, 0.43f);
            var button = buttonRect.gameObject.AddComponent<Button>();
            dialog.confirm = button;
            button.targetGraphic = buttonRect.GetComponent<Image>();
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() => dialog.gameObject.SetActive(false));
            CreateText("Label", buttonRect, font, "확인", 24, 0);
            overlay.SetActive(false);
            return dialog;
        }

        private static RectTransform CreateRect(string name, Transform parent, int width, int height, int x, int y)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x, y);
            return rect;
        }

        private static TMP_Text CreateText(string name, Transform parent, TMP_FontAsset font, string text, int size, int y)
        {
            var rect = CreateRect(name, parent, name == "Label" ? 200 : 610, name == "Message" ? 115 : 60, 0, y);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.text = text;
            label.fontSize = size;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;
            return label;
        }

        public void Show(string text)
        {
            message.text = text;
            transform.SetAsLastSibling();
            gameObject.SetActive(true);
            confirm.Select();
        }
    }
}
