using System;

namespace EscapeGame.World
{
    // 완료 판정과 연결 가용성은 독립 계약입니다. 로직 검사의 가짜 판정기는 연결 구현이 필요 없습니다.
    public interface IRoomCompletionConnection
    {
        bool IsAvailable
        {
            get;
        }
        event Action AvailabilityChanged;
    }
}
