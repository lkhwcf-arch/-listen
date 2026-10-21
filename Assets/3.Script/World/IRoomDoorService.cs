using System.Threading;
using System.Threading.Tasks;

namespace EscapeGame.World
{
    // 문 조작은 허가 결과만 사용합니다. 서버 통신과 개발자 판정은 조립 지점에서 선택합니다.
    public interface IRoomDoorService
    {
        Task<bool> CheckRoomDoorAsync(int roomNo, CancellationToken token);
    }
}
