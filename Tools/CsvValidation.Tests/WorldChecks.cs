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
        void Check(bool ok, string label) { if (!ok) throw new Exception("FAIL: " + label); count++; }
        var fixture = new TestValidator(xml);
        var validator = new CsvValidator(fixture.Catalog);
        var source = fixture.Input(original);
        var store = new CsvDataStore();
        store.TryRegister(source, validator, out _);
        var data = new CsvGameData(store);
        var a = new RoomLoadPlan(data, "A"); var b = new RoomLoadPlan(data, "B");
        Check(a.First.Id == 1001 && b.First.Id == 1002, "role first rooms");
        Check(a.Anchors(a.First).Count == 8 && b.Anchors(b.First).Count == 14, "R01 active anchors");
        Check(a.Anchors(a.First).All(row => row.PlayerType != "B"), "A does not load B anchors");
        Check(b.Anchors(b.First).All(row => row.PlayerType != "A"), "B does not load A anchors");
        var local = new LocalRoomProgression(data);
        var answer = data.PuzzleSlots.Where(row => row.PuzzleId == 5001).OrderBy(row => row.Slot).Select(row => row.Answer.ToLowerInvariant()).ToArray();
        Check(!local.Submit("A", 5001, answer), "clue role cannot solve");
        Check(!local.Submit("B", 5002, answer), "locked puzzle cannot solve");
        Check(!local.Submit("B", 5001, new[] { "wrong" }), "wrong answer does not unlock");
        Check(local.Submit("B", 5001, answer), "correct answer unlocks");
        Check(local.UnlockedRoomIds.SequenceEqual(new[] {1001,1002,1003,1004}), "old rooms remain after unlock");
        local.Submit("B", 5001, answer);
        Check(local.UnlockedRoomIds.Count == 4, "repeated solve is idempotent");
        var gateway = new FakeSession(source);
        int localReads = 0, sceneEntries = 0;
        using var entry = new GameEntryService(gateway, gateway, () => {localReads++; return source;}, validator,
            _ => { sceneEntries++; return Task.CompletedTask; });
        await entry.EnterAsync(false, "http://test", null, default);
        Check(gateway.Joins == 1 && gateway.Downloads == 1 && localReads == 0, "server mode only downloads server source");
        Check(entry.Data.Source == source && entry.Role == "B" && !entry.IsLocal && sceneEntries == 1, "server snapshot injected before scene");
        Check(entry.UnlockedRoomIds.Count == 4, "progress received during download survives data registration");
        gateway.PublishRooms(new[] {1001,1002,1003,1004});
        Check(entry.UnlockedRoomIds.Count == 4, "server progression event delivered");
        await entry.EnterAsync(true, null, "A", default);
        Check(gateway.Leaves == 1 && localReads == 1 && gateway.Downloads == 1 && entry.IsLocal && entry.Role == "A", "local entry leaves server and uses bundled source");
        gateway.FailsDownload = true;
        await entry.EnterAsync(false, "http://test", null, default);
        Check(entry.Data == null && sceneEntries == 2 && localReads == 1 && gateway.Leaves == 2, "sync failure does not fallback and rolls back seat");
        var invalid = new CsvDataSet(new Dictionary<string, EscapeGame.Data.Csv.CsvDocument>());
        using var bad = new GameEntryService(gateway, gateway, () => invalid, validator, _ => { sceneEntries++; return Task.CompletedTask; });
        await bad.EnterAsync(true, null, "B", default);
        Check(bad.Data == null && sceneEntries == 2, "invalid local data cannot enter scene");
        Console.WriteLine($"{count} world/entry checks passed.");
    }

    private sealed class FakeSession : ISessionConnectionService, ISessionContentService
    {
        private readonly CsvDataSet data;
        public int Joins, Leaves, Downloads;
        public bool FailsDownload;
        public FakeSession(CsvDataSet data) => this.data = data;
        public SessionInfo State { get; private set; } = new SessionInfo(null,null,null,null,0,false);
        public bool IsBusy => false;
        public string ServerBaseUrl => "http://test";
        public event Action<SessionInfo> StateChanged { add {} remove {} }
        public event Action<ClientSessionPhase?, ClientSessionPhase> PhaseChanged { add {} remove {} }
        public event Action<SessionFailureCode> BackgroundFailed { add {} remove {} }
        public event Action<SessionFailureCode> Disconnected { add {} remove {} }
        public event Action<IReadOnlyList<int>> RoomsChanged;
        public void PublishRooms(int[] ids) => RoomsChanged?.Invoke(Array.AsReadOnly(ids));
        public Task<SessionCommandResult> JoinAsync(string address, CancellationToken token)
        { Joins++; State = new SessionInfo("s","p","B",ClientSessionPhase.Waiting,1,false); return Ok(); }
        public Task<SessionCommandResult> LeaveAsync(CancellationToken token)
        { Leaves++; State = new SessionInfo(null,null,null,null,0,false); return Ok(); }
        public Task<CsvDataSet> DownloadDataAsync(CancellationToken token)
        { Downloads++; if (FailsDownload) throw new IOException("test download failure"); PublishRooms(new[] {1001,1002,1003,1004}); return Task.FromResult(data); }
        public Task<SessionCommandResult> QueryAsync(CancellationToken token) { PublishRooms(new[] {1001,1002}); return Ok(); }
        public Task<SessionCommandResult> HeartbeatAsync(CancellationToken token) => Ok();
        public Task<SessionCommandResult> ReportSceneLoadedAsync(CancellationToken token) => Ok();
        public Task<bool> SubmitPuzzleAsync(int id, string[] answers, CancellationToken token) => Task.FromResult(false);
        public void Disconnect(SessionFailureCode reason) { }
        private Task<SessionCommandResult> Ok() => Task.FromResult(SessionCommandResult.Completed());
    }
}
