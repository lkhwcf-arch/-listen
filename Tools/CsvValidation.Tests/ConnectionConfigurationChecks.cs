using EscapeGame.Configuration;
using EscapeGame.Data;
using EscapeGame.Data.CsvValidation.Runtime;
using EscapeGame.Networking;
using EscapeGame.Session.Contracts;
using EscapeGame.Session.Presentation;
using EscapeGame.World;

static class ConnectionConfigurationChecks
{
    internal static async Task Run(string root, Dictionary<string, string> original, string catalogXml)
    {
        int count = 0;
        void Check(bool ok, string name) { if (!ok) throw new Exception("FAIL: " + name); count++; }
        void Throws<T>(Action action, string name) where T : Exception
        { bool caught = false; try { action(); } catch (T) { caught = true; } Check(caught, name); }
        var document = new ClientConnectionDocument {
            schemaVersion = 1,
            editor = new ClientConnectionProfile { serverBaseUrl = " http://127.0.0.1:3000/ ", autoConnect = true },
            testBuild = new ClientConnectionProfile { serverBaseUrl = "http://192.168.1.34:3001", autoConnect = true },
            stableBuild = new ClientConnectionProfile { serverBaseUrl = "https://game.example.test", autoConnect = true }
        };
        var editor = ClientConnectionSettings.Select(document, ClientExecutionEnvironment.Editor);
        var test = ClientConnectionSettings.Select(document, ClientExecutionEnvironment.TestBuild);
        Check(editor.ServerBaseUrl == "http://127.0.0.1:3000" && editor.AutoConnect, "editor selects and normalizes editor address");
        Check(test.ServerBaseUrl == "http://192.168.1.34:3001", "development selects LAN address");
        Check(ClientConnectionSettings.Select(document, ClientExecutionEnvironment.StableBuild).ServerBaseUrl == "https://game.example.test", "stable selects its own address");
        document.stableBuild.serverBaseUrl = "";
        Throws<InvalidOperationException>(() => ClientConnectionSettings.Select(document, ClientExecutionEnvironment.StableBuild), "stable cannot fall back to test");
        Check(ClientConnectionSettings.Select(document, ClientExecutionEnvironment.TestBuild).ServerBaseUrl == test.ServerBaseUrl, "unconfigured stable does not block test build");
        document.editor.serverBaseUrl = "http://changed.test";
        Check(editor.ServerBaseUrl == "http://127.0.0.1:3000", "selected settings own immutable values");
        document.schemaVersion = 2;
        Throws<InvalidOperationException>(() => ClientConnectionSettings.Select(document, ClientExecutionEnvironment.Editor), "unknown schema rejected");
        document.schemaVersion = 1;
        document.editor = null;
        Throws<InvalidOperationException>(() => ClientConnectionSettings.Select(document, ClientExecutionEnvironment.Editor), "missing selected profile rejected");
        Throws<ArgumentOutOfRangeException>(() => ClientConnectionSettings.Select(document, (ClientExecutionEnvironment)99), "unknown environment rejected");
        foreach (var invalid in new[] { "", "relative/path", "ftp://game.test", "http://game.test?url=other", "http://game.test#part", "http://user:pass@game.test" })
            Throws<ArgumentException>(() => HttpServerAddress.Normalize(invalid), "invalid address " + invalid);

        var configured = System.Text.Json.JsonSerializer.Deserialize<ClientConnectionDocument>(
            File.ReadAllText(Path.Combine(root, "Assets/StreamingAssets/Configuration/connection.json")),
            new System.Text.Json.JsonSerializerOptions { IncludeFields = true });
        Check(ClientConnectionSettings.Select(configured, ClientExecutionEnvironment.Editor).ServerBaseUrl == "http://127.0.0.1:3000", "distributed JSON contains editor endpoint");
        Check(ClientConnectionSettings.Select(configured, ClientExecutionEnvironment.TestBuild).ServerBaseUrl == "http://192.168.1.34:3001", "distributed JSON contains user LAN endpoint");
        Throws<InvalidOperationException>(() => ClientConnectionSettings.Select(configured, ClientExecutionEnvironment.StableBuild), "distributed stable profile requires cloud setup");

        int attempts = 0;
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        string usedAddress = null;
        var connection = new ConfiguredServerConnection(test, async (address, token) => {
            attempts++; usedAddress = address; await gate.Task;
        });
        var running = connection.StartAsync(default);
        await connection.StartAsync(default);
        await connection.ConnectAsync(default);
        Check(attempts == 1 && usedAddress == test.ServerBaseUrl, "automatic and button requests cannot overlap");
        gate.SetResult(true); await running;
        await connection.StartAsync(default);
        Check(attempts == 1, "completed startup never auto-joins again");
        await connection.ConnectAsync(default);
        Check(attempts == 2, "explicit retry reuses selected address");
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        bool cancelled = false;
        try { await connection.ConnectAsync(cancellation.Token); } catch (OperationCanceledException) { cancelled = true; }
        Check(cancelled && attempts == 2, "cancelled request never sends join");
        bool fail = true;
        var retry = new ConfiguredServerConnection(test, (_, _) => fail ? Task.FromException(new IOException("test")) : Task.CompletedTask);
        try { await retry.StartAsync(default); } catch (IOException) { }
        fail = false; await retry.ConnectAsync(default);
        Check(true, "fault releases request gate for explicit retry");
        document.editor = new ClientConnectionProfile { serverBaseUrl = "http://offline.test", autoConnect = false };
        int manualCalls = 0;
        var manualSettings = ClientConnectionSettings.Select(document, ClientExecutionEnvironment.Editor);
        var manual = new ConfiguredServerConnection(manualSettings, (_, _) => { manualCalls++; return Task.CompletedTask; });
        await manual.StartAsync(default);
        Check(manualCalls == 0, "autoConnect false keeps local test selection available");
        await manual.ConnectAsync(default);
        Check(manualCalls == 1, "manual join still uses configuration");

        var service = new FakeSession();
        var view = new FakeView();
        using (var presenter = new SessionConnectionPresenter(service, view, test, default))
        {
            await presenter.StartAsync(); await presenter.StartAsync();
            Check(service.Joins == 1 && service.LastAddress == test.ServerBaseUrl, "session presenter joins configured endpoint once");
            Check(!view.Last.CanEditAddress, "session presenter never enables address editing");
        }
        view.RequestJoin();
        Check(service.Joins == 1, "disposed presenter unsubscribes button");

        // 실제 CSV를 주입하여 입장·데이터 등록·씬 전환까지 같은 경로를 검사합니다.
        var fixture = new TestValidator(catalogXml);
        var source = fixture.Input(original);
        service = new FakeSession { Source = source };
        view = new FakeView();
        int entered = 0;
        using var entry = new GameEntryService(service, service, () => throw new Exception("local fallback"),
            new CsvValidator(fixture.Catalog), _ => { entered++; return Task.CompletedTask; });
        using (var presenter = new GameEntryPresenter(entry, view, test, default))
        {
            await presenter.StartAsync(); await presenter.StartAsync();
            Check(service.Joins == 1 && service.Downloads == 1 && entered == 1, "game entry presenter preserves join/data/scene sequence");
            Check(service.LastAddress == test.ServerBaseUrl && entry.Role == "B" && !entry.IsLocal, "game entry injects configured address and server role");
            Check(!view.Last.CanEditAddress, "game entry never enables address editing");
        }
        Console.WriteLine($"{count} connection configuration checks passed.");
    }

    private sealed class FakeView : IGameEntryView
    {
        public SessionConnectionPresentation Last;
        public event Action JoinRequested;
        public event Action LeaveRequested { add { } remove { } }
        public event Action<string> LocalRequested { add { } remove { } }
        public void RequestJoin() => JoinRequested?.Invoke();
        public void Render(SessionConnectionPresentation presentation) => Last = presentation;
        public void ShowDisconnected(string message) { }
    }
    private sealed class FakeSession : ISessionConnectionService, ISessionContentService
    {
        public int Joins, Downloads;
        public string LastAddress;
        public CsvDataSet Source;
        public SessionInfo State { get; private set; } = new SessionInfo(null, null, null, null, 0, false);
        public bool IsBusy => false;
        public string ServerBaseUrl => LastAddress;
        public event Action<SessionInfo> StateChanged { add { } remove { } }
        public event Action<ClientSessionPhase?, ClientSessionPhase> PhaseChanged { add { } remove { } }
        public event Action<SessionFailureCode> BackgroundFailed { add { } remove { } }
        public event Action<SessionFailureCode> Disconnected { add { } remove { } }
        public event Action<IReadOnlyList<int>> RoomsChanged { add { } remove { } }
        public Task<SessionCommandResult> JoinAsync(string address, CancellationToken token)
        { Joins++; LastAddress = address; State = new SessionInfo("s", "p", "B", ClientSessionPhase.Waiting, 1, false); return Task.FromResult(SessionCommandResult.Completed()); }
        public Task<SessionCommandResult> LeaveAsync(CancellationToken token) => Task.FromResult(SessionCommandResult.Completed());
        public Task<SessionCommandResult> QueryAsync(CancellationToken token) => Task.FromResult(SessionCommandResult.Completed());
        public Task<SessionCommandResult> HeartbeatAsync(CancellationToken token) => Task.FromResult(SessionCommandResult.Completed());
        public Task<SessionCommandResult> ReportSceneLoadedAsync(CancellationToken token) => Task.FromResult(SessionCommandResult.Completed());
        public Task<CsvDataSet> DownloadDataAsync(CancellationToken token) { Downloads++; return Task.FromResult(Source); }
        public Task<PuzzleSubmissionResult> SubmitPuzzleAsync(int id, string[] answers, CancellationToken token) => throw new NotSupportedException();
        public void Disconnect(SessionFailureCode reason) { }
    }
}
