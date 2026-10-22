using System;

namespace EscapeGame.World
{
    // 정답 확정 이벤트와 기존 서버의 BothSolved 판정을 엔딩에 제공합니다.
    public interface IRoomCompletionService : IRoomDoorService
    {
        event Action<int> RoomSolved;
        bool HasDoorPermission(int roomNo);
    }
}
