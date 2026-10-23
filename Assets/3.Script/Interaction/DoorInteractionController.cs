using System;
using System.Threading;
using System.Threading.Tasks;
using EscapeGame.World;

namespace EscapeGame.Interaction
{
    // 문 개폐 상태와 요청 수명입니다. 허가 후 개폐는 로컬 상태이며 애니메이션·HTTP와 분리합니다.
    public sealed class DoorInteractionController : IDisposable
    {
        private readonly IRoomDoorService service;
        private readonly int roomNo;
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private readonly CancellationToken owner;
        private bool disposed, allowed;
        public bool IsOpen
        {
            get;
            private set;
        }
        public bool IsBusy
        {
            get;
            private set;
        }
        public event Action Changed;
        public DoorInteractionController(int roomNo, IRoomDoorService service)
        {
            if (roomNo < 1)
                throw new ArgumentOutOfRangeException(nameof(roomNo));
            this.roomNo = roomNo;
            this.service = service ?? throw new ArgumentNullException(nameof(service));
            owner = lifetime.Token;
        }
        public async Task<bool> ToggleAsync(CancellationToken token = default)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(DoorInteractionController));
            token.ThrowIfCancellationRequested();
            if (IsBusy)
                return false;
            IsBusy = true;
            using var request = CancellationTokenSource.CreateLinkedTokenSource(owner, token);
            try
            {
                bool decision = allowed || await service.CheckRoomDoorAsync(roomNo, request.Token);
                request.Token.ThrowIfCancellationRequested();
                if (!decision || disposed)
                    return false;
                allowed = true;
                IsOpen = !IsOpen;
                Changed?.Invoke();
                return true;
            }
            finally
            {
                IsBusy = false;
            }
        }
        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            lifetime.Cancel();
            lifetime.Dispose();
            Changed = null;
        }
    }
}
