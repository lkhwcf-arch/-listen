using EscapeGame.Data;
using EscapeGame.Data.Csv;
using EscapeGame.Data.CsvValidation.Runtime;
using EscapeGame.Data.CsvValidation.Schema;
using EscapeGame.Data.Tables;
using EscapeGame.Player;

string root = Path.GetFullPath(args[0]);
var source = Directory.GetFiles(Path.Combine(root, "Assets/11.CSV"), "*.csv")
    .ToDictionary(Path.GetFileNameWithoutExtension, File.ReadAllText);
var schema = CsvValidationCatalog.Read(File.ReadAllText(Path.Combine(root, "Assets/3.Script/Data/CsvValidation/Configuration/CsvValidationRules.xml")));
CsvGameData Load(Dictionary<string, string> raw)
{
    var set = new CsvDataSet(raw.ToDictionary(x => x.Key, x => new CsvParser().Read(x.Value, x.Key)));
    var store = new CsvDataStore();
    if (!store.TryRegister(set, new CsvValidator(schema), out var report)) throw new InvalidOperationException("CSV validation failed");
    return new CsvGameData(store);
}
Dictionary<string, string> Cell(string sheet, string id, string column, string value)
{
    var copy = new Dictionary<string, string>(source);
    var doc = new CsvParser().Read(copy[sheet], sheet);
    string Quote(string s) => "\"" + s.Replace("\"", "\"\"") + "\"";
    copy[sheet] = string.Join(",", doc.Headers.Select(Quote)) + "\n" + string.Join("\n", doc.Rows.Select(row =>
        string.Join(",", doc.Headers.Select(header => Quote(row.Get("ID") == id && header == column ? value : row.Get(header))))));
    return copy;
}
int checks = 0;
void Check(bool ok, string label) { if (!ok) throw new Exception(label); checks++; }
void Reject(Action action, string label)
{
    bool rejected = false;
    try { action(); } catch (ArgumentException) { rejected = true; } catch (InvalidOperationException) { rejected = true; }
    Check(rejected, label);
}
var data = Load(source);
var a = new PlayerSpawnPlan(data, "A", 13001, 16010);
var b = new PlayerSpawnPlan(data, "B", 13001, 16010);
Check(a.Anchor.Id == 2001 && a.Anchor.AnchorId == "R01_Player", "A uses declared common spawn");
Check(b.Anchor.Id == a.Anchor.Id && b.Anchor.AnchorId == a.Anchor.AnchorId, "B uses same common spawn");
Check(a.Room.Id == 1001 && b.Room.Id == 1002, "role room IDs stay distinct");
Check(!a.UsesCylinder && b.UsesCylinder, "model type remains role-specific");
Check(a.EyeHeightMm == 1600 && a.CapsuleHeightMm == 1800, "height units normalized to integer mm");
Check(a.CapsuleRadiusMm == 300 && a.StepOffsetMm == 300, "fractional meter settings normalize correctly");
Check(a.FovMilliDegrees == 70000, "FOV uses integer millidegrees");
Check(b.EyeHeightMm == a.EyeHeightMm, "role does not select a different character preset");
Reject(() => new PlayerSpawnPlan(data, "C", 13001, 16010), "unknown role rejected");
Reject(() => new PlayerSpawnPlan(data, "A", 0, 16010), "invalid ID rejected");
Reject(() => new PlayerSpawnPlan(data, "A", 13001, 99999), "missing profile selection rejected");
Reject(() => new PlayerSpawnPlan(Load(Cell("ANCHOR_NONINTERACT", "2001", "Active", "N")), "A", 13001, 16010), "inactive spawn rejected");
Reject(() => new PlayerSpawnPlan(Load(Cell("ANCHOR_NONINTERACT", "2001", "PlayerType", "B")), "A", 13001, 16010), "other-role spawn rejected");
Reject(() => new PlayerSpawnPlan(Load(Cell("ANCHOR_NONINTERACT", "2001", "RoomNo", "2")), "A", 13001, 16010), "other-room spawn rejected");
var duplicate = new Dictionary<string, string>(source);
duplicate["ANCHOR_NONINTERACT"] += "\n\"2999\",\"1\",\"A\",\"R01_ExtraPlayer\",\"13001\",\"Y\"\n";
Reject(() => new PlayerSpawnPlan(Load(duplicate), "A", 13001, 16010), "common plus role-specific spawn is ambiguous");
Check(new PlayerSpawnPlan(Load(duplicate), "B", 13001, 16010).Anchor.Id == 2001, "A-only extra spawn cannot affect B");
Check(new PlayerSpawnPlan(Load(Cell("CHARACTER_STAT", "17001", "EyeHeight", "1.25")), "A", 13001, 16010).EyeHeightMm == 1250, "settings come from CSV");
Check(new PlayerSpawnPlan(Load(Cell("ANCHOR_NONINTERACT", "2001", "AnchorID", "RenamedSpawn")), "A", 13001, 16010).Anchor.AnchorId == "RenamedSpawn", "anchor name is never hardcoded");
Reject(() => new PlayerSpawnPlan(Load(Cell("CHARACTER_STAT", "17001", "EyeHeight", "2.0")), "A", 13001, 16010), "eye outside capsule rejected");
Check(source["ANCHOR_NONINTERACT"].Contains("R01_Player"), "test mutations leave source snapshot unchanged");
var profile = a.Character;
Check(profile.ProfileId == 17001 && profile.WalkSpeedMmPerSecond == 3000, "selected speed comes from GAME_CONFIG and CSV");
Check(profile.AccelerationMmPerSecondSquared == 10000 && profile.DecelerationMmPerSecondSquared == 12000, "acceleration units normalized once");
var fast = PlayerCharacterSettings.Select(Load(Cell("GAME_CONFIG", "16010", "Value", "17002")), 16010);
Check(fast.WalkSpeedMmPerSecond == 4500 && fast.AccelerationMmPerSecondSquared == 14000 && fast.DecelerationMmPerSecondSquared == 16000, "QA profile selection changes all movement values");
Reject(() => PlayerCharacterSettings.Select(Load(Cell("CHARACTER_STAT", "17001", "Acceleration", "0")), 16010), "nonfunctional zero acceleration fails early");
Reject(() => PlayerCharacterSettings.Select(Load(Cell("CHARACTER_STAT", "17001", "WalkSpeed", "1000")), 16010), "unsafe fixed point range rejected");
Reject(() => new PlayerMoveCommand(1001, 0), "invalid input axis rejected");
PlayerMovementMotor Motor() => new PlayerMovementMotor(profile, -9810, 50000);
void Advance(PlayerMovementMotor m, PlayerMoveCommand command, int totalMs)
{ while (totalMs > 0) { int step = Math.Min(totalMs, 100); m.Tick(command, step); totalMs -= step; } }
var forward = new PlayerMoveCommand(0, 1000);
var diagonal = new PlayerMoveCommand(1000, 1000);
var motor = Motor();
var first = motor.Tick(forward, 100);
Check(motor.ForwardVelocityUmPerSecond == 1000000 && first.ForwardMm == 50, "100ms at 10m/s2 accelerates to 1m/s and travels 50mm");
Advance(motor, forward, 200);
Check(motor.ForwardVelocityUmPerSecond == 3000000, "speed reaches cap at 300ms");
Check(motor.Tick(forward, 100).ForwardMm == 300, "cruise 3m/s travels 300mm in 100ms");
Advance(motor, default, 250);
Check(motor.ForwardVelocityUmPerSecond == 0, "release uses 12m/s2 and stops in 250ms");
Check(motor.Tick(default, 100).ForwardMm == 0, "idle remains stopped");
motor = Motor(); Advance(motor, diagonal, 1000);
long vx = motor.RightVelocityUmPerSecond, vz = motor.ForwardVelocityUmPerSecond;
Check(vx == vz && vx * vx + vz * vz <= 3000000L * 3000000, "diagonal velocity never exceeds walk speed");
Advance(motor, new PlayerMoveCommand(0, -1000), 1000);
Check(motor.ForwardVelocityUmPerSecond == -3000000 && motor.RightVelocityUmPerSecond == 0, "reversal reaches new target without drift");
var split = Motor(); var full = Motor();
int splitX = 0, splitY = 0, splitZ = 0, fullX = 0, fullY = 0, fullZ = 0;
for (int i = 0; i < 1000; i++) { var s = split.Tick(diagonal, 1); splitX += s.RightMm; splitY += s.UpMm; splitZ += s.ForwardMm; }
for (int i = 0; i < 10; i++) { var s = full.Tick(diagonal, 100); fullX += s.RightMm; fullY += s.UpMm; fullZ += s.ForwardMm; }
Check(splitX == fullX && splitZ == fullZ && splitY == fullY, "remainders survive frame splitting including gravity");
Check(split.RightVelocityUmPerSecond == full.RightVelocityUmPerSecond, "same integer-ms input ordering yields same velocity");
motor = Motor();
var fall = motor.Tick(default, 100);
Check(fall.UpMm == -49 && motor.UpVelocityUmPerSecond == -981000, "gravity from project adapter integrates in integer units");
motor.OnGrounded();
Check(motor.UpVelocityUmPerSecond == 0, "ground contact clears falling velocity");
Advance(motor, default, 10000);
Check(motor.UpVelocityUmPerSecond == -50000000, "terminal fall speed bounded");
motor.Reset();
Check(motor.UpVelocityUmPerSecond == 0 && motor.RightVelocityUmPerSecond == 0 && motor.ForwardVelocityUmPerSecond == 0, "pause/disable reset clears all movement state");
var clean = Motor().Tick(forward, 100); var reset = motor.Tick(forward, 100);
Check(reset.ForwardMm == clean.ForwardMm && reset.UpMm == clean.UpMm, "reset clears sub-mm residue too");
Reject(() => motor.Tick(default, 101), "oversized frame cannot bypass driver clamp");
Reject(() => motor.Tick(default, -1), "negative time rejected");
Reject(() => new PlayerMovementMotor(profile, 0, 50000), "non-downward gravity rejected");
Check(motor.Tick(default, 0).ForwardMm == 0, "zero elapsed time creates no movement");
var qa = new PlayerMovementMotor(fast, -9810, 50000); Advance(qa, forward, 1000);
Check(qa.Tick(forward, 100).ForwardMm == 450, "QA profile cruises at 4.5m/s");
Console.WriteLine($"{checks} player spawn and integer movement checks passed.");
