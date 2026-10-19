using EscapeGame.Data;
using EscapeGame.Data.Csv;
using EscapeGame.Data.CsvValidation.Contracts;
using EscapeGame.Data.CsvValidation.Runtime;
using EscapeGame.Data.Tables;
using EscapeGame.Networking;
using EscapeGame.Session;
using EscapeGame.Session.Contracts;

static class ContentRegressionChecks
{
    internal static async Task Run(Dictionary<string, string> original, string xml)
    {
        int count = 0;
        void Check(bool ok, string label) { if (!ok) throw new Exception("FAIL: " + label); count++; }
        void Throws<T>(Action action, string label) where T : Exception
        {
            bool caught = false;
            try { action(); } catch (T) { caught = true; }
            Check(caught, label);
        }
        var fixture = new TestValidator(xml);
        Dictionary<string, string> Mutate(Action<Dictionary<string, string>> change)
        { var copy = new Dictionary<string, string>(original, StringComparer.Ordinal); change(copy); return copy; }
        void Cell(Dictionary<string, string> input, int rowIndex, string column, string value)
        {
            var doc = new CsvParser().Read(input["ROOM_A"]);
            string Quote(string text) => "\"" + text.Replace("\"", "\"\"") + "\"";
            input["ROOM_A"] = string.Join(",", doc.Headers.Select(Quote)) + "\n" + string.Join("\n", doc.Rows.Select((row, i) =>
                string.Join(",", doc.Headers.Select(header => Quote(i == rowIndex && header == column ? value : row.Get(header))))));
        }
        bool Has(Dictionary<string, string> input, CsvValidationCode code) => fixture.Validate(input).Any(issue =>
            issue.Severity == CsvValidationSeverity.Error && issue.Sheet == "ROOM_A" && issue.Code == code);
        var disconnected = Mutate(input => Cell(input, 0, "Next", "0"));
        Check(Has(disconnected, CsvValidationCode.RoomStartCount), "split start rejected before entry");
        Check(Has(Mutate(input => { for (int i = 0; i < 7; i++) Cell(input, i, "Active", "N"); }), CsvValidationCode.RoomStartCount), "no active start rejected");
        Check(Has(Mutate(input => { Cell(input, 0, "Next", "0"); Cell(input, 6, "Next", "1003"); }), CsvValidationCode.RoomDisconnected), "disconnected cycle rejected despite single start");
        Check(Has(Mutate(input => Cell(input, 1, "Active", "N")), CsvValidationCode.RoomStartCount), "inactive middle room cannot split start");
        Check(Has(Mutate(input => Cell(input, 6, "Active", "N")), CsvValidationCode.RoomDisconnected), "inactive terminal breaks active chain");
        Check(Has(Mutate(input => Cell(input, 0, "ExitRule", "BothSolvd")), CsvValidationCode.InvalidValue), "unknown exit rule rejected by schema");
        var oneRoom = Mutate(input => { Cell(input, 0, "Next", "0"); for (int i = 1; i < 7; i++) Cell(input, i, "Active", "N"); });
        Check(!fixture.Validate(oneRoom).Any(issue => issue.Severity == CsvValidationSeverity.Error), "one active terminal room is valid");
        Check(!RoomExitRules.RequiresBothPuzzles(RoomExitRules.SolvedEach) && RoomExitRules.RequiresBothPuzzles(RoomExitRules.BothSolved), "shared exit policy keeps both supported meanings");
        Throws<ArgumentException>(() => RoomExitRules.RequiresBothPuzzles("BothSolvd"), "runtime policy cannot silently replace unknown exit rule");
        var store = new CsvDataStore();
        var validator = new CsvValidator(fixture.Catalog);
        var baseline = fixture.Input(original);
        store.TryRegister(baseline, validator, out _);
        Check(!store.TryRegister(fixture.Input(disconnected), validator, out _) && ReferenceEquals(store.Data, baseline), "invalid room graph cannot replace registered source");

        var decisions = new[] { (accepted: false, correct: false, outcome: PuzzleSubmissionOutcome.Rejected),
            (accepted: true, correct: false, outcome: PuzzleSubmissionOutcome.Incorrect),
            (accepted: true, correct: true, outcome: PuzzleSubmissionOutcome.Correct) };
        foreach (var decision in decisions)
        {
            var result = PuzzleSubmissionResult.FromDecision(decision.accepted, decision.correct);
            Check(result.Outcome == decision.outcome && result.Accepted == decision.accepted && result.Correct == decision.correct, "public decision " + decision.outcome);
            var dto = new PuzzleSubmissionResponseDto { success = true, accepted = decision.accepted, correct = decision.correct, snapshot = new SessionSnapshotDto() };
            SessionResultValidator.ValidatePuzzleSubmission(200, dto);
            Check(true, "HTTP decision validation " + decision.outcome);
        }
        Throws<ArgumentException>(() => PuzzleSubmissionResult.FromDecision(false, true), "impossible public decision rejected");
        Throws<ArgumentException>(() => SessionResultValidator.ValidatePuzzleSubmission(200,
            new PuzzleSubmissionResponseDto { success = true, accepted = false, correct = true, snapshot = new SessionSnapshotDto() }), "impossible wire decision rejected");

        var data = new CsvGameData(store);
        var local = new EscapeGame.World.LocalRoomProgression(data);
        var answers = data.PuzzleSlots.Where(row => row.PuzzleId == 5001).OrderBy(row => row.Slot).Select(row => row.Answer).ToArray();
        Check(local.Submit("A", 5001, answers).Outcome == PuzzleSubmissionOutcome.Rejected, "local role rejection preserved");
        Check(local.Submit("B", 5001, new[] { "wrong" }).Outcome == PuzzleSubmissionOutcome.Incorrect, "local wrong answer preserved");
        Check(local.Submit("B", 5001, answers).Outcome == PuzzleSubmissionOutcome.Correct, "local correct answer preserved");

        var gateway = new ContentGateway();
        using var service = new SessionClient(new ContentFactory(gateway), default);
        Check((await service.JoinAsync("http://test", default)).Outcome == SessionCommandOutcome.Completed, "session core joins test gateway");
        foreach (var decision in decisions)
        {
            gateway.Decision = PuzzleSubmissionResult.FromDecision(decision.accepted, decision.correct);
            var result = await service.SubmitPuzzleAsync(5001, answers, default);
            Check(result.Outcome == decision.outcome, "gateway/coordinator/service preserves " + decision.outcome);
        }
        gateway.Fail = true;
        bool failed = false;
        try { await service.SubmitPuzzleAsync(5001, answers, default); }
        catch (SessionResultException error) { failed = error.Code == SessionFailureCode.ConnectionFailed; }
        Check(failed, "transport error is not converted into incorrect answer");
        gateway.Fail = false;
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        bool cancelled = false;
        try { await service.SubmitPuzzleAsync(5001, answers, cancel.Token); }
        catch (OperationCanceledException) { cancelled = true; }
        Check(cancelled, "cancelled submission is not converted into incorrect answer");
        Console.WriteLine($"{count} content regression checks passed.");
    }

    private sealed class ContentFactory : ISessionGatewayFactory
    {
        private readonly ISessionGateway gateway;
        internal ContentFactory(ISessionGateway gateway) => this.gateway = gateway;
        public ISessionGateway Create(string address) => gateway;
    }
    private sealed class ContentGateway : ISessionGateway, ISessionContentGateway
    {
        internal PuzzleSubmissionResult Decision = PuzzleSubmissionResult.FromDecision(false, false);
        internal bool Fail;
        private SessionSnapshotData Snapshot => new SessionSnapshotData("test", ClientSessionPhase.Waiting,
            new[] { new SessionPlayerData("player", "B", false) }, new[] { 1001, 1002 });
        public Task<SessionJoinData> JoinAsync(CancellationToken token) => Task.FromResult(new SessionJoinData("test", new SessionPlayerData("player", "B", false), "token"));
        public Task<SessionSnapshotData> QueryAsync(string session, string participant, CancellationToken token) => Task.FromResult(Snapshot);
        public Task<SessionSnapshotData> ReportSceneLoadedAsync(string session, string participant, CancellationToken token) => Task.FromResult(Snapshot);
        public Task LeaveAsync(string session, string participant, CancellationToken token) => Task.CompletedTask;
        public Task HeartbeatAsync(string session, string participant, CancellationToken token) => Task.CompletedTask;
        public Task<CsvDataSet> DownloadDataAsync(string session, string participant, CancellationToken token) => throw new NotSupportedException();
        public Task<SessionPuzzleSubmissionData> SubmitPuzzleAsync(string session, string participant, int id, string[] answers, CancellationToken token)
        {
            if (Fail) throw new SessionResultException(SessionFailureCode.ConnectionFailed);
            return Task.FromResult(new SessionPuzzleSubmissionData(Snapshot, Decision));
        }
    }
}
