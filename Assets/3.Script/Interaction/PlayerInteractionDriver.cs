using System;
using EscapeGame.Player;
using System.Linq;
using EscapeGame.World;
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
        private IRoomAnswerService answers;
        private string role;
        private PuzzleAnswerController answerController;
        private PuzzleAnswerView answerView;
        public CloseupController Closeup { get; private set; }
        public WorldInteractionTarget Hovered { get; private set; }
        public bool IsReady => panel != null && finder != null && pointer != null && pointer.IsReady && isActiveAndEnabled;
        public void Initialize(Camera view, PlayerPointerDriver pointer, int distanceMm, InteractionPresentationSettings settings, IRoomAnswerService answers = null, string role = null)
        {
            if (panel != null) throw new InvalidOperationException("상호작용은 한 번만 초기화합니다.");
            this.view = view; this.pointer = pointer; this.settings = settings; this.distanceMm = distanceMm;
            this.answers = answers; this.role = role;
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
            ClearAnswer(); preview?.Dispose(); preview = null;
            if (!Closeup.IsOpen) { panel.Hide(); return; }
            var target = (WorldInteractionTarget)Closeup.Target;
            SetHovered(null);
            panel.SetNotice("");
            preview = new CloseupPreview(target, view, settings); panel.Preview.texture = preview.Texture; panel.Show(target.Anchor.RoomNo);
            if (!target.Features.Has(FeatureScripts.Dial) && !target.Features.Has(FeatureScripts.Console)) return;
            if (!target.Data.Puzzles.Any(row => row.Active && row.InputAnchor == target.Anchor.Id))
            { panel.SetNotice("입력 퍼즐이 연결되지 않은 미리보기 대상입니다. PUZZLE.InputAnchor를 확인해 주세요."); return; }
            if (answers == null) { panel.SetNotice("정답 요청 서비스가 연결되지 않았습니다."); return; }
            var puzzle = target.Data.Puzzles.Single(row => row.Active && row.InputAnchor == target.Anchor.Id);
            if (new RoomLoadPlan(target.Data, role).Find(puzzle.SolverRoom) == null)
            { panel.SetNotice("현재 역할에서는 이 퍼즐의 정답을 입력할 수 없습니다."); return; }
            var plan = PuzzleInputPlan.Create(target.Data, role, target.Anchor, target.Features);
            answerController = new PuzzleAnswerController(plan, answers, destroyCancellationToken, error => Debug.LogException(error));
            answerView = new PuzzleAnswerView(panel.ContentRoot, settings.Font, answerController);
        }
        private void ClearAnswer()
        {
            answerView?.Dispose(); answerView = null; answerController?.Dispose(); answerController = null;
        }
        private void OnDestroy()
        {
            if (Closeup != null) { Closeup.Changed -= OnCloseupChanged; if (panel != null) panel.CloseRequested -= Closeup.Close; Closeup.Dispose(); }
            ClearAnswer(); highlight?.Dispose(); preview?.Dispose(); panel?.Dispose();
        }
    }
}
