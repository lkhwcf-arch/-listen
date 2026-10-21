using EscapeGame.Session.Contracts;
using EscapeGame.World;

static class SessionStartChecks
{
    internal static async Task Run()
    {
        int count = 0;
        void Check(bool value, string message) { if (!value) throw new Exception(message); count++; }
        var session = new FakeSession();
        using var gate = new SessionStartGate(session);
        int events = 0; gate.Changed += () => events++;
        session.Publish(ClientSessionPhase.Waiting, 1);
        var waiting = gate.WaitForParticipantsAsync(default);
        Check(!waiting.IsCompleted && !gate.CanEnterScene && !gate.CanPlay, "한 명이면 씬 진입과 조작을 대기합니다.");
        int before = events;
        session.Publish(ClientSessionPhase.Waiting, 1);
        Check(events == before, "동일 상태 재조회는 변경 이벤트를 반복하지 않습니다.");
        session.Publish(ClientSessionPhase.Loading, 2);
        await waiting;
        Check(gate.CanEnterScene && !gate.CanPlay, "두 명이 모이면 씬 로드만 허용합니다.");
        session.Publish(ClientSessionPhase.Loading, 2, true);
        Check(!gate.CanPlay, "자신의 준비 보고만으로 조작을 허용하지 않습니다.");
        session.Publish(ClientSessionPhase.Playing, 2, true);
        Check(gate.CanPlay, "서버 Playing 확인 후 조작을 허용합니다.");
        session.Publish(ClientSessionPhase.Waiting, 1, true);
        Check(!gate.CanPlay && !gate.CanEnterScene, "상대 퇴장 시 다시 잠급니다.");
        using (var cancellation = new CancellationTokenSource())
        {
            var cancelled = gate.WaitForParticipantsAsync(cancellation.Token);
            cancellation.Cancel();
            try { await cancelled; throw new Exception("취소 실패"); } catch (OperationCanceledException) { count++; }
        }
        var disconnected = gate.WaitForParticipantsAsync(default);
        session.Disconnect(SessionFailureCode.ConnectionFailed);
        try { await disconnected; throw new Exception("연결 종료 실패"); } catch (InvalidOperationException) { count++; }
        Check(!gate.CanPlay && gate.Message.Contains("연결"), "연결 종료는 조작을 잠그고 원인을 알립니다.");
        gate.SetLocalMode(true);
        await gate.WaitForParticipantsAsync(default);
        Check(gate.CanPlay && gate.CanEnterScene, "로컬 테스트는 서버 없이 즉시 허용합니다.");
        gate.SetLocalMode(false);
        session.Publish(ClientSessionPhase.Waiting, 1);
        var abandoned = gate.WaitForParticipantsAsync(default);
        gate.Dispose();
        try { await abandoned; throw new Exception("수명 종료 실패"); } catch (OperationCanceledException) { count++; }
        Check(session.Subscribers == 0, "종료 시 상태 구독을 해제합니다.");
        Console.WriteLine($"게임 시작 조건 검사 {count}개 통과.");
    }
    private sealed class FakeSession : ISessionConnectionService
    {
        private Action<SessionInfo> changed;
        public int Subscribers => changed?.GetInvocationList().Length ?? 0;
        public SessionInfo State { get; private set; } = new SessionInfo(null, null, null, null, 0, false);
        public bool IsBusy => false;
        public string ServerBaseUrl => "http://test";
        public event Action<SessionInfo> StateChanged { add { changed += value; } remove { changed -= value; } }
        public event Action<ClientSessionPhase?, ClientSessionPhase> PhaseChanged { add { } remove { } }
        public event Action<SessionFailureCode> BackgroundFailed { add { } remove { } }
        public event Action<SessionFailureCode> Disconnected { add { } remove { } }
        public void Publish(ClientSessionPhase phase, int count, bool ready = false)
        { State = new SessionInfo("s", "p", "A", phase, count, ready); changed?.Invoke(State); }
        public void Disconnect(SessionFailureCode reason)
        { State = new SessionInfo("s", "p", "A", null, 0, false, SessionConnectionStatus.Disconnected, reason); changed?.Invoke(State); }
        public Task<SessionCommandResult> LeaveAsync(CancellationToken token) => throw new NotSupportedException();
        public Task<SessionCommandResult> HeartbeatAsync(CancellationToken token) => throw new NotSupportedException();
        public Task<SessionCommandResult> JoinAsync(string address, CancellationToken token) => throw new NotSupportedException();
        public Task<SessionCommandResult> ReportSceneLoadedAsync(CancellationToken token) => throw new NotSupportedException();
        public Task<SessionCommandResult> QueryAsync(CancellationToken token) => throw new NotSupportedException();
    }
}
