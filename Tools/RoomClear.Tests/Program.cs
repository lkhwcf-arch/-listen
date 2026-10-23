using EscapeGame.Data;
using EscapeGame.Data.Csv;
using EscapeGame.Data.CsvValidation.Runtime;
using EscapeGame.Data.CsvValidation.Schema;
using EscapeGame.Data.Tables;
using EscapeGame.Editor;
using EscapeGame.World;

string root = Path.GetFullPath(args.Length == 0 ? "../.." : args[0]);
var source = new CsvDataSet(Directory.GetFiles(Path.Combine(root, "Assets/11.CSV"), "*.csv")
    .ToDictionary(Path.GetFileNameWithoutExtension, file => new CsvParser().Read(File.ReadAllText(file), Path.GetFileNameWithoutExtension(file))));
var schema = CsvValidationCatalog.Read(File.ReadAllText(Path.Combine(root, "Assets/3.Script/Data/CsvValidation/Configuration/CsvValidationRules.xml")));
var store = new CsvDataStore();
if (!store.TryRegister(source, new CsvValidator(schema), out var report)) throw new Exception("CSV validation failed");
var data = new CsvGameData(store);
int passed = 0;
void Check(bool ok, string label)
{
    if (!ok)
        throw new Exception(label);
    passed++;
}
void Reject(Action command, string label)
{
    bool rejected = false;
    try
    {
        command();
    }
    catch (InvalidOperationException)
    {
        rejected = true;
    }
    Check(rejected, label);
}
foreach (string role in new[] { "A", "B" })
{
    var plan = new RoomLoadPlan(data, role);
    var state = new DeveloperRoomProgression(plan);
    int events = 0;
    state.Changed += () =>
    {
        events++;
        Check(state.IsCleared(plan.First), "event sees committed clear state");
    };
    Check(state.IsUnlocked(plan.First) && !state.IsCleared(plan.First), role + " initial room only");
    Check(state.Rooms.Count(row => state.IsUnlocked(row)) == 1, role + " future rooms locked");
    Reject(() => state.Complete(99), role + " unknown room rejected");
    Reject(() => state.Complete(2), role + " locked room rejected");
    Check(events == 0, role + " rejected commands emit no event");
    var next = state.NextForClear(1);
    Check(next.Id == plan.First.Next && next.RoomNo == 2, role + " follows CSV Next ID");
    Check(!state.IsUnlocked(next) && !state.IsCleared(plan.First), role + " preparation cannot commit state");
    // If preview creation fails here, Complete is not called: the same request remains available.
    Check(state.NextForClear(1).Id == next.Id && events == 0, role + " failed preparation remains retryable");
    Check(state.Complete(1) && state.IsUnlocked(next), role + " commit unlocks next");
    Check(!state.Complete(1) && events == 1, role + " duplicate clear has no effect");
    Check(state.IsUnlocked(plan.First), role + " previous room retained");
    foreach (var room in plan.Rooms.OrderBy(row => row.RoomNo).Skip(1))
        Check(state.Complete(room.RoomNo), role + " clears room " + room.RoomNo);
    var last = plan.Rooms.Single(row => row.Next == 0);
    Check(state.NextForClear(last.RoomNo) == null && !state.Complete(last.RoomNo), role + " terminal room has no successor");
    Check(events == plan.Rooms.Count && state.Rooms.All(row => state.IsCleared(row) && state.IsUnlocked(row)), role + " full chain preserved");
    var fresh = new DeveloperRoomProgression(plan);
    Check(!fresh.IsCleared(plan.First) && fresh.Rooms.Count(row => fresh.IsUnlocked(row)) == 1, role + " restart resets progress");
}
Check(data.RoomsA.First().Id != data.RoomsB.First().Id, "role IDs remain distinct");
Console.WriteLine($"{passed} developer room progression checks passed; current CSV warnings: {report.WarningCount}.");
