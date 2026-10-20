using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EscapeGame.Session.Contracts;
using EscapeGame.World;

namespace EscapeGame.Interaction
{
    public enum AnswerInputPhase { Editing, Submitting, Correct, Incorrect, Failed, Cancelled }

    // 입력 상태와 요청 수명을 소유합니다. UI·Unity·HTTP와 분리합니다.
    public sealed class PuzzleAnswerController : IDisposable
    {
        private readonly IRoomAnswerService service;
        private readonly Action<Exception> diagnostic;
        private readonly CancellationTokenSource lifetime;
        private readonly string[] values;
        private bool disposed;
        public PuzzleInputPlan Plan { get; }
        public IReadOnlyList<string> Values { get; }
        public int SelectedSlot { get; private set; }
        public AnswerInputPhase Phase { get; private set; }
        public SessionFailureCode Failure { get; private set; }
        public bool CanEdit => !disposed && Phase != AnswerInputPhase.Submitting && Phase != AnswerInputPhase.Correct;
        public bool CanSubmit => CanEdit && values.All(value => !string.IsNullOrEmpty(value));
        public event Action Changed;
        public PuzzleAnswerController(PuzzleInputPlan plan, IRoomAnswerService service, CancellationToken owner = default, Action<Exception> diagnostic = null)
        {
            Plan = plan ?? throw new ArgumentNullException(nameof(plan));
            this.service = service ?? throw new ArgumentNullException(nameof(service)); this.diagnostic = diagnostic;
            lifetime = CancellationTokenSource.CreateLinkedTokenSource(owner);
            values = new string[plan.SlotCount]; Values = Array.AsReadOnly(values); Reset();
        }
        public void SelectSlot(int slot)
        {
            if (!CanEdit) return; ValidateSlot(slot); SelectedSlot = slot; Changed?.Invoke();
        }
        public void Cycle(int slot, int direction)
        {
            if (!CanEdit) return; ValidateSlot(slot);
            if (direction != -1 && direction != 1) throw new ArgumentOutOfRangeException(nameof(direction));
            int index = Array.IndexOf(Plan.Choices.ToArray(), values[slot]);
            values[slot] = Plan.Choices[(index + direction + Plan.Choices.Count) % Plan.Choices.Count];
            SelectedSlot = slot; MarkEdited();
        }
        public void Choose(string value)
        {
            if (!CanEdit) return;
            if (!Plan.Choices.Contains(value)) throw new ArgumentException("선언되지 않은 입력값입니다.", nameof(value));
            values[SelectedSlot] = value;
            if (Plan.Kind == PuzzleInputKind.Sequence) SelectedSlot = (SelectedSlot + 1) % values.Length;
            MarkEdited();
        }
        public void Reset()
        {
            if (!CanEdit) return;
            for (int i = 0; i < values.Length; i++) values[i] = Plan.Kind == PuzzleInputKind.Dial ? Plan.Choices[0] : "";
            SelectedSlot = 0; MarkEdited();
        }
        private void MarkEdited() { Phase = AnswerInputPhase.Editing; Failure = SessionFailureCode.None; Changed?.Invoke(); }
        private void ValidateSlot(int slot) { if (slot < 0 || slot >= values.Length) throw new ArgumentOutOfRangeException(nameof(slot)); }
        public async Task SubmitAsync()
        {
            if (!CanSubmit) return;
            var token = lifetime.Token;
            Phase = AnswerInputPhase.Submitting; Failure = SessionFailureCode.None; Changed?.Invoke();
            try
            {
                token.ThrowIfCancellationRequested();
                var result = await service.SubmitAnswerAsync(Plan.RoomNo, (string[])values.Clone(), token);
                token.ThrowIfCancellationRequested();
                if (disposed) return;
                if (result == null) { Phase = AnswerInputPhase.Failed; Failure = SessionFailureCode.InvalidResponse; }
                else if (!result.IsSuccess) { Phase = AnswerInputPhase.Failed; Failure = result.Failure; }
                else Phase = result.Correct ? AnswerInputPhase.Correct : AnswerInputPhase.Incorrect;
            }
            catch (OperationCanceledException) { if (!disposed) Phase = AnswerInputPhase.Cancelled; }
            catch (Exception error)
            {
                if (disposed) return;
                Phase = AnswerInputPhase.Failed; Failure = SessionFailureCode.UnexpectedFailure; diagnostic?.Invoke(error);
            }
            if (!disposed) Changed?.Invoke();
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            lifetime.Cancel(); lifetime.Dispose(); Changed = null;
        }
    }
}
