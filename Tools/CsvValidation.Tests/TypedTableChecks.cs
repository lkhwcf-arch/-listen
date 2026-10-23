using System.Collections;
using System.Globalization;
using System.Reflection;
using EscapeGame.Data;
using EscapeGame.Data.Csv;
using EscapeGame.Data.CsvValidation.Runtime;
using EscapeGame.Data.Tables;

internal static class TypedTableChecks
{
    public static Task Run(string root, Dictionary<string, string> original, string xml)
    {
        int passed = 0;
        void Check(bool condition, string name)
        {
            if (!condition)
                throw new Exception("FAIL: " + name);
            passed++;
            Console.WriteLine("PASS: " + name);
        }
        var fixture = new TestValidator(xml);
        var validator = new CsvValidator(fixture.Catalog);
        CsvDataStore Store(Dictionary<string, string> sources)
        {
            var result = new CsvDataStore();
            if (!result.TryRegister(fixture.Input(sources), validator, out var report))
                throw new Exception("Invalid fixture: " + string.Join("\n", report.Issues));
            return result;
        }
        var store = Store(original);
        var data = new CsvGameData(store);
        var tableProperties = typeof(CsvGameData).GetProperties().Where(x => x.PropertyType.IsGenericType).ToArray();
        var mappedSheets = new HashSet<string>(StringComparer.Ordinal);
        int checkedCells = 0;
        foreach (var tableProperty in tableProperties)
        {
            Type rowType = tableProperty.PropertyType.GenericTypeArguments[0];
            var mapping = rowType.GetProperty("Table", BindingFlags.Static | BindingFlags.Public).GetValue(null);
            string sheet = (string)mapping.GetType().GetProperty("SheetName").GetValue(mapping);
            if (!mappedSheets.Add(sheet))
                throw new Exception("Duplicate CSV binding: " + sheet);
            var rows = ((IEnumerable)tableProperty.GetValue(data)).Cast<object>().ToArray();
            var sourceRows = data.Source.GetDocument(sheet).Rows;
            if (rows.Length != sourceRows.Count)
                throw new Exception("Row count mismatch: " + sheet);
            foreach (var property in rowType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                if (property.CanWrite)
                    throw new Exception("Writable definition property: " + rowType.Name + "." + property.Name);
            for (int i = 0; i < rows.Length; i++)
                foreach (string column in data.Source.GetDocument(sheet).Headers)
                {
                    string name = column == "FOV" ? "Fov" : column.Replace("ID", "Id");
                    var property = rowType.GetProperty(name);
                    if (property == null)
                        throw new Exception("Unmapped column: " + sheet + "." + column);
                    string raw = sourceRows[i].Get(column);
                    object actual = property.GetValue(rows[i]);
                    Type type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                    bool matches;
                    if (Nullable.GetUnderlyingType(property.PropertyType) != null && raw.Length == 0)
                        matches = actual == null;
                    else if (type == typeof(int))
                        matches = (int)actual == int.Parse(raw, CultureInfo.InvariantCulture);
                    else if (type == typeof(float))
                    {
                        double expected = double.Parse(raw, CultureInfo.InvariantCulture);
                        matches = Math.Abs((float)actual - expected) <= Math.Max(0.000001, Math.Abs(expected) * 0.000001);
                    }
                    else if (type == typeof(bool))
                        matches = (bool)actual == (raw == "Y");
                    else if (type == typeof(string))
                        matches = (string)actual == raw;
                    else
                    {
                        var expected = raw.Length == 0 || (sheet == "REGISTRY" && raw == "-") ? Array.Empty<string>() : raw.Split(',').Select(x => x.Trim()).ToArray();
                        matches = sheet == "PREFAB" && column == "Components" ? ((IEnumerable<int>)actual).SequenceEqual(raw.Length == 0 ? Array.Empty<int>() : raw.Split('|').Select(int.Parse)) : ((IEnumerable<string>)actual).SequenceEqual(expected);
                    }
                    if (!matches)
                        throw new Exception($"Value mismatch: {sheet}:{sourceRows[i].SourceLine} [{column}]");
                    checkedCells++;
                }
        }
        Check(mappedSheets.SetEquals(original.Keys) && tableProperties.Length == 23, "all 23 CSV files bind to distinct data classes");
        Check(checkedCells > 1000, $"all {checkedCells} source cells are preserved in typed properties");
        Check(ReferenceEquals(store.Data, data.Source), "typed tables use the registered in-memory snapshot");
        var repeated = new CsvGameData(store);
        Check(ReferenceEquals(data.Puzzles, repeated.Puzzles) && ReferenceEquals(data.CharacterStats, repeated.CharacterStats), "creating the table bundle reuses cached mappings");

        var consumer = new PuzzleConsumer(data.Puzzles, data.PuzzleSlots);
        Check(ReferenceEquals(consumer.Puzzles, store.GetTable(PuzzleData.Table)) && consumer.Puzzles.First(x => x.Id == 5001).SlotCount == 4,
            "real data classes can be injected without filenames or raw CSV rows");
        var onlyOneTable = Store(original);
        var mapped = onlyOneTable.MapTable(PuzzleData.Table);
        Check(onlyOneTable.TryGetTable(PuzzleData.Table, out var cached) && ReferenceEquals(mapped, cached), "typed mapping descriptor supports independent table registration and retrieval");

        Check(data.Puzzles.Where(x => x.Id == 5007 || x.Id == 5008).All(x => x.ClueRoom == null), "empty optional room IDs remain null");
        Check(data.Sounds.First(x => x.Id == 15001).MinDist == null && data.Sounds.First(x => x.Id == 15003).MinDist == 1f, "optional sound distances distinguish unset and actual values");
        Check(data.NonInteractAnchors.Any(x => !x.Active) && data.Cards.Any(x => !x.IsAnswer), "Y/N flags convert to bool without removing inactive rows");
        Check(data.PuzzleSlots.First().Answer == "3" && data.PuzzleSlots.Any(x => x.Answer == "Right"), "puzzle answers remain strings for mixed number/direction meanings");
        Check(data.Components.First(x => x.Id == 14002).Params == "CamOffset=0,0.3,-0.5", "component parameter strings preserve embedded commas");
        Check(data.Consoles.First().Buttons.SequenceEqual(new[] { "Up", "Down", "Left", "Right" }), "button list parses once into read-only values");
        Check(data.Locks.First(x => x.AnchorRef == 2054).DialLetters.Count == 9 && data.Locks.First().DialLetters.Count == 0, "dial letter lists preserve optional emptiness");
        Check(data.Registry.First().Parent.Count == 0 && data.Registry.First(x => x.Sheet == "ROOM_A").Uses.SequenceEqual(new[] { "PREFAB", "PUZZLE" }), "registry lists share delimiter conversion and recognize dash as empty");
        Check(data.RoomsA.Last().Next == 0 && data.RoomsA.First().TargetMs == 120000, "logical IDs and milliseconds remain integers including zero sentinel");
        Check(data.CharacterStats.First().EyeHeight == 1.6f && data.Sounds.First().Volume == 0.5f && data.Wires.First().LengthM == 1.4f, "Unity settings preserve decimal values as floats");
        Check(data.RoomsA[0] is RoomData && data.RoomsB[0] is RoomData && data.InteractAnchors[0] is AnchorData && data.NonInteractAnchors[0] is AnchorData,
            "identical room and anchor structures share their field conversion");

        Check(data.GameConfigs.First(x => x.Id == 16001).AsInt() == 900000 && data.GameConfigs.First(x => x.Id == 16030).AsFloat() == 0.75f,
            "game config values expose accessors matching their declared type");
        bool rejected = false;
        try
        {
            data.GameConfigs.First(x => x.Id == 16002).AsInt();
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }
        Check(rejected, "color/string config values cannot be silently read as numbers");
        var signed = GameConfigData.Table.Convert(Row("ID,Value,Type,Unit\n1,-3,int,"));
        Check(signed.AsInt() == -3, "signed config values use the config integer policy");

        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            var localized = new CsvGameData(Store(original));
            Check(localized.CharacterStats.First().EyeHeight == 1.6f && localized.GameConfigs.First(x => x.Id == 16030).AsFloat() == 0.75f,
                "typed numeric conversion is independent of operating system culture");
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }

        var optional = new Dictionary<string, string>(original);
        optional.Remove("SOUND");
        optional.Remove("SOUND_LINK");
        var optionalData = new CsvGameData(Store(optional));
        Check(optionalData.Sounds.Count == 0 && optionalData.SoundLinks.Count == 0 && optionalData.Puzzles.Count == 8, "missing optional tables produce empty collections");
        var inactive = new Dictionary<string, string>(original);
        var registry = new CsvParser().Read(inactive["REGISTRY"]);
        string Quote(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
        inactive["REGISTRY"] = string.Join(",", registry.Headers.Select(Quote)) + "\n" + string.Join("\n", registry.Rows.Select(row =>
            string.Join(",", registry.Headers.Select(column => Quote(column == "Active" && (row.Get("Sheet") == "SOUND" || row.Get("Sheet") == "SOUND_LINK") ? "N" : row.Get(column))))));
        var inactiveStore = Store(inactive);
        var inactiveData = new CsvGameData(inactiveStore);
        Check(inactiveData.Sounds.Count == 0 && inactiveData.SoundLinks.Count == 0 && inactiveData.Source.GetDocument("SOUND").Rows.Count > 0,
            "explicitly inactive tables keep raw validation input but publish no runtime rows");
        Check(!inactiveStore.TryGetTable(SoundData.Table, out _) && !inactiveStore.TryGetTable(SoundLinkData.Table, out _),
            "inactive tables do not allocate typed mapping caches");
        inactiveStore.MapTable(SoundData.Table);
        Check(new CsvGameData(inactiveStore).Sounds.Count == 0, "explicit tool mapping does not override inactive runtime policy");
        inactive["REGISTRY"] = string.Join(",", registry.Headers.Select(Quote)) + "\n" + string.Join("\n", registry.Rows
            .Where(row => row.Get("Sheet") != "SOUND" && row.Get("Sheet") != "SOUND_LINK")
            .Select(row => string.Join(",", registry.Headers.Select(column => Quote(row.Get(column))))));
        var unregistered = Store(inactive);
        var unregisteredData = new CsvGameData(unregistered);
        Check(unregisteredData.Sounds.Count == 0 && unregisteredData.SoundLinks.Count == 0 && !unregistered.TryGetTable(SoundData.Table, out _),
            "unregistered known tables are not exposed as runtime definitions");
        var oldPuzzles = data.Puzzles;
        store.TryRegister(fixture.Input(original), validator, out _);
        var replacement = new CsvGameData(store);
        Check(!ReferenceEquals(oldPuzzles, replacement.Puzzles) && consumer.Puzzles[0].Id == 5001, "replacement creates new typed tables while prior injected data stays stable");

        rejected = false;
        try
        {
            CardData.Table.Convert(Row("ID,AnchorRef,CardNo,TopMark,BottomMark,IsAnswer\n1,1,1,A,1,yes"));
        }
        catch (FormatException error)
        {
            rejected = error.Message.Contains("CARD.csv:2 [IsAnswer]");
        }
        Check(rejected, "conversion failures include CSV filename, row and column");
        rejected = false;
        try
        {
            WireData.Table.Convert(Row("AnchorRef,Color,LengthM,IsAnswer,Port\n1,Red,1e100,Y,1"));
        }
        catch (FormatException error)
        {
            rejected = error.Message.Contains("WIRE.csv:2 [LengthM]");
        }
        Check(rejected, "nonrepresentable float values fail with source location");
        rejected = false;
        try
        {
            new CsvGameData(new CsvDataStore());
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }
        Check(rejected, "typed bundle requires validated store registration");
        Console.WriteLine($"{passed} typed table checks passed.");
        return Task.CompletedTask;
    }
    private static CsvRow Row(string csv) => new CsvParser().Read(csv).Rows[0];
    private sealed class PuzzleConsumer
    {
        public IReadOnlyList<PuzzleData> Puzzles
        {
            get;
        }
        public IReadOnlyList<PuzzleSlotData> Slots
        {
            get;
        }
        public PuzzleConsumer(IReadOnlyList<PuzzleData> puzzles, IReadOnlyList<PuzzleSlotData> slots)
        {
            Puzzles = puzzles;
            Slots = slots;
        }
    }
}
