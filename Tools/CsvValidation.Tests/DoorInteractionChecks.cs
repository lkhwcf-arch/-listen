using EscapeGame.Interaction;
using EscapeGame.World;
internal static class DoorInteractionChecks
{
    public static async Task Run()
    {
int count = 0;
void Check(bool condition, string label) { if (!condition) throw new Exception(label); count++; }
var fake = new Fake();
using var door = new DoorInteractionController(7,fake);
int events = 0; door.Changed += () => events++;
Check(!await door.ToggleAsync() && !door.IsOpen && events == 0,"거절된 문은 닫힌 상태");
fake.Handler = _ => Task.FromResult(true);
Check(await door.ToggleAsync() && door.IsOpen && events == 1 && fake.Room == 7,"정수 방 번호로 허가 후 열기");
Check(await door.ToggleAsync() && !door.IsOpen && events == 2,"열린 문 닫기");
Check(await door.ToggleAsync() && door.IsOpen && events == 3 && fake.Calls == 2,"허가 후 개폐에 추가 판정 요청 없음");
var pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
var slow = new Fake { Handler = _ => pending.Task };
using var second = new DoorInteractionController(1,slow);
var request = second.ToggleAsync();
Check(second.IsBusy && !second.IsOpen,"허가 응답 전 열지 않음");
Check(!await second.ToggleAsync() && slow.Calls == 1,"요청 중 중복 클릭 제어");
pending.SetResult(true);
Check(await request && second.IsOpen && !second.IsBusy,"대기 요청 완료 후 개방");
var canceled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
var cancelFake = new Fake { Handler = _ => canceled.Task };
using var third = new DoorInteractionController(1,cancelFake);
using var cts = new CancellationTokenSource();
var cancelRequest = third.ToggleAsync(cts.Token); cts.Cancel(); canceled.SetResult(true);
try { await cancelRequest; throw new Exception("취소가 전달되지 않음"); } catch(OperationCanceledException) { count++; }
Check(!third.IsOpen && !third.IsBusy,"늦은 허가 응답은 취소한 문을 열지 않음");
cancelFake.Handler = _ => Task.FromResult(true);
Check(await third.ToggleAsync() && cancelFake.Calls == 2,"취소한 허가를 캐시하지 않고 재요청");
var dying = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
var last = new DoorInteractionController(1,new Fake { Handler = _ => dying.Task });
int lateEvents = 0; last.Changed += () => lateEvents++;
var dyingRequest = last.ToggleAsync(); last.Dispose(); dying.SetResult(true);
try { await dyingRequest; throw new Exception("종료 취소 누락"); } catch(OperationCanceledException) { count++; }
Check(!last.IsOpen && lateEvents == 0,"종료 후 늦은 이벤트와 문 개방 차단");
try { await last.ToggleAsync(); throw new Exception("종료 후 호출 허용"); } catch(ObjectDisposedException) { count++; }
using var broken = new DoorInteractionController(2,new Fake { Handler = _ => throw new InvalidOperationException("통신 실패") });
try { await broken.ToggleAsync(); throw new Exception("실패 누락"); } catch(InvalidOperationException) { count++; }
Check(!broken.IsBusy && !broken.IsOpen,"통신 실패 후 닫힘과 요청 잠금 해제");
try { new DoorInteractionController(0,fake); throw new Exception("방 번호 검증 누락"); } catch(ArgumentOutOfRangeException) { count++; }
using var pre = new CancellationTokenSource(); pre.Cancel();
int before = fake.Calls;
try { await door.ToggleAsync(pre.Token); throw new Exception("사전 취소 누락"); } catch(OperationCanceledException) { count++; }
Check(fake.Calls == before,"사전 취소 시 요청 없음");
Console.WriteLine($"문 상태·허가·중복·취소·수명 검사 {count}개 통과");

    }
    private sealed class Fake : IRoomDoorService
{
    public int Calls, Room;
    public Func<CancellationToken,Task<bool>> Handler = _ => Task.FromResult(false);
    public Task<bool> CheckRoomDoorAsync(int roomNo,CancellationToken token) { Calls++; Room=roomNo; return Handler(token); }
}

}
