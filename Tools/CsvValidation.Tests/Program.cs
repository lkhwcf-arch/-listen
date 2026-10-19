using EscapeGame.Data.Csv;
using EscapeGame.Data.CsvValidation;
using EscapeGame.Data.CsvValidation.Contracts;

string root = args.Length > 0 ? Path.GetFullPath(args[0]) : Directory.GetCurrentDirectory();
var original = Directory.GetFiles(Path.Combine(root, "Assets", "11.CSV"), "*.csv")
    .ToDictionary(Path.GetFileNameWithoutExtension, File.ReadAllText, StringComparer.Ordinal);
int passed = 0;
string catalogXml = File.ReadAllText(Path.Combine(root, "Assets/3.Script/Data/CsvValidation/Configuration/CsvValidationRules.xml"));
TestValidator CreateValidator() => new TestValidator(catalogXml);
var baseline = CreateValidator().Validate(original);
foreach (var issue in baseline) Console.WriteLine(issue);
if (baseline.Any(x => x.Severity == CsvValidationSeverity.Error)) throw new Exception("현재 데이터 기본 검사 실패");
Console.WriteLine($"Baseline: {original.Count} files, {baseline.Count} issues");

void Check(string name, Action<Dictionary<string, string>> change, Func<CsvValidationIssue, bool> expected)
{
    var input = new Dictionary<string, string>(original, StringComparer.Ordinal);
    change(input);
    var result = CreateValidator().Validate(input);
    if (!result.Any(expected)) throw new Exception("FAIL: " + name);
    passed++;
    Console.WriteLine("PASS: " + name);
}
void Cell(Dictionary<string, string> input, string sheet, int rowIndex, string column, string value)
{
    var document = new CsvParser().Read(input[sheet]);
    string Quote(string text) => "\"" + text.Replace("\"", "\"\"") + "\"";
    input[sheet] = string.Join(",", document.Headers.Select(Quote)) + "\n" + string.Join("\n", document.Rows.Select((row, index) =>
        string.Join(",", document.Headers.Select(header => Quote(index == rowIndex && header == column ? value : row.Get(header))))));
}
Func<CsvValidationIssue, bool> At(string sheet, string column) => x => x.Sheet == sheet && x.Column == column && x.Severity == CsvValidationSeverity.Error;
Check("duplicate ID", x => Cell(x, "CARD", 1, "ID", "3001"), At("CARD", "ID"));
Check("missing header", x => x["CARD"] = x["CARD"].Replace("\"TopMark\"", "\"Typo\""), At("CARD", "TopMark"));
Check("invalid quote", x => x["CARD"] += "\n\"unclosed", x => x.Sheet == "CARD" && x.Code == CsvValidationCode.ParseError && x.Details.Contains("UnclosedQuote"));
Check("field count", x => x["CARD"] += "\n1,2", x => x.Sheet == "CARD" && x.Code == CsvValidationCode.ParseError && x.Details.Contains("FieldCountMismatch"));
Check("bad flag", x => Cell(x, "CARD", 0, "IsAnswer", "yes"), At("CARD", "IsAnswer"));
Check("blank required", x => Cell(x, "CARD", 0, "TopMark", ""), At("CARD", "TopMark"));
Check("integer format", x => Cell(x, "CARD", 0, "CardNo", "1.5"), At("CARD", "CardNo"));
Check("nonfinite float", x => Cell(x, "WIRE", 0, "LengthM", "NaN"), At("WIRE", "LengthM"));
Check("config type", x => Cell(x, "GAME_CONFIG", 0, "Value", "abc"), At("GAME_CONFIG", "Value"));
Check("missing prefab", x => Cell(x, "ANCHOR_INTERACT", 0, "PrefabID", "99999"), At("ANCHOR_INTERACT", "PrefabID"));
Check("parent anchor kind", x => Cell(x, "ARROW", 0, "AnchorRef", "2030"), At("ARROW", "AnchorRef"));
Check("missing required sheet", x => x.Remove("LOCK"), x => x.Sheet == "REGISTRY" && x.Details.Contains("LOCK"));
Check("missing puzzle", x => Cell(x, "PUZZLE_SLOT", 0, "PuzzleID", "99999"), At("PUZZLE_SLOT", "PuzzleID"));
Check("slot count", x => Cell(x, "PUZZLE", 0, "SlotCount", "5"), At("PUZZLE", "SlotCount"));
Check("duplicate slot", x => Cell(x, "PUZZLE_SLOT", 1, "Slot", "1"), At("PUZZLE_SLOT", "PuzzleID,Slot"));
Check("slot range", x => Cell(x, "PUZZLE_SLOT", 0, "Slot", "99"), At("PUZZLE_SLOT", "Slot"));
Check("wrong input room", x => Cell(x, "PUZZLE", 0, "InputAnchor", "2034"), At("PUZZLE", "InputAnchor"));
Check("global sound target", x => Cell(x, "SOUND_LINK", 0, "TargetID", "1"), At("SOUND_LINK", "TargetID"));
Check("missing sound", x => Cell(x, "SOUND_LINK", 0, "SoundID", "99999"), At("SOUND_LINK", "SoundID"));
Check("cross room Next", x => Cell(x, "ROOM_A", 0, "Next", "1002"), At("ROOM_A", "Next"));
Check("room cycle", x => Cell(x, "ROOM_A", 1, "Next", "1001"), At("ROOM_A", "Next"));
Check("ALL requires both rooms", x => Cell(x, "ANCHOR_INTERACT", 0, "RoomNo", "99"), At("ANCHOR_INTERACT", "RoomNo"));
Check("volume range", x => Cell(x, "SOUND", 0, "Volume", "2"), At("SOUND", "Volume"));
Check("distance range", x => Cell(x, "SOUND", 1, "MinDist", "9"), At("SOUND", "MaxDist"));
Check("malformed registry does not crash", x => x["REGISTRY"] = "ID\n1", At("REGISTRY", "Sheet"));
var validator = CreateValidator();
var first = validator.Validate(new Dictionary<string, string>());
var second = validator.Validate(original);
if (first.Count == 0 || second.Count != baseline.Count) throw new Exception("Repeated validation leaked state");
Console.WriteLine($"{passed + 1} checks passed (including repeated validation).");
await MemoryPipelineChecks.Run(root, original, catalogXml);
await TypedTableChecks.Run(root, original, catalogXml);
await WorldChecks.Run(original, catalogXml);
await ContentRegressionChecks.Run(original, catalogXml);
