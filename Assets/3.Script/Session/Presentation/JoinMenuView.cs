using System;
using EscapeGame.World;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EscapeGame.Session.Presentation
{
    // 프리팹을 표시하는 Unity Adapter입니다. 인원 판정·HTTP 요청은 Presenter/Service가 소유합니다.
    public sealed class JoinMenuView : MonoBehaviour, IGameEntryView
    {
        [SerializeField] private GameObject startPrefab;
        [SerializeField] private GameObject waitingRoomPrefab;
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private Button localTestATemplate;
        [SerializeField] private Button localTestBTemplate;
        private GameObject startRoot, waitingRoot;
        private Button escapeButton, localA, localB;
        private TMP_Text startMessage, waitingMessage, playerA, playerB;
        private Image dotA, dotB;
        public event Action JoinRequested;
        public event Action<string> LocalRequested;
        public event Action LeaveRequested { add { } remove { } }
        public bool HasWaitingRoom => waitingRoot != null;

        public void ValidateConfiguration()
        {
            if (startPrefab == null || waitingRoomPrefab == null || font == null)
                throw new InvalidOperationException("시작·대기 프리팹과 한글 폰트를 연결하세요.");
            if (startPrefab.GetComponent<Canvas>() == null || waitingRoomPrefab.GetComponent<Canvas>() == null)
                throw new InvalidOperationException("시작·대기 프리팹 루트에 Canvas가 필요합니다.");
            Require<Button>(startPrefab, "Start_Button");
            Require<TMP_Text>(waitingRoomPrefab, "Panel/Message_Box/Message_Text");
            Require<TMP_Text>(waitingRoomPrefab, "Panel/Player_Box/Player1_Row/Name_Text");
            Require<TMP_Text>(waitingRoomPrefab, "Panel/Player_Box/Player2_Row/Name_Text");
            Require<Image>(waitingRoomPrefab, "Panel/Player_Box/Player1_Row/Status_Dot");
            Require<Image>(waitingRoomPrefab, "Panel/Player_Box/Player2_Row/Status_Dot");
        }
        public void Initialize()
        {
            if (startRoot != null) return;
            ValidateConfiguration();
            startRoot = CreateScreen(startPrefab);
            escapeButton = Require<Button>(startRoot, "Start_Button");
            escapeButton.onClick.AddListener(OnEscape);
            var label = escapeButton.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = "탈출하기";
            startMessage = CreateStartMessage(startRoot.transform);
            if (Application.isEditor)
            {
                localA = CreateLocalButton(localTestATemplate, startRoot.transform, "클라이언트 테스트 A", 80);
                localB = CreateLocalButton(localTestBTemplate, startRoot.transform, "클라이언트 테스트 B", 135);
                localA?.onClick.AddListener(OnLocalA); localB?.onClick.AddListener(OnLocalB);
            }
        }
        private GameObject CreateScreen(GameObject prefab)
        {
            var instance = Instantiate(prefab, transform, false);
            instance.name = prefab.name;
            try
            {
                // 제작 프리팹의 Canvas 루트가 0 스케일이므로 복제본만 정상화합니다.
                instance.transform.localScale = Vector3.one;
                var canvas = instance.GetComponent<Canvas>() ?? throw new InvalidOperationException(prefab.name + "에 Canvas가 필요합니다.");
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = instance.GetComponent<CanvasScaler>();
                if (scaler != null)
                {
                    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
                }
                foreach (var text in instance.GetComponentsInChildren<TMP_Text>(true)) text.font = font;
                return instance;
            }
            catch { Destroy(instance); throw; }
        }
        private void EnsureWaitingRoom()
        {
            if (waitingRoot != null) return;
            waitingRoot = CreateScreen(waitingRoomPrefab);
            waitingMessage = Require<TMP_Text>(waitingRoot, "Panel/Message_Box/Message_Text");
            playerA = Require<TMP_Text>(waitingRoot, "Panel/Player_Box/Player1_Row/Name_Text");
            playerB = Require<TMP_Text>(waitingRoot, "Panel/Player_Box/Player2_Row/Name_Text");
            dotA = Require<Image>(waitingRoot, "Panel/Player_Box/Player1_Row/Status_Dot");
            dotB = Require<Image>(waitingRoot, "Panel/Player_Box/Player2_Row/Status_Dot");
            foreach (var label in new[] { playerA, playerB })
            { label.enableAutoSizing = true; label.fontSizeMin = 20; label.fontSizeMax = 32; }
        }
        public void Render(SessionConnectionPresentation presentation)
        {
            Initialize();
            escapeButton.interactable = presentation.CanJoin;
            if (localA != null) localA.interactable = presentation.CanJoin;
            if (localB != null) localB.interactable = presentation.CanJoin;
            startMessage.text = presentation.Message;
            if (presentation.ShowWaitingRoom)
            {
                EnsureWaitingRoom();
                bool both = presentation.Participants == "2";
                RenderPlayer(playerA, dotA, "A", both || presentation.Role == "A", presentation.Role);
                RenderPlayer(playerB, dotB, "B", both || presentation.Role == "B", presentation.Role);
                waitingMessage.text = presentation.Message;
                startRoot.SetActive(false); waitingRoot.SetActive(true);
            }
            else
            {
                startRoot.SetActive(true);
                if (waitingRoot != null)
                {
                    waitingRoot.SetActive(false);
                    if (presentation.CanJoin) { Destroy(waitingRoot); waitingRoot = null; }
                }
            }
        }
        private static void RenderPlayer(TMP_Text label, Image dot, string role, bool joined, string ownRole)
        {
            label.text = "플레이어 " + role + (role == ownRole ? " (나)" : "") + (joined ? " · 접속됨" : " · 대기 중");
            dot.color = joined ? new Color(.2f, .8f, .5f) : new Color(.4f, .4f, .4f);
        }
        private TMP_Text CreateStartMessage(Transform parent)
        {
            var go = new GameObject("접속 안내", typeof(RectTransform)); go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform; rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0);
            rect.anchoredPosition = new Vector2(0, 50); rect.sizeDelta = new Vector2(1500, 70);
            var text = go.AddComponent<TextMeshProUGUI>(); text.font = font; text.fontSize = 26;
            text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false; return text;
        }
        private Button CreateLocalButton(Button template, Transform parent, string title, int y)
        {
            if (template == null) return null;
            var button = Instantiate(template, parent, false); button.gameObject.name = title;
            button.onClick = new Button.ButtonClickedEvent();
            var rect = (RectTransform)button.transform; rect.anchorMin = rect.anchorMax = new Vector2(0, 0);
            rect.localScale = Vector3.one; rect.sizeDelta = new Vector2(280, 44); rect.anchoredPosition = new Vector2(170, y);
            foreach (var label in button.GetComponentsInChildren<TMP_Text>(true)) { label.font = font; label.text = title; }
            button.gameObject.SetActive(true); return button;
        }
        private static T Require<T>(GameObject root, string path) where T : Component
            => root.transform.Find(path)?.GetComponent<T>() ?? throw new InvalidOperationException(root.name + " 필수 UI 경로 누락: " + path);
        public void ShowConfigurationError(string message)
        {
            if (startRoot == null) return;
            startRoot.SetActive(true); startMessage.text = message; escapeButton.interactable = false;
            if (localA != null) localA.interactable = false;
            if (localB != null) localB.interactable = false;
        }
        public void ShowDisconnected(string message) => ShowConfigurationError(message);
        private void OnEscape() => JoinRequested?.Invoke();
        private void OnLocalA() => LocalRequested?.Invoke("A");
        private void OnLocalB() => LocalRequested?.Invoke("B");
        private void OnDestroy()
        {
            if (escapeButton != null) escapeButton.onClick.RemoveListener(OnEscape);
            localA?.onClick.RemoveListener(OnLocalA); localB?.onClick.RemoveListener(OnLocalB);
            if (startRoot != null) Destroy(startRoot);
            if (waitingRoot != null) Destroy(waitingRoot);
            JoinRequested = null; LocalRequested = null;
        }
    }
}
