using System;

namespace EscapeGame.World
{
    public enum EndingPlaybackPhase { Idle, Preparing, Playing, Completed, Failed }
    public interface IEndingVideoPlayback : IDisposable
    {
        event Action Prepared;
        event Action Finished;
        event Action<string> Failed;
        void Prepare();
        void Play();
    }

    // 상태·준비 제한 시간은 VideoPlayer 및 UI와 분리합니다.
    public sealed class EndingPlaybackController : IDisposable
    {
        private readonly IEndingVideoPlayback playback;
        private readonly int prepareTimeoutMs;
        private long prepareStarted;
        private bool disposed;
        public EndingPlaybackPhase Phase { get; private set; }
        public string Error { get; private set; }
        public event Action Changed;
        public event Action Completed;
        public EndingPlaybackController(IEndingVideoPlayback playback, int prepareTimeoutMs)
        {
            this.playback = playback ?? throw new ArgumentNullException(nameof(playback));
            if (prepareTimeoutMs < 1) throw new ArgumentOutOfRangeException(nameof(prepareTimeoutMs));
            this.prepareTimeoutMs = prepareTimeoutMs;
            playback.Prepared += OnPrepared; playback.Finished += OnFinished; playback.Failed += OnFailed;
        }
        public bool Start(long nowMs)
        {
            if (disposed) throw new ObjectDisposedException(nameof(EndingPlaybackController));
            if (nowMs < 0) throw new ArgumentOutOfRangeException(nameof(nowMs));
            if (Phase != EndingPlaybackPhase.Idle) return false;
            prepareStarted = nowMs; Phase = EndingPlaybackPhase.Preparing; Changed?.Invoke();
            try { playback.Prepare(); } catch (Exception error) { OnFailed(error.Message); }
            return true;
        }
        public void Tick(long nowMs)
        {
            if (!disposed && Phase == EndingPlaybackPhase.Preparing && nowMs >= prepareStarted && nowMs - prepareStarted >= prepareTimeoutMs)
                OnFailed("엔딩 영상 준비 시간이 초과되었습니다.");
        }
        private void OnPrepared()
        {
            if (disposed || Phase != EndingPlaybackPhase.Preparing) return;
            Phase = EndingPlaybackPhase.Playing; Changed?.Invoke();
            try { playback.Play(); } catch (Exception error) { OnFailed(error.Message); }
        }
        private void OnFinished()
        {
            if (disposed || Phase != EndingPlaybackPhase.Playing) return;
            Phase = EndingPlaybackPhase.Completed; Changed?.Invoke(); Completed?.Invoke();
        }
        private void OnFailed(string message)
        {
            if (disposed || (Phase != EndingPlaybackPhase.Preparing && Phase != EndingPlaybackPhase.Playing)) return;
            Error = message; Phase = EndingPlaybackPhase.Failed; Changed?.Invoke();
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            playback.Prepared -= OnPrepared; playback.Finished -= OnFinished; playback.Failed -= OnFailed;
            Changed = null; Completed = null; playback.Dispose();
        }
    }
}
