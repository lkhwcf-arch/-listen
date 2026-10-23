using System;

namespace EscapeGame.Interaction
{
    public interface ICloseupTarget
    {
        bool IsAvailable
        {
            get;
        }
        bool SupportsCloseup
        {
            get;
        }
    }
    // UI 상태와 잠금 토큰을 소유합니다. Unity/HTTP/카메라 참조는 없습니다.
    public sealed class CloseupController : IDisposable
    {
        private readonly Func<IDisposable> acquireLock;
        private IDisposable uiLock;
        private bool disposed;
        public ICloseupTarget Target
        {
            get;
            private set;
        }
        public bool IsOpen => Target != null;
        public event Action Changed;
        public CloseupController(Func<IDisposable> acquireLock) => this.acquireLock = acquireLock ?? throw new ArgumentNullException(nameof(acquireLock));
        public bool Open(ICloseupTarget target)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(CloseupController));
            if (IsOpen || target == null || !target.IsAvailable || !target.SupportsCloseup)
                return false;
            uiLock = acquireLock() ?? throw new InvalidOperationException("UI 조작 잠금 토큰이 필요합니다.");
            Target = target;
            Changed?.Invoke();
            return true;
        }
        public void Close()
        {
            if (!IsOpen)
                return;
            Target = null;
            var owned = uiLock;
            uiLock = null;
            owned?.Dispose();
            Changed?.Invoke();
        }
        public void Refresh()
        {
            if (IsOpen && !Target.IsAvailable)
                Close();
        }
        public void Dispose()
        {
            if (disposed)
                return;
            Close();
            disposed = true;
        }
    }
}
