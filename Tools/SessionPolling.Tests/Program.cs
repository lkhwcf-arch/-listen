using EscapeGame.Session;
using EscapeGame.Session.Contracts;
using EscapeGame.Timing;

var clock = new FakeClock();
var service = new FakeService();
var controller = new SessionPollingController(service,
    new MillisecondInterval(clock, 1000), new MillisecondInterval(clock, 5000));
int checks = 0;
void Tick(long now) { clock.ElapsedMilliseconds = now; controller.Tick(CancellationToken.None); }
void Expect(int expected, string reason)
{
    if (service.Count != expected) throw new Exception($"{reason}: {service.Count} != {expected}");
    checks++;
}

Tick(0); Expect(0, "미접속은 조회하지 않음");
service.Set("one", ClientSessionPhase.Waiting);
Tick(0); Expect(1, "입장 직후 최초 조회");
Tick(999); Expect(1, "대기 1초 전");
Tick(1000); Expect(2, "대기 1초 도달");
service.Set("one", ClientSessionPhase.Loading);
Tick(2000); Expect(3, "준비 중에도 1초");
service.Set("one", ClientSessionPhase.Playing);
Tick(2001); Expect(3, "게임 전환 직후 중복 조회 없음");
Tick(7000); Expect(3, "게임 전환 5초 전");
Tick(7001); Expect(4, "게임 전환 5초 도달");
service.Set("one", ClientSessionPhase.Playing);
Tick(12001); Expect(5, "같은 상태 응답은 주기를 초기화하지 않음");
service.IsBusy = true;
Tick(17001); Expect(5, "다른 요청 수행 중 조회 보류");
service.IsBusy = false;
Tick(17002); Expect(6, "요청 완료 후 도달한 주기 수행");
Tick(90000); Expect(7, "프레임 지연 후 누락 요청 몰아 보내지 않음");
Tick(90000); Expect(7, "같은 프레임 중복 조회 없음");
service.Set("one", ClientSessionPhase.Waiting);
Tick(90001); Tick(91000); Expect(7, "대기 복귀 1초 전");
Tick(91001); Expect(8, "대기 복귀 후 1초 조회");
service.Set("two", ClientSessionPhase.Playing);
Tick(91002); Expect(9, "재입장 최초 조회");
Tick(96001); Expect(9, "재입장 5초 전");
Tick(96002); Expect(10, "재입장 후 5초 조회");
controller.Tick(new CancellationToken(true)); Expect(10, "취소한 수명은 조회하지 않음");
Console.WriteLine($"조회 간격 검사 {checks}개 통과");

sealed class FakeClock : IMillisecondClock { public long ElapsedMilliseconds { get; set; } }
sealed class FakeService : ISessionConnectionService
{
    public SessionInfo State { get; private set; } = new SessionInfo(null, null, null, null, 0, false);
    public bool IsBusy { get; set; }
    public string ServerBaseUrl => "http://127.0.0.1";
    public int Count { get; private set; }
    public event Action<SessionInfo> StateChanged { add { } remove { } }
    public event Action<ClientSessionPhase?, ClientSessionPhase> PhaseChanged { add { } remove { } }
    public event Action<SessionFailureCode> BackgroundFailed { add { } remove { } }
    public event Action<SessionFailureCode> Disconnected { add { } remove { } }
    public void Set(string player, ClientSessionPhase phase) => State = new SessionInfo("session", player, "A", phase, 2, true);
    public Task<SessionCommandResult> QueryAsync(CancellationToken token)
    { Count++; return Task.FromResult(SessionCommandResult.Completed()); }
    public Task<SessionCommandResult> LeaveAsync(CancellationToken token) => throw new NotSupportedException();
    public Task<SessionCommandResult> HeartbeatAsync(CancellationToken token) => throw new NotSupportedException();
    public void Disconnect(SessionFailureCode reason) => throw new NotSupportedException();
    public Task<SessionCommandResult> JoinAsync(string address, CancellationToken token) => throw new NotSupportedException();
    public Task<SessionCommandResult> ReportSceneLoadedAsync(CancellationToken token) => throw new NotSupportedException();
}
