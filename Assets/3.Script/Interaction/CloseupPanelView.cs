using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace EscapeGame.Interaction
{
    public sealed class CloseupPanelView : IDisposable
    {
        private readonly GameObject root, overlay, ownedEventSystem;
        private readonly TMP_Text title, hint, notice;
        private readonly Button back;
        private bool disposed;
        public RawImage Preview { get; }
        public RectTransform ContentRoot { get; }
        public bool IsAvailable => !disposed && root != null && overlay != null;
        public bool IsVisible => IsAvailable && overlay.activeSelf;
        public event Action CloseRequested;
        public CloseupPanelView(Scene scene, InteractionPresentationSettings settings)
        {
            settings.Validate();
            root = new GameObject("InteractionCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(root, scene);
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 1000;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080);
            hint = InteractionUiFactory.Text("TargetHint", root.transform, settings.Font, "", 600, 60, 0, -300, 25);
            InteractionUiFactory.Text("Reticle", root.transform, settings.Font, "·", 32, 32, 0, 0, 34);
            overlay = new GameObject("CloseupOverlay", typeof(RectTransform), typeof(Image)); overlay.transform.SetParent(root.transform, false);
            var cover = (RectTransform)overlay.transform; cover.anchorMin = Vector2.zero; cover.anchorMax = Vector2.one; cover.offsetMin = cover.offsetMax = Vector2.zero;
            overlay.GetComponent<Image>().color = new Color(0, 0, 0, .8f);
            var panel = InteractionUiFactory.Rect("CloseupPanel", cover, 1120, 700, 0, 0); panel.gameObject.AddComponent<Image>().color = new Color(.09f, .12f, .16f);
            title = InteractionUiFactory.Text("Title", panel, settings.Font, "조작 화면", 1000, 64, 0, 282, 32);
            var previewRect = InteractionUiFactory.Rect("ObjectPreview", panel, 480, 480, -240, 5); Preview = previewRect.gameObject.AddComponent<RawImage>(); Preview.raycastTarget = false;
            ContentRoot = InteractionUiFactory.Rect("Controls", panel, 420, 480, 300, 5);
            notice = InteractionUiFactory.Text("InputNotice", ContentRoot, settings.Font, "", 400, 240, 0, 0, 22);
            back = InteractionUiFactory.Button("Back", panel, settings.Font, "돌아가기", 240, 60, 0, -292); back.onClick.AddListener(OnBack);
            overlay.SetActive(false);
            if (EventSystem.current == null)
            {
                ownedEventSystem = new GameObject("InteractionEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                SceneManager.MoveGameObjectToScene(ownedEventSystem, scene);
                ownedEventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
        }
        private void OnBack() { if (IsAvailable) CloseRequested?.Invoke(); }
        public void Show(int roomNo)
        {
            if (!IsAvailable) return;
            if (title != null) title.text = roomNo + "번 방 · 조작 화면";
            if (hint != null) hint.text = "";
            overlay.SetActive(true);
        }
        public void Hide()
        {
            if (disposed) return;
            if (Preview != null) Preview.texture = null;
            if (notice != null) notice.text = "";
            if (overlay != null) overlay.SetActive(false);
        }
        public void SetNotice(string message) { if (!disposed && notice != null) notice.text = message; }
        public void SetHint(bool available) => SetHint(available ? "좌클릭으로 살펴보기" : "");
        public void SetHint(string message) { if (!disposed && hint != null) hint.text = message; }
        public void SetActive(bool active) { if (!disposed && root != null) root.SetActive(active); }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            CloseRequested = null;
            // 씬 해제는 Canvas를 플레이어보다 먼저 파괴할 수 있습니다. Unity의 null 비교로 확인합니다.
            if (back != null) back.onClick.RemoveListener(OnBack);
            if (Preview != null) Preview.texture = null;
            if (root != null) { root.SetActive(false); InteractionObjectLifetime.Destroy(root); }
            if (ownedEventSystem != null) { ownedEventSystem.SetActive(false); InteractionObjectLifetime.Destroy(ownedEventSystem); }
        }
    }
}
