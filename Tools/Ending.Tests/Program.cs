using EscapeGame.World;

int checks = 0;
void Check(bool valid, string message) { if (!valid) throw new Exception(message); checks++; }
void Reject(Action action, string message)
{
    bool rejected = false; try { action(); } catch (ArgumentOutOfRangeException) { rejected = true; }
    Check(rejected, message);
}
var video = new FakeVideo();
using (var flow = new EndingPlaybackController(video, 1000))
{
    int completed = 0; flow.Completed += () => { Check(flow.Phase == EndingPlaybackPhase.Completed, "완료 상태 반영 후 이벤트 발행"); completed++; };
    Check(flow.Start(100) && video.Prepares == 1 && flow.Phase == EndingPlaybackPhase.Preparing, "준비 후 재생 흐름 시작");
    Check(!flow.Start(200) && video.Prepares == 1, "준비 중 중복 시작 금지");
    video.Finish(); Check(completed == 0 && flow.Phase == EndingPlaybackPhase.Preparing, "재생 전 완료 이벤트 무시");
    flow.Tick(1099); Check(flow.Phase == EndingPlaybackPhase.Preparing, "제한 시간 전 대기");
    video.Ready(); Check(video.Plays == 1 && flow.Phase == EndingPlaybackPhase.Playing, "준비 완료 뒤 재생");
    video.Ready(); Check(video.Plays == 1, "중복 준비 이벤트 무시");
    flow.Tick(99999); Check(flow.Phase == EndingPlaybackPhase.Playing, "재생 중 준비 제한 시간 적용 안 함");
    video.Finish(); video.Finish(); video.Error("늦은 오류");
    Check(completed == 1 && flow.Phase == EndingPlaybackPhase.Completed && !flow.Start(100000), "완료 후 재실행·늦은 오류 무시");
}
Check(video.Disposed, "재생 포트 수명 정리");
video = new FakeVideo();
using (var flow = new EndingPlaybackController(video, 1000))
{
    flow.Start(0); flow.Tick(1000);
    Check(flow.Phase == EndingPlaybackPhase.Failed && flow.Error.Contains("초과"), "준비 시간 초과는 실패");
    video.Ready(); Check(video.Plays == 0, "시간 초과 뒤 늦은 준비 콜백 무시");
}
video = new FakeVideo();
using (var flow = new EndingPlaybackController(video, 1000))
{
    flow.Start(0); video.Ready(); video.Error("디코더 오류"); video.Finish();
    Check(flow.Phase == EndingPlaybackPhase.Failed && flow.Error == "디코더 오류", "재생 오류는 완료와 구분");
}
video = new FakeVideo { ThrowPrepare = true };
using (var flow = new EndingPlaybackController(video, 1000))
{ flow.Start(0); Check(flow.Phase == EndingPlaybackPhase.Failed, "동기 준비 예외도 실패 처리"); }
video = new FakeVideo { ThrowPlay = true };
using (var flow = new EndingPlaybackController(video, 1000))
{ flow.Start(0); video.Ready(); Check(flow.Phase == EndingPlaybackPhase.Failed, "동기 재생 예외도 실패 처리"); }
video = new FakeVideo();
var disposed = new EndingPlaybackController(video, 1000); disposed.Start(0); disposed.Dispose();
video.Ready(); video.Finish(); video.Error("late");
Check(video.Plays == 0 && video.SubscriberCount == 0, "정리 시 구독 제거·늦은 콜백 차단");
bool disposedRejected = false; try { disposed.Start(0); } catch (ObjectDisposedException) { disposedRejected = true; }
Check(disposedRejected, "정리한 컨트롤러 재사용 금지");
Reject(() => new EndingPlaybackController(new FakeVideo(), 0), "잘못된 준비 제한 시간 거부");
var rooms = new FakeRooms();
using (var ending = new EndingCompletionController(7, rooms, 1000))
{
    ending.Tick(0); rooms.Solve(1, false); ending.Tick(1000);
    Check(ending.Phase == EndingCompletionPhase.Playing && rooms.Requests == 0, "자기 마지막 정답 전에는 조회·엔딩 없음");
    rooms.Solve(7, false);
    Check(ending.Phase == EndingCompletionPhase.WaitingForPartner, "자기 정답만 맞히면 대기");
    var pending = new TaskCompletionSource<bool>(); rooms.Query = () => pending.Task;
    ending.Tick(1000); ending.Tick(10000);
    Check(rooms.Requests == 1, "조회 중 다음 프레임이 와도 요청 중복 없음");
    pending.SetResult(false);
    Check(ending.Phase == EndingCompletionPhase.WaitingForPartner && ending.LastError == null, "상대 미완료 false는 정상 대기");
    ending.Tick(10999); Check(rooms.Requests == 1, "응답 후 조회 간격 유지");
    rooms.Query = () => Task.FromResult(true); ending.Tick(11000);
    Check(ending.Phase == EndingCompletionPhase.Ready && rooms.Requests == 2, "상대 완료 확인 시 문 개폐 없이 엔딩 준비");
    rooms.Solve(7, true); ending.Tick(99999);
    Check(rooms.Requests == 2, "엔딩 준비 후 조회 종료·정답 중복 이벤트 무시");
}
Check(rooms.Subscribers == 0, "입장 수명 종료 시 정답 이벤트 구독 해제");
rooms = new FakeRooms();
using (var ending = new EndingCompletionController(7, rooms, 1000))
{
    rooms.Solve(7, true);
    Check(ending.Phase == EndingCompletionPhase.Ready && rooms.Requests == 0, "두 번째 정답 제출자는 서버 응답의 캐시로 즉시 준비");
}
rooms = new FakeRooms { Query = () => Task.FromException<bool>(new Exception("일시적 연결 오류")) };
using (var ending = new EndingCompletionController(7, rooms, 1000))
{
    rooms.Solve(7, false); ending.Tick(0);
    Check(ending.Phase == EndingCompletionPhase.WaitingForPartner && ending.LastError != null, "연결 실패를 정답 완료로 바꾸지 않음");
    rooms.Query = () => Task.FromResult(true); ending.Tick(999);
    Check(ending.Phase == EndingCompletionPhase.WaitingForPartner, "실패 후에도 조회 간격 유지");
    ending.Tick(1000);
    Check(ending.Phase == EndingCompletionPhase.Ready && ending.LastError == null, "연결 복구 후 완료 확인");
}
rooms = new FakeRooms();
var lateResult = new TaskCompletionSource<bool>(); rooms.Query = () => lateResult.Task;
var endingDisposed = new EndingCompletionController(7, rooms, 1000);
endingDisposed.Changed += () => { if (endingDisposed.Phase == EndingCompletionPhase.Ready) throw new Exception("늦은 완료 이벤트"); };
rooms.Solve(7, false); endingDisposed.Tick(0); endingDisposed.Dispose(); lateResult.SetResult(true);
Check(rooms.LastToken.IsCancellationRequested && rooms.Subscribers == 0 && endingDisposed.Phase != EndingCompletionPhase.Ready,
    "씬·플레이어 정리 뒤 진행 중 조회 취소 및 늦은 응답 무시");
foreach (bool aFirst in new[] { true, false })
{
    bool both = false;
    var firstService = new FakeRooms { Query = () => Task.FromResult(both) };
    var secondService = new FakeRooms();
    using var first = new EndingCompletionController(7, firstService, 1000);
    using var second = new EndingCompletionController(7, secondService, 1000);
    firstService.Solve(7, false); first.Tick(0);
    Check(first.Phase == EndingCompletionPhase.WaitingForPartner && second.Phase == EndingCompletionPhase.Playing,
        (aFirst ? "A" : "B") + " 선행 정답자는 대기하고 후행 플레이어는 계속 입력 가능");
    both = true; secondService.Solve(7, true); first.Tick(1000);
    Check(first.Phase == EndingCompletionPhase.Ready && second.Phase == EndingCompletionPhase.Ready,
        "후행 정답 확정 후 양쪽 엔딩 준비");
}
Reject(() => new EndingCompletionController(0, new FakeRooms(), 1000), "잘못된 마지막 방 거부");
Reject(() => new EndingCompletionController(7, new FakeRooms(), 0), "잘못된 조회 간격 거부");
Console.WriteLine($"엔딩 협동 완료·재생 수명 검사 {checks}개 통과");

sealed class FakeRooms : IRoomCompletionService
{
    public event Action<int> RoomSolved;
    public bool Permitted;
    public int Requests;
    public CancellationToken LastToken;
    public int Subscribers => RoomSolved?.GetInvocationList().Length ?? 0;
    public Func<Task<bool>> Query = () => Task.FromResult(false);
    public void Solve(int roomNo, bool permitted) { Permitted = permitted; RoomSolved?.Invoke(roomNo); }
    public bool HasDoorPermission(int roomNo) => Permitted;
    public Task<bool> CheckRoomDoorAsync(int roomNo, CancellationToken token)
    { Requests++; LastToken = token; return Query(); }
}

sealed class FakeVideo : IEndingVideoPlayback
{
    public event Action Prepared;
    public event Action Finished;
    public event Action<string> Failed;
    public int Prepares, Plays;
    public bool Disposed, ThrowPrepare, ThrowPlay;
    public int SubscriberCount => (Prepared?.GetInvocationList().Length ?? 0) + (Finished?.GetInvocationList().Length ?? 0) + (Failed?.GetInvocationList().Length ?? 0);
    public void Prepare() { Prepares++; if (ThrowPrepare) throw new Exception("prepare"); }
    public void Play() { Plays++; if (ThrowPlay) throw new Exception("play"); }
    public void Ready() => Prepared?.Invoke();
    public void Finish() => Finished?.Invoke();
    public void Error(string message) => Failed?.Invoke(message);
    public void Dispose() => Disposed = true;
}

