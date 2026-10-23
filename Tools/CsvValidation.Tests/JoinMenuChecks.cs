using EscapeGame.Configuration;
using EscapeGame.Data;
using EscapeGame.Data.CsvValidation.Runtime;
using EscapeGame.Session.Contracts;
using EscapeGame.Session.Presentation;
using EscapeGame.World;

static class JoinMenuChecks
{
    internal static async Task Run(Dictionary<string, string> original, string xml)
    {
        int count = 0;
        void Check(bool valid, string message)
        {
            if (!valid)
                throw new Exception(message);
            count++;
        }
        var fixture = new TestValidator(xml);
        var data = fixture.Input(original);
        var validator = new CsvValidator(fixture.Catalog);
        var settings = ClientConnectionSettings.Select(new ClientConnectionDocument
        {
            schemaVersion = 1,
            editor = new ClientConnectionProfile { serverBaseUrl = "http://menu.test", autoConnect = true }
        }, ClientExecutionEnvironment.Editor);

        var session = new FakeSession(data);
        var view = new FakeView();
        var sceneReached = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var sceneFinished = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using (var entry = new GameEntryService(session, session, () => data, validator, _ =>
        {
            sceneReached.TrySetResult(true);
            return sceneFinished.Task;
        }))
        using (var presenter = new GameEntryPresenter(entry, view, settings, default))
        {
            Check(session.Joins == 0 && !view.Last.ShowWaitingRoom, "메뉴 생성만으로 autoConnect 설정의 서버에 입장하지 않음");
            var entering = presenter.JoinAsync();
            Check(session.Joins == 1 && session.Address == settings.ServerBaseUrl, "탈출하기 명령이 config 주소를 사용");
            Check(entry.IsWaitingForParticipants && view.Last.ShowWaitingRoom && view.WaitingCreations == 1 && !entering.IsCompleted,
                "첫 참가자는 최신 한 명 상태에서 대기 UI를 한 번 생성");
            await presenter.JoinAsync();
            Check(session.Joins == 1 && view.WaitingCreations == 1, "입장 중 반복 명령이 접속과 대기 UI를 중복 생성하지 않음");
            session.Publish(ClientSessionPhase.Loading, 2);
            await sceneReached.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Check(view.Last.ShowWaitingRoom && view.WaitingCreations == 1, "첫 참가자는 상대가 와도 씬 전환 중 같은 대기 화면 유지");
            Check(!entry.StartGate.CanPlay, "두 명 입장만으로 실제 플레이 조작을 허용하지 않음");
            sceneFinished.SetResult(true);
            await entering;
        }
        view.RequestJoin();
        Check(session.Joins == 1, "Presenter 정리 후 버튼 이벤트 구독 해제");

        session = new FakeSession(data) { DownloadParticipants = 2 };
        view = new FakeView();
        sceneFinished = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        int scenes = 0;
        using (var entry = new GameEntryService(session, session, () => data, validator, _ =>
        {
            scenes++;
            return sceneFinished.Task;
        }))
        using (var presenter = new GameEntryPresenter(entry, view, settings, default))
        {
            var entering = presenter.JoinAsync();
            Check(scenes == 1 && !view.Last.ShowWaitingRoom && view.WaitingCreations == 0, "두 번째 참가자는 대기 UI 생성 없이 Ingame 로드 시작");
            Check(!entry.IsWaitingForParticipants && entry.StartGate.ParticipantCount == 2, "데이터 동기화가 반영한 최신 인원으로 분기");
            Check(session.Queries == 0, "이미 동기화된 상태를 위해 별도 상태 API를 추가하지 않음");
            Check(!entry.StartGate.CanPlay, "두 번째 참가자도 양쪽 ready 전에는 조작 대기");
            sceneFinished.SetResult(true);
            await entering;
            Check(view.WaitingCreations == 0, "두 번째 참가자의 전체 입장 흐름에 대기 UI 순간 생성도 없음");
        }
        session = new FakeSession(data);
        view = new FakeView();
        scenes = 0;
        using (var entry = new GameEntryService(session, session, () => data, validator, _ =>
        {
            scenes++;
            return Task.CompletedTask;
        }))
        using (var presenter = new GameEntryPresenter(entry, view, settings, default))
        {
            var entering = presenter.JoinAsync();
            session.Disconnect(SessionFailureCode.ConnectionFailed);
            await entering;
            Check(scenes == 0 && entry.Data == null && view.Last.CanJoin && !view.Last.ShowWaitingRoom, "대기 중 연결 종료는 Ingame 진입 없이 시작 메뉴 재시도 상태");
            int previous = view.WaitingCreations;
            session.DownloadParticipants = 2;
            await presenter.JoinAsync();
            Check(scenes == 1 && view.WaitingCreations == previous, "오류 후 재입장에 이전 대기 표시 상태를 재사용하지 않음");
        }
        session = new FakeSession(data);
        view = new FakeView();
        scenes = 0;
        using (var screen = new CancellationTokenSource())
        using (var entry = new GameEntryService(session, session, () => data, validator, _ =>
        {
            scenes++;
            return Task.CompletedTask;
        }))
        using (var presenter = new GameEntryPresenter(entry, view, settings, screen.Token))
        {
            var entering = presenter.JoinAsync();
            screen.Cancel();
            await entering;
            Check(scenes == 0 && session.Leaves == 1 && entry.Data == null, "화면 종료는 참가자 대기 취소와 자리 정리를 수행");
        }
        session = new FakeSession(data) { FailDownload = true };
        view = new FakeView();
        scenes = 0;
        using (var entry = new GameEntryService(session, session, () => data, validator, _ =>
        {
            scenes++;
            return Task.CompletedTask;
        }))
        using (var presenter = new GameEntryPresenter(entry, view, settings, default))
        {
            await presenter.JoinAsync();
            Check(scenes == 0 && session.Leaves == 1 && view.WaitingCreations == 0 && view.Last.CanJoin, "동기화 오류를 참가자 없음으로 추정하지 않음");
        }
        session = new FakeSession(data);
        view = new FakeView();
        scenes = 0;
        using (var entry = new GameEntryService(session, session, () => data, validator, _ =>
        {
            scenes++;
            return Task.CompletedTask;
        }))
        using (var presenter = new GameEntryPresenter(entry, view, settings, default))
        {
            view.RequestLocal("A");
            Check(scenes == 1 && session.Joins == 0 && view.WaitingCreations == 0, "에디터 로컬 A/B 버튼은 상대 대기 없이 기존 검사 경로 사용");
        }
        Console.WriteLine($"시작 메뉴·첫/두 번째 참가자 UI·중복·취소·재시도 검사 {count}개 통과");
    }
    private sealed class FakeView : IGameEntryView
    {
        public SessionConnectionPresentation Last;
        public int WaitingCreations;
        private bool waiting;
        public event Action JoinRequested;
        public event Action<string> LocalRequested;
        public event Action LeaveRequested { add { } remove { } }
        public void RequestJoin() => JoinRequested?.Invoke();
        public void RequestLocal(string role) => LocalRequested?.Invoke(role);
        public void ShowDisconnected(string message)
        {
        }
        public void Render(SessionConnectionPresentation value)
        {
            Last = value;
            if (value.ShowWaitingRoom && !waiting)
                WaitingCreations++;
            waiting = value.ShowWaitingRoom;
        }
    }
    private sealed class FakeSession : ISessionConnectionService, ISessionContentService
    {
        private readonly CsvDataSet data;
        public FakeSession(CsvDataSet data) => this.data = data;
        public int Joins, Leaves, Queries, DownloadParticipants = 1;
        public bool FailDownload;
        public string Address;
        public SessionInfo State
        {
            get;
            private set;
        } = new SessionInfo(null, null, null, null, 0, false);
        public bool IsBusy => false;
        public string ServerBaseUrl => Address;
        public event Action<SessionInfo> StateChanged;
        public event Action<ClientSessionPhase?, ClientSessionPhase> PhaseChanged { add { } remove { } }
        public event Action<SessionFailureCode> BackgroundFailed { add { } remove { } }
        public event Action<SessionFailureCode> Disconnected { add { } remove { } }
        public event Action<IReadOnlyList<int>> RoomsChanged { add { } remove { } }
        public void Publish(ClientSessionPhase phase, int participants)
        {
            State = new SessionInfo("menu", "player", "A", phase, participants, false);
            StateChanged?.Invoke(State);
        }
        public Task<SessionCommandResult> JoinAsync(string address, CancellationToken token)
        {
            Joins++;
            Address = address;
            Publish(ClientSessionPhase.Waiting, 1);
            return Ok();
        }
        public Task<CsvDataSet> DownloadDataAsync(CancellationToken token)
        {
            if (FailDownload)
                throw new IOException("검사용 데이터 다운로드 실패");
            Publish(DownloadParticipants < 2 ? ClientSessionPhase.Waiting : ClientSessionPhase.Loading, DownloadParticipants);
            return Task.FromResult(data);
        }
        public Task<SessionCommandResult> QueryAsync(CancellationToken token)
        {
            Queries++;
            return Ok();
        }
        public Task<SessionCommandResult> LeaveAsync(CancellationToken token)
        {
            Leaves++;
            State = new SessionInfo(null, null, null, null, 0, false);
            StateChanged?.Invoke(State);
            return Ok();
        }
        public void Disconnect(SessionFailureCode reason)
        {
            State = new SessionInfo(null, null, null, null, 0, false, SessionConnectionStatus.Disconnected, reason);
            StateChanged?.Invoke(State);
        }
        public Task<SessionCommandResult> HeartbeatAsync(CancellationToken token) => Ok();
        public Task<SessionCommandResult> ReportSceneLoadedAsync(CancellationToken token) => Ok();
        public Task<PuzzleSubmissionResult> SubmitPuzzleAsync(int id, string[] answers, CancellationToken token) => throw new NotSupportedException();
        private static Task<SessionCommandResult> Ok() => Task.FromResult(SessionCommandResult.Completed());
    }
}
