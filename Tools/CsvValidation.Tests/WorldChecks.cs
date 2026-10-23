using EscapeGame.Data;
using EscapeGame.Data.CsvValidation;
using EscapeGame.Data.CsvValidation.Runtime;
using EscapeGame.Data.Tables;
using EscapeGame.Session.Contracts;
using EscapeGame.World;

static class WorldChecks
{
    internal static async Task Run(Dictionary<string, string> original, string xml)
    {
        int count = 0;
        void Check(bool ok, string label)
        {
            if (!ok)
                throw new Exception("FAIL: " + label);
            count++;
        }
        var fixture = new TestValidator(xml);
        var validator = new CsvValidator(fixture.Catalog);
        var source = fixture.Input(original);
        var store = new CsvDataStore();
        store.TryRegister(source, validator, out _);
        var data = new CsvGameData(store);
        var a = new RoomLoadPlan(data, "A");
        var b = new RoomLoadPlan(data, "B");
        Check(a.First.Id == 1001 && b.First.Id == 1002, "role first rooms");
        Check(ReferenceEquals(a.Rooms, a.Rooms) && ReferenceEquals(a.Anchors(a.First), a.Anchors(a.First)), "immutable room and anchor views are reused");
        Check(a.Find(a.First.Id) == a.First && a.Find(-1) == null, "indexed room lookup preserves missing-ID behavior");
        var cachedAnchors = a.Anchors(a.First);
        long before = GC.GetAllocatedBytesForCurrentThread();
        int checksum = 0;
        for (int i = 0; i < 10000; i++)
            checksum += a.Rooms.Count + a.First.Id + a.Find(1001).RoomNo + a.Anchors(a.First).Count;
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Check(allocated == 0 && checksum > 0, "10000 warmed immutable lookups allocate no managed bytes");
        Console.WriteLine($"방 조회 할당 검사: 10000회, {allocated} bytes (.NET 순수 모델, Unity Profiler 측정 아님)");
        var common = a.Anchors(a.First).Where(row => row.PlayerType == "ALL").Select(row => row.Id).ToArray();
        Check(common.Length > 0 && common.SequenceEqual(b.Anchors(b.First).Where(row => row.PlayerType == "ALL").Select(row => row.Id)), "both roles receive the same declared common anchors");
        Check(a.Anchors(a.First).All(row => row.PlayerType != "B"), "A does not load B anchors");
        Check(b.Anchors(b.First).All(row => row.PlayerType != "A"), "B does not load A anchors");
        var local = new LocalRoomProgression(data);
        var answer = data.PuzzleSlots.Where(row => row.PuzzleId == 5001).OrderBy(row => row.Slot).Select(row => row.Answer.ToLowerInvariant()).ToArray();
        Check(local.Submit("A", 5001, answer).Outcome == PuzzleSubmissionOutcome.Rejected, "clue role cannot solve");
        Check(local.Submit("B", 5002, answer).Outcome == PuzzleSubmissionOutcome.Rejected, "locked puzzle cannot solve");
        Check(local.Submit("B", 5001, new[] { "wrong" }).Outcome == PuzzleSubmissionOutcome.Incorrect, "wrong answer does not unlock");
        Check(local.Submit("B", 5001, answer).Outcome == PuzzleSubmissionOutcome.Correct, "correct answer unlocks");
        Check(local.UnlockedRoomIds.SequenceEqual(new[] { 1001, 1002, 1004 }), "only solver next unlocks before clue checks door");
        if (!local.CheckDoor("A", 1))
            throw new Exception("clue door should be permitted after solve");
        local.Submit("B", 5001, answer);
        Check(local.UnlockedRoomIds.Count == 4, "repeated solve is idempotent");
        var gateway = new FakeSession(source);
        int localReads = 0, sceneEntries = 0;
        using var entry = new GameEntryService(gateway, gateway, () =>
        {
            localReads++;
            return source;
        }, validator,
            _ =>
            {
                sceneEntries++;
                return Task.CompletedTask;
            });
        await entry.EnterAsync(false, "http://test", null, default);
        Check(gateway.Joins == 1 && gateway.Downloads == 1 && localReads == 0, "server mode only downloads server source");
        Check(entry.Data.Source == source && entry.Role == "B" && !entry.IsLocal && sceneEntries == 1, "server snapshot injected before scene");
        Check(entry.UnlockedRoomIds.Count == 4, "progress received during download survives data registration");
        gateway.PublishRooms(new[] { 1001, 1002, 1003, 1004 });
        Check(entry.UnlockedRoomIds.Count == 4, "server progression event delivered");
        await entry.EnterAsync(true, null, "A", default);
        Check(gateway.Leaves == 1 && localReads == 1 && gateway.Downloads == 1 && entry.IsLocal && entry.Role == "A", "local entry leaves server and uses bundled source");
        gateway.FailsDownload = true;
        await entry.EnterAsync(false, "http://test", null, default);
        Check(entry.Data == null && sceneEntries == 2 && localReads == 1 && gateway.Leaves == 2, "sync failure does not fallback and rolls back seat");
        var invalid = new CsvDataSet(new Dictionary<string, EscapeGame.Data.Csv.CsvDocument>());
        using var bad = new GameEntryService(gateway, gateway, () => invalid, validator, _ =>
        {
            sceneEntries++;
            return Task.CompletedTask;
        });
        await bad.EnterAsync(true, null, "B", default);
        Check(bad.Data == null && sceneEntries == 2, "invalid local data cannot enter scene");
        foreach (bool useLocal in new[] { true, false })
        {
            var transitionGateway = new FakeSession(source);
            using var screen = new CancellationTokenSource();
            bool sceneFinished = false;
            using var transition = new GameEntryService(transitionGateway, transitionGateway, () => source, validator, async owner =>
            {
                screen.Cancel(); // Join Screen.OnDisable이 씬 교체 도중 발생하는 순서를 재현합니다.
                await Task.Yield();
                owner.ThrowIfCancellationRequested();
                sceneFinished = true;
            });
            await transition.EnterAsync(useLocal, "http://test", "A", screen.Token);
            Check(sceneFinished && transition.Data != null && transitionGateway.Leaves == 0,
                "screen teardown cannot roll back committed scene entry: local=" + useLocal);
        }
        using (var cancelled = new CancellationTokenSource())
        {
            cancelled.Cancel();
            var cancelledGateway = new FakeSession(source);
            int loads = 0;
            using var cancelledEntry = new GameEntryService(cancelledGateway, cancelledGateway, () => source, validator,
                _ =>
                {
                    loads++;
                    return Task.CompletedTask;
                });
            await cancelledEntry.EnterAsync(false, "http://test", null, cancelled.Token);
            Check(cancelledGateway.Joins == 0 && loads == 0 && cancelledEntry.Data == null, "pre-commit cancellation never joins or loads a scene");
        }
        using (var ownerLifetime = new CancellationTokenSource())
        {
            var lostOwnerGateway = new FakeSession(source);
            using var lostOwner = new GameEntryService(lostOwnerGateway, lostOwnerGateway, () => source, validator, owner =>
            {
                ownerLifetime.Cancel();
                owner.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            }, ownerLifetime.Token);
            await lostOwner.EnterAsync(false, "http://test", null, default);
            Check(lostOwner.Data == null && lostOwnerGateway.Leaves == 1, "game-owner shutdown still cancels and rolls back entry");
        }
        var waitingGateway = new FakeSession(source) { JoinParticipants = 1 };
        int waitingLoads = 0;
        using (var waitingEntry = new GameEntryService(waitingGateway, waitingGateway, () => source, validator,
            _ =>
            {
                waitingLoads++;
                return Task.CompletedTask;
            }))
        {
            var entering = waitingEntry.EnterAsync(false, "http://test", null, default);
            Check(!entering.IsCompleted && waitingLoads == 0 && waitingEntry.IsBusy && waitingEntry.Data != null,
                "한 명 접속은 데이터를 등록한 뒤 Join에서 대기합니다.");
            waitingGateway.PublishState(ClientSessionPhase.Loading, 2);
            await entering;
            Check(waitingLoads == 1 && waitingEntry.Data != null && !waitingEntry.StartGate.CanPlay,
                "두 명 접속 시 씬을 한 번 로드하고 양쪽 준비 전에는 조작을 잠급니다.");
            waitingGateway.PublishState(ClientSessionPhase.Playing, 2);
            Check(waitingEntry.StartGate.CanPlay, "두 참가자 준비 후 조작을 허용합니다.");
        }
        using (var waitingCancellation = new CancellationTokenSource())
        {
            var cancelledWait = new FakeSession(source) { JoinParticipants = 1 };
            using var cancelledEntry = new GameEntryService(cancelledWait, cancelledWait, () => source, validator,
                _ => throw new Exception("취소한 대기에서 씬 로드 금지"));
            var entering = cancelledEntry.EnterAsync(false, "http://test", null, waitingCancellation.Token);
            waitingCancellation.Cancel();
            await entering;
            Check(cancelledEntry.Data == null && cancelledWait.Leaves == 1, "Join 대기 취소는 참가 자리를 반환합니다.");
        }
        var failedSpawnGateway = new FakeSession(source);
        using (var failedSpawn = new GameEntryService(failedSpawnGateway, failedSpawnGateway, () => source, validator,
            _ => throw new InvalidOperationException("test player initialization failure")))
        {
            await failedSpawn.EnterAsync(false, "http://test", null, default);
            Check(failedSpawn.Data == null && failedSpawnGateway.Leaves == 1 && failedSpawn.Message.Contains("player initialization"),
                "player/room initialization failure is an entry failure and releases the seat");
        }
        Console.WriteLine($"{count} world/entry checks passed.");
    }

    private sealed class FakeSession : ISessionConnectionService, ISessionContentService
    {
        private readonly CsvDataSet data;
        public int Joins, Leaves, Downloads;
        public bool FailsDownload;
        public int JoinParticipants = 2;
        public FakeSession(CsvDataSet data) => this.data = data;
        public SessionInfo State
        {
            get;
            private set;
        } = new SessionInfo(null, null, null, null, 0, false);
        public bool IsBusy => false;
        public string ServerBaseUrl => "http://test";
        public event Action<SessionInfo> StateChanged;
        public void PublishState(ClientSessionPhase phase, int participants)
        {
            State = new SessionInfo("s", "p", "B", phase, participants, false);
            StateChanged?.Invoke(State);
        }
        public event Action<ClientSessionPhase?, ClientSessionPhase> PhaseChanged { add { } remove { } }
        public event Action<SessionFailureCode> BackgroundFailed { add { } remove { } }
        public event Action<SessionFailureCode> Disconnected { add { } remove { } }
        public event Action<IReadOnlyList<int>> RoomsChanged;
        public void PublishRooms(int[] ids) => RoomsChanged?.Invoke(Array.AsReadOnly(ids));
        public Task<SessionCommandResult> JoinAsync(string address, CancellationToken token)
        {
            Joins++;
            PublishState(JoinParticipants < 2 ? ClientSessionPhase.Waiting : ClientSessionPhase.Loading, JoinParticipants);
            return Ok();
        }
        public Task<SessionCommandResult> LeaveAsync(CancellationToken token)
        {
            Leaves++;
            State = new SessionInfo(null, null, null, null, 0, false);
            return Ok();
        }
        public Task<CsvDataSet> DownloadDataAsync(CancellationToken token)
        {
            Downloads++;
            if (FailsDownload)
                throw new IOException("test download failure");
            PublishRooms(new[] { 1001, 1002, 1003, 1004 });
            return Task.FromResult(data);
        }
        public Task<SessionCommandResult> QueryAsync(CancellationToken token)
        {
            PublishRooms(new[] { 1001, 1002 });
            return Ok();
        }
        public Task<SessionCommandResult> HeartbeatAsync(CancellationToken token) => Ok();
        public Task<SessionCommandResult> ReportSceneLoadedAsync(CancellationToken token) => Ok();
        public Task<PuzzleSubmissionResult> SubmitPuzzleAsync(int id, string[] answers, CancellationToken token) => Task.FromResult(PuzzleSubmissionResult.FromDecision(false, false));
        public void Disconnect(SessionFailureCode reason)
        {
            State = new SessionInfo("s", "p", "B", null, 0, false, SessionConnectionStatus.Disconnected, reason);
            StateChanged?.Invoke(State);
        }
        private Task<SessionCommandResult> Ok() => Task.FromResult(SessionCommandResult.Completed());
    }
}
