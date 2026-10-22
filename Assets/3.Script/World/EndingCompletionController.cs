using System;
using System.Threading;
using System.Threading.Tasks;

namespace EscapeGame.World
{
    public enum EndingCompletionPhase { Playing, WaitingForPartner, Ready }

    // 자기 정답을 확인한 뒤에만 서버의 협동 완료 판정을 재조회합니다. 문 개폐와 무관합니다.
    public sealed class EndingCompletionController : IDisposable
    {
        private readonly int finalRoomNo, pollIntervalMs;
        private readonly IRoomCompletionService service;
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private readonly CancellationToken token;
        private long lastTick, nextPoll;
        private bool disposed, pending;
        public EndingCompletionPhase Phase { get; private set; }
        public string LastError { get; private set; }
        public event Action Changed;
        public EndingCompletionController(int finalRoomNo, IRoomCompletionService service, int pollIntervalMs)
        {
            if (finalRoomNo < 1 || pollIntervalMs < 1) throw new ArgumentOutOfRangeException(nameof(finalRoomNo));
            this.finalRoomNo = finalRoomNo; this.service = service ?? throw new ArgumentNullException(nameof(service));
            this.pollIntervalMs = pollIntervalMs; token = lifetime.Token; service.RoomSolved += OnSolved;
        }
        private void OnSolved(int roomNo)
        {
            if (disposed || roomNo != finalRoomNo || Phase != EndingCompletionPhase.Playing) return;
            Phase = service.HasDoorPermission(roomNo) ? EndingCompletionPhase.Ready : EndingCompletionPhase.WaitingForPartner;
            Changed?.Invoke();
        }
        public void Tick(long nowMs)
        {
            if (nowMs < 0) throw new ArgumentOutOfRangeException(nameof(nowMs));
            lastTick = Math.Max(lastTick, nowMs);
            if (disposed || Phase != EndingCompletionPhase.WaitingForPartner || pending || nowMs < nextPoll) return;
            pending = true;
            _ = CheckAsync();
        }
        private async Task CheckAsync()
        {
            try
            {
                bool allowed = await service.CheckRoomDoorAsync(finalRoomNo, token);
                if (disposed || token.IsCancellationRequested) return;
                bool changed = LastError != null || allowed;
                LastError = null;
                if (allowed) Phase = EndingCompletionPhase.Ready;
                if (changed) Changed?.Invoke();
            }
            catch (OperationCanceledException) when (disposed || token.IsCancellationRequested) { }
            catch (Exception error)
            {
                if (!disposed) { LastError = error.Message; Changed?.Invoke(); }
            }
            finally { pending = false; nextPoll = lastTick + pollIntervalMs; }
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; service.RoomSolved -= OnSolved;
            lifetime.Cancel(); lifetime.Dispose(); Changed = null;
        }
    }
}
