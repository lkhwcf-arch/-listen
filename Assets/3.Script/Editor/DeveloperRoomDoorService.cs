#if UNITY_EDITOR
using System;
using System.Threading;
using System.Threading.Tasks;
using EscapeGame.World;

namespace EscapeGame.Editor
{
    // 서버·정답 판정을 우회하는 코드는 에디터에만 둡니다. 이미 생성 가능한 개방된 방의 문만 허가하며 다음 방 생성·클리어와 분리합니다.
    public sealed class DeveloperRoomDoorService : IRoomDoorService, IDisposable
    {
        private readonly DeveloperRoomProgression progression;
        private bool disposed;
        public DeveloperRoomDoorService(DeveloperRoomProgression progression)
        {
            this.progression = progression ?? throw new ArgumentNullException(nameof(progression));
        }
        public Task<bool> CheckRoomDoorAsync(int roomNo, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (disposed)
                throw new ObjectDisposedException(nameof(DeveloperRoomDoorService));
            if (roomNo < 1)
                throw new ArgumentOutOfRangeException(nameof(roomNo));
            return Task.FromResult(progression.IsUnlocked(progression.FindRoom(roomNo)));
        }
        public void Dispose()
        {
            disposed = true;
        }
    }
}
#endif
