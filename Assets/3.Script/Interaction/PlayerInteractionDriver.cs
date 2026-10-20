using System;
using EscapeGame.Player;
using UnityEngine;

namespace EscapeGame.Interaction
{
    public sealed class PlayerInteractionDriver : MonoBehaviour
    {
        private PlayerPointerDriver pointer;
        private Camera view;
        private int distanceMm;
        private InteractionPresentationSettings settings;
        private InteractionTargetFinder finder;
        private CloseupPanelView panel;
        private CloseupPreview preview;
        private TargetHighlight highlight;
        public CloseupController Closeup { get; private set; }
        public WorldInteractionTarget Hovered { get; private set; }
        public bool IsReady => panel != null && finder != null && pointer != null && pointer.IsReady && isActiveAndEnabled;
        public void Initialize(Camera view, PlayerPointerDriver pointer, int distanceMm, InteractionPresentationSettings settings)
        {
            if (panel != null) throw new InvalidOperationException("상호작용은 한 번만 초기화합니다.");
            this.view = view; this.pointer = pointer; this.settings = settings; this.distanceMm = distanceMm;
            finder = new InteractionTargetFinder(transform);
            panel = new CloseupPanelView(gameObject.scene, settings);
            Closeup = new CloseupController(pointer.Controls.BlockForUi);
            Closeup.Changed += OnCloseupChanged; panel.CloseRequested += Closeup.Close;
            panel.SetActive(isActiveAndEnabled);
        }
        private void OnEnable() => panel?.SetActive(true);
        private void OnDisable() { Closeup?.Close(); SetHovered(null); panel?.SetActive(false); }
        private void LateUpdate()
        {
            if (!IsReady) return;
            Closeup.Refresh();
            if (Closeup.IsOpen) { if (pointer.CancelPressed) Closeup.Close(); return; }
            var target = pointer.Controls.GameplayEnabled ? finder.Find(view.ViewportPointToRay(new Vector3(.5f, .5f, 0)), distanceMm) : null;
            SetHovered(target); panel.SetHint(target != null && target.SupportsCloseup);
            if (pointer.PrimaryPressed && target != null && target.SupportsCloseup)
                try { Closeup.Open(target); } catch (Exception error) { Closeup.Close(); Debug.LogError("[클로즈업] " + error.Message); }
        }
        private void SetHovered(WorldInteractionTarget target)
        {
            if (Hovered == target) return;
            highlight?.Dispose(); highlight = null; Hovered = target;
            if (target != null) highlight = new TargetHighlight(target);
        }
        private void OnCloseupChanged()
        {
            preview?.Dispose(); preview = null;
            if (!Closeup.IsOpen) { panel.Hide(); return; }
            var target = (WorldInteractionTarget)Closeup.Target;
            SetHovered(null);
            preview = new CloseupPreview(target, view, settings); panel.Preview.texture = preview.Texture; panel.Show(target.Anchor.RoomNo);
        }
        private void OnDestroy()
        {
            if (Closeup != null) { Closeup.Changed -= OnCloseupChanged; if (panel != null) panel.CloseRequested -= Closeup.Close; Closeup.Dispose(); }
            highlight?.Dispose(); preview?.Dispose(); panel?.Dispose();
        }
    }
}
