using EscapeGame.Audio;
using System;
using System.Threading;
using System.Threading.Tasks;
using EscapeGame.Player;
using EscapeGame.Data.Tables;
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
        private IRoomDoorService doors;
        private string doorNotice;
        private bool interacting;
        private string role;
        private PuzzleAnswerController answerController;
        private PuzzleAnswerView answerView;
        private AnchorData feedbackTarget;
        private AnswerInputPhase lastAnswerPhase;
        public event Action<GameAudioCue, EscapeGame.Data.Tables.AnchorData> FeedbackEmitted;
        public CloseupController Closeup
        {
            get;
            private set;
        }
        public WorldInteractionTarget Hovered
        {
            get;
            private set;
        }
        public bool IsReady => panel != null && panel.IsAvailable && finder != null && pointer != null && pointer.IsReady && isActiveAndEnabled;
        public void Initialize(Camera view, PlayerPointerDriver pointer, int distanceMm, InteractionPresentationSettings settings, IRoomAnswerService answers = null, string role = null, IRoomDoorService doors = null)
        {
            if (panel != null)
                throw new InvalidOperationException("상호작용은 한 번만 초기화합니다.");
            this.view = view;
            this.pointer = pointer;
            this.settings = settings;
            this.distanceMm = distanceMm;
            this.answers = answers;
            this.role = role;
            this.doors = doors ?? answers as IRoomDoorService;
            finder = new InteractionTargetFinder(transform);
            panel = new CloseupPanelView(gameObject.scene, settings);
            Closeup = new CloseupController(pointer.Controls.BlockForUi);
            Closeup.Changed += OnCloseupChanged;
            panel.CloseRequested += Closeup.Close;
            panel.SetActive(isActiveAndEnabled);
        }
        private void OnEnable() => panel?.SetActive(true);
        private void OnDisable()
        {
            Closeup?.Close();
            SetHovered(null);
            panel?.SetActive(false);
        }
        private void LateUpdate()
        {
            if (!IsReady)
                return;
            Closeup.Refresh();
            if (Closeup.IsOpen)
            {
                if (pointer.CancelPressed)
                    Closeup.Close();
                return;
            }
            var target = pointer.Controls.GameplayEnabled ? finder.Find(view.ViewportPointToRay(new Vector3(.5f, .5f, 0)), distanceMm) : null;
            SetHovered(target);
            var door = target != null ? target.GetComponent<WorldDoorView>() : null;
            panel.SetHint(door != null ? doorNotice ?? (interacting ? "문 허가 확인 중" : door.IsOpen ? "좌클릭으로 문 닫기" : "좌클릭으로 문 열기") : target != null && target.SupportsCloseup ? "좌클릭으로 살펴보기" : "");
            if (pointer.PrimaryPressed && target != null)
                HandlePrimary(target);
        }
        private async void HandlePrimary(WorldInteractionTarget target)
        {
            try
            {
                await InteractAsync(target, destroyCancellationToken);
            }
            catch (OperationCanceledException) { }
            catch (Exception error)
            {
                if (this != null)
                {
                    doorNotice = "문을 조작하지 못했습니다. 연결 상태와 문 판정 설정을 확인해 주세요.";
                    Debug.LogWarning("[상호작용] " + error.Message);
                }
            }
        }
        // 실제 좌클릭과 검사가 사용하는 같은 명령 경로입니다. UI·요청 중복과 조작 잠금을 공유합니다.
        public async Task<bool> InteractAsync(WorldInteractionTarget target, CancellationToken token = default)
        {
            if (interacting || !IsReady || !pointer.Controls.GameplayEnabled || Closeup.IsOpen || target == null || !target.IsAvailable)
                return false;
            token.ThrowIfCancellationRequested();
            var door = target.GetComponent<WorldDoorView>();
            if (door == null)
            {
                if (!target.SupportsCloseup)
                    return false;
                Closeup.Open(target);
                return true;
            }
            interacting = true;
            doorNotice = null;
            try
            {
                bool changed = await door.ToggleAsync(doors, settings.DoorOpenAngleMilliDegrees, token);
                if (this != null)
                    FeedbackEmitted?.Invoke(changed ? (door.IsOpen ? GameAudioCue.DoorOpen : GameAudioCue.DoorClose) : GameAudioCue.DoorLocked, target.Anchor);
                if (this != null && !changed)
                    doorNotice = "문 열기 조건이 충족되지 않았습니다. 방 클리어 상태를 확인해 주세요.";
                return changed;
            }
            finally
            {
                interacting = false;
            }
        }
        private void SetHovered(WorldInteractionTarget target)
        {
            if (Hovered == target)
                return;
            doorNotice = null;
            highlight?.Dispose();
            highlight = null;
            Hovered = target;
            if (target != null)
                highlight = new TargetHighlight(target);
        }
        private void OnCloseupChanged()
        {
            ClearAnswer();
            preview?.Dispose();
            preview = null;
            if (!Closeup.IsOpen)
            {
                panel.Hide();
                if (feedbackTarget != null)
                {
                    FeedbackEmitted?.Invoke(GameAudioCue.CloseupClose, feedbackTarget);
                    FeedbackEmitted?.Invoke(GameAudioCue.ClickClose, feedbackTarget);
                    feedbackTarget = null;
                }
                return;
            }
            var target = (WorldInteractionTarget)Closeup.Target;
            feedbackTarget = target.Anchor;
            FeedbackEmitted?.Invoke(GameAudioCue.Click, feedbackTarget);
            FeedbackEmitted?.Invoke(GameAudioCue.CloseupOpen, feedbackTarget);
            SetHovered(null);
            panel.SetNotice("");
            preview = new CloseupPreview(target, view, settings);
            panel.Preview.texture = preview.Texture;
            panel.Show(target.Anchor.RoomNo);
            if (!target.Features.Has(FeatureScripts.Dial) && !target.Features.Has(FeatureScripts.Console))
                return;
            if (!target.Data.Puzzles.Any(row => row.Active && row.InputAnchor == target.Anchor.Id))
            {
                panel.SetNotice("입력 퍼즐이 연결되지 않은 미리보기 대상입니다. PUZZLE.InputAnchor를 확인해 주세요.");
                return;
            }
            if (answers == null)
            {
                panel.SetNotice("정답 요청 서비스가 연결되지 않았습니다.");
                return;
            }
            var puzzle = target.Data.Puzzles.Single(row => row.Active && row.InputAnchor == target.Anchor.Id);
            if (new RoomLoadPlan(target.Data, role).Find(puzzle.SolverRoom) == null)
            {
                panel.SetNotice("현재 역할에서는 이 퍼즐의 정답을 입력할 수 없습니다.");
                return;
            }
            var plan = PuzzleInputPlan.Create(target.Data, role, target.Anchor, target.Features);
            answerController = new PuzzleAnswerController(plan, answers, destroyCancellationToken, error => Debug.LogException(error));
            lastAnswerPhase = answerController.Phase;
            answerController.Changed += OnAnswerChanged;
            answerView = new PuzzleAnswerView(panel.ContentRoot, settings.Font, answerController, trigger => FeedbackEmitted?.Invoke(trigger, target.Anchor));
        }
        private void OnAnswerChanged()
        {
            if (answerController == null || answerController.Phase == lastAnswerPhase)
                return;
            lastAnswerPhase = answerController.Phase;
            if (lastAnswerPhase == AnswerInputPhase.Correct || lastAnswerPhase == AnswerInputPhase.Incorrect)
                FeedbackEmitted?.Invoke(lastAnswerPhase == AnswerInputPhase.Correct ? GameAudioCue.Correct : GameAudioCue.Wrong, feedbackTarget);
        }
        private void ClearAnswer()
        {
            answerView?.Dispose();
            answerView = null;
            if (answerController != null)
                answerController.Changed -= OnAnswerChanged;
            answerController?.Dispose();
            answerController = null;
        }
        private void OnDestroy()
        {
            if (Closeup != null)
            {
                Closeup.Changed -= OnCloseupChanged;
                if (panel != null)
                    panel.CloseRequested -= Closeup.Close;
                Closeup.Dispose();
            }
            ClearAnswer();
            highlight?.Dispose();
            preview?.Dispose();
            panel?.Dispose();
            FeedbackEmitted = null;
        }
    }
}
