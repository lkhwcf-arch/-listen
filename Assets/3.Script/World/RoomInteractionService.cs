using System;
using System.Threading;
using System.Threading.Tasks;
using EscapeGame.Session;
using EscapeGame.Session.Contracts;

namespace EscapeGame.World
{
    // 입장/씬 전환과 독립적으로 방 명령·허가 캐시·완료 통지를 소유합니다.
    public sealed class RoomInteractionService : IRoomAnswerService, IRoomCompletionService, IRoomCompletionConnection, IDisposable
    {
        private readonly CancellationToken owner;
        private bool disposed;
        public RoomPermissionService Permissions { get; }
        public bool IsAvailable { get; private set; }
        public event Action<int> RoomSolved;
        public event Action AvailabilityChanged;
        public RoomInteractionService(Func<int, string[], CancellationToken, Task<RoomDecision>> request, CancellationToken owner)
        { Permissions = new RoomPermissionService(request); this.owner = owner; }
        public void SetAvailable(bool value)
        {
            if (IsAvailable == value) return;
            IsAvailable = value;
            if (!value) Permissions.Reset();
            AvailabilityChanged?.Invoke();
        }
        public void Reset() { SetAvailable(false); Permissions.Reset(); }
        public bool HasDoorPermission(int roomNo) => Permissions.HasPermission(roomNo);
        internal void PublishSolved(int roomNo) { if (!disposed && IsAvailable) RoomSolved?.Invoke(roomNo); }
        public async Task<bool> SubmitRoomAnswerAsync(int roomNo, string[] answers, CancellationToken token)
        {
            if (disposed) throw new ObjectDisposedException(nameof(RoomInteractionService));
            using var request = CancellationTokenSource.CreateLinkedTokenSource(token, owner);
            bool correct = await Permissions.SubmitAnswerAsync(roomNo, answers, request.Token);
            request.Token.ThrowIfCancellationRequested();
            if (correct) PublishSolved(roomNo);
            return correct;
        }
        public async Task<bool> CheckRoomDoorAsync(int roomNo, CancellationToken token)
        {
            if (disposed) throw new ObjectDisposedException(nameof(RoomInteractionService));
            using var request = CancellationTokenSource.CreateLinkedTokenSource(token, owner);
            return await Permissions.CheckDoorAsync(roomNo, request.Token);
        }
        async Task<RoomAnswerResult> IRoomAnswerService.SubmitAnswerAsync(int roomNo, string[] answers, CancellationToken token)
        {
            try { return RoomAnswerResult.Decision(await SubmitRoomAnswerAsync(roomNo, answers, token)); }
            catch (SessionResultException error) { return RoomAnswerResult.Failed(error.Code); }
        }
        public void Dispose() { if (disposed) return; disposed = true; Reset(); RoomSolved = null; AvailabilityChanged = null; }
    }
}
