using System.Collections.ObjectModel;
using System.Globalization;
using EscapeGame.Data;
using EscapeGame.Data.Csv;
using EscapeGame.Data.Infrastructure;
using EscapeGame.Data.CsvValidation.Contracts;
using EscapeGame.Data.CsvValidation.Runtime;
using EscapeGame.Data.CsvValidation.Schema;

internal static class MemoryPipelineChecks
{
    public static async Task Run(string root, Dictionary<string, string> original, string xml)
    {
        int passed = 0;
        void Check(bool value, string name)
        {
            if (!value) throw new Exception("FAIL: " + name);
            passed++; Console.WriteLine("PASS: " + name);
        }
        var fixture = new TestValidator(xml);
        var data = fixture.Input(original);
        var validator = new CsvValidator(fixture.Catalog);
        var first = validator.Validate(data);
        var repeated = validator.Validate(data);
        Check(first.IsValid && ReferenceEquals(first.Data, data) && ReferenceEquals(repeated.Data, data), "validation consumes the same in-memory snapshot");

        var store = new CsvDataStore();
        int loadCalls = 0, registered = 0;
        bool appliedBeforeEvent = true;
        store.Registered += snapshot => { registered++; appliedBeforeEvent &= ReferenceEquals(store.Data, snapshot) && ReferenceEquals(store.Validation.Data, snapshot); };
        Task<CsvDataSet> Load(CancellationToken token) { loadCalls++; return Task.FromResult(data); }
        await store.InitializeAsync(Load, validator, CancellationToken.None);
        await store.InitializeAsync(Load, validator, CancellationToken.None);
        Check(loadCalls == 1 && registered == 1 && appliedBeforeEvent, "initialize loads/registers once and publishes applied state");

        int conversions = 0;
        var puzzles = store.MapTable("PUZZLE", row => { conversions++; return new PuzzleData(row); });
        var consumer = new PuzzleConsumer(store.GetTable<PuzzleData>("PUZZLE"));
        Check(conversions == 8 && consumer.Puzzles[0].Id == 5001 && consumer.Puzzles[0].SlotCount == 4 && ReferenceEquals(puzzles, consumer.Puzzles), "real CSV rows map to data classes and inject into a consumer");
        Check(ReferenceEquals(puzzles, store.GetTable<PuzzleData>("PUZZLE")) && conversions == 8, "typed table is reused without re-reading or converting");
        bool rejected = false;
        try { store.MapTable("PUZZLE", row => new PuzzleData(row)); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "duplicate mapping registration cannot silently replace a mapper");
        Check(puzzles is ReadOnlyCollection<PuzzleData>, "mapped table collection is read-only");

        var bad = new Dictionary<string, string>(original) { ["PUZZLE"] = original["PUZZLE"].Replace("\"5001\"", "\"invalid\"") };
        var invalid = fixture.Input(bad);
        bool accepted = store.TryRegister(invalid, validator, out var invalidReport);
        Check(!accepted && !invalidReport.IsValid && ReferenceEquals(store.Data, data) && ReferenceEquals(store.GetTable<PuzzleData>("PUZZLE"), puzzles) && registered == 1,
            "invalid registration preserves prior snapshot and typed cache");
        var emptyStore = new CsvDataStore();
        rejected = false;
        try { emptyStore.MapTable("PUZZLE", row => new PuzzleData(row)); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "unvalidated data cannot be mapped through the store");

        var replacement = fixture.Input(original);
        Check(store.TryRegister(replacement, validator, out _) && ReferenceEquals(store.Data, replacement) && registered == 2, "valid replacement publishes a new snapshot");
        rejected = false;
        try { store.GetTable<PuzzleData>("PUZZLE"); } catch (KeyNotFoundException) { rejected = true; }
        Check(rejected && consumer.Puzzles[0].Id == 5001, "replacement invalidates store cache while old injected snapshot stays stable");
        var newPuzzles = store.MapTable("PUZZLE", row => new PuzzleData(row));
        Check(!ReferenceEquals(newPuzzles, puzzles), "new registration can map fresh data for reinjection");
        store.TryRegister(replacement, validator, out _);
        Check(registered == 2 && ReferenceEquals(newPuzzles, store.GetTable<PuzzleData>("PUZZLE")), "same snapshot registration does not invalidate mappings or repeat event");

        rejected = false;
        try { store.MapTable<string>("CARD", row => throw new FormatException("bad conversion")); } catch (FormatException) { rejected = true; }
        var cards = store.MapTable("CARD", row => row.Get("ID"));
        Check(rejected && cards.Count == 50, "failed conversion leaves no partial typed registration");
        var swapped = fixture.Input(original);
        rejected = false;
        try { store.MapTable("TV", row => { store.TryRegister(swapped, validator, out _); return row.Get("Letter"); }); }
        catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "mapping cannot cache results after source replacement during conversion");

        var concurrentStore = new CsvDataStore();
        var completion = new TaskCompletionSource<CsvDataSet>(TaskCreationOptions.RunContinuationsAsynchronously);
        int concurrentLoads = 0;
        Task<CsvDataSet> Deferred(CancellationToken token) { concurrentLoads++; return completion.Task; }
        var pending1 = concurrentStore.InitializeAsync(Deferred, validator, CancellationToken.None);
        var pending2 = concurrentStore.InitializeAsync(Deferred, validator, CancellationToken.None);
        using (var cancellation = new CancellationTokenSource())
        {
            var waiting = concurrentStore.InitializeAsync(Deferred, validator, cancellation.Token);
            cancellation.Cancel();
            rejected = false;
            try { await waiting; } catch (OperationCanceledException) { rejected = true; }
            Check(rejected && !pending1.IsCompleted, "waiting caller cancellation does not cancel the active load");
        }
        completion.SetResult(data);
        await Task.WhenAll(pending1, pending2);
        Check(concurrentLoads == 1 && ReferenceEquals(concurrentStore.Data, data), "concurrent initialization performs one load");

        var cancelledStore = new CsvDataStore();
        using (var cancellation = new CancellationTokenSource())
        {
            var result = new TaskCompletionSource<CsvDataSet>(TaskCreationOptions.RunContinuationsAsynchronously);
            var pending = cancelledStore.InitializeAsync(_ => result.Task, validator, cancellation.Token);
            cancellation.Cancel(); result.SetResult(data);
            rejected = false;
            try { await pending; } catch (OperationCanceledException) { rejected = true; }
            Check(rejected && !cancelledStore.HasData, "cancelled load cannot register a late snapshot");
        }
        await cancelledStore.InitializeAsync(Load, validator, CancellationToken.None);
        Check(cancelledStore.HasData, "initialization can retry after cancellation");

        var retryStore = new CsvDataStore();
        var failed = await retryStore.InitializeAsync(_ => Task.FromResult(invalid), validator, CancellationToken.None);
        var recovered = await retryStore.InitializeAsync(Load, validator, CancellationToken.None);
        Check(!failed.IsValid && recovered.IsValid && retryStore.HasData, "failed validation can retry without poisoning initialization");
        var directStore = new CsvDataStore();
        var loading = new TaskCompletionSource<CsvDataSet>(TaskCreationOptions.RunContinuationsAsynchronously);
        var stale = directStore.InitializeAsync(_ => loading.Task, validator, CancellationToken.None);
        directStore.TryRegister(replacement, validator, out _);
        loading.SetResult(data); await stale;
        Check(ReferenceEquals(directStore.Data, replacement), "late initialization does not overwrite a direct registration");

        var renamed = original.ToDictionary(x => x.Key.Replace("PUZZLE", "RIDDLES"), x => x.Value.Replace("PUZZLE", "RIDDLES"));
        Check(!new TestValidator(xml.Replace("PUZZLE", "RIDDLES")).Validate(renamed).Any(x => x.Severity == CsvValidationSeverity.Error), "table names can change through configuration without editing rule code");
        var custom = new CsvValidator(fixture.Catalog, additionalRules: new[] { new MarkerRule() }).Validate(data);
        Check(custom.Issues.Any(x => x.Details.Contains("injected")), "additional validation rule can be injected");
        var resources = new CsvValidator(fixture.Catalog, new MissingResources()).Validate(data);
        Check(resources.WarningCount > 0 && resources.IsValid, "resource resolver remains optional and replaceable");
        var unknown = new Dictionary<string, string>(original) { ["EXTRA"] = "ID,Value\n1,A\n1,B" };
        Check(fixture.Validate(unknown).Any(x => x.Sheet == "EXTRA" && x.Code == CsvValidationCode.DuplicateKey), "unknown schema still receives generic ID checks");

        var text = new CountingTextSource();
        var paths = Directory.GetFiles(Path.Combine(root, "Assets/11.CSV"), "*.csv")
            .Select(path => new KeyValuePair<string, string>(Path.GetFileNameWithoutExtension(path), path)).ToArray();
        var diskStore = new CsvDataStore();
        Task<CsvDataSet> LoadFiles(CancellationToken token) => CsvDataSet.LoadAsync(paths, new CsvLoader(text, new CsvParser()), token);
        await diskStore.InitializeAsync(LoadFiles, validator, CancellationToken.None);
        validator.Validate(diskStore.Data); validator.Validate(diskStore.Data);
        diskStore.MapTable("PUZZLE", row => new PuzzleData(row));
        await diskStore.InitializeAsync(LoadFiles, validator, CancellationToken.None);
        Check(text.Reads.Count == paths.Length && text.Reads.Values.All(x => x == 1), "actual files are read once across initialization, validation and mapping");
        var documents = data.Documents.ToDictionary(x => x.Key, x => x.Value);
        var immutable = new CsvDataSet(documents); documents.Clear();
        Check(immutable.Documents.Count == original.Count, "snapshot owns a read-only copy of the document registry");

        string scratch = Path.Combine(root, "Temp/CsvValidationTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);
        string invalidUtf8 = Path.Combine(scratch, "BAD.csv"), broken = Path.Combine(scratch, "BROKEN.csv");
        await File.WriteAllBytesAsync(invalidUtf8, new byte[] { 0xFF, 0xFE, 0xFF });
        await File.WriteAllTextAsync(broken, "ID\n\"unclosed");
        var badRead = await CsvDataSet.LoadAsync(new[] { new KeyValuePair<string, string>("BAD", invalidUtf8), new KeyValuePair<string, string>("BROKEN", broken) },
            new CsvLoader(new Utf8CsvFileSource(), new CsvParser()), CancellationToken.None);
        Check(badRead.LoadIssues.Any(x => x.Code == CsvValidationCode.ReadFailed) && badRead.LoadIssues.Any(x => x.Code == CsvValidationCode.ParseError) && !validator.Validate(badRead).IsValid,
            "load errors survive in memory and prevent registration");
        Console.WriteLine($"{passed} memory pipeline checks passed.");
    }
    private sealed class PuzzleData
    {
        public int Id { get; }
        public int SlotCount { get; }
        public PuzzleData(CsvRow row) { Id = int.Parse(row.Get("ID"), CultureInfo.InvariantCulture); SlotCount = int.Parse(row.Get("SlotCount"), CultureInfo.InvariantCulture); }
    }
    private sealed class PuzzleConsumer
    {
        public IReadOnlyList<PuzzleData> Puzzles { get; }
        public PuzzleConsumer(IReadOnlyList<PuzzleData> puzzles) => Puzzles = puzzles;
    }
    private sealed class MarkerRule : ICsvValidationRule
    { public void Validate(CsvValidationContext context) => context.Warn(CsvValidationCode.UnknownSchema, "TEST", 1, "", "injected"); }
    private sealed class MissingResources : ICsvResourceResolver
    { public bool Exists(string path, CsvResourceKind kind) => false; }
    private sealed class CountingTextSource : ICsvTextSource
    {
        private readonly Utf8CsvFileSource source = new Utf8CsvFileSource();
        public readonly Dictionary<string, int> Reads = new Dictionary<string, int>();
        public Task<string> ReadTextAsync(string name, CancellationToken token) { Reads[name] = Reads.GetValueOrDefault(name) + 1; return source.ReadTextAsync(name, token); }
    }
}
