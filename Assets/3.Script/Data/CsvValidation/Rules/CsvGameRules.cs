using EscapeGame.Data.Csv;
using EscapeGame.Data.CsvValidation.Contracts;
using EscapeGame.Data.CsvValidation.Runtime;
using EscapeGame.Data.CsvValidation.Schema;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace EscapeGame.Data.CsvValidation.Rules
{
    public sealed class CsvRoomRule : ICsvValidationRule
    {
        public void Validate(CsvValidationContext context)
        {
            var rooms = context.Tagged(CsvTableTags.Room).ToArray();
            foreach (var table in rooms)
                foreach (var row in table.Rows)
                {
                    context.Token.ThrowIfCancellationRequested();
                    var visited = new HashSet<string>(StringComparer.Ordinal) { row.Get(table.Key) };
                    string next = row.Get("Next");
                    while (next != "0" && table.Find(next) != null)
                    {
                        context.Token.ThrowIfCancellationRequested();
                        if (!visited.Add(next)) { context.Add(CsvValidationCode.RoomCycle, table.Name, row.SourceLine, "Next"); break; }
                        next = table.Find(next).Get("Next");
                    }
                }
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var table in context.Tagged(CsvTableTags.Anchor))
                foreach (var row in table.Rows)
                {
                    context.Token.ThrowIfCancellationRequested();
                    foreach (var room in rooms.Where(x => row.Get("PlayerType") == "ALL" || x.Definition.Player == row.Get("PlayerType")))
                        if (!room.Rows.Any(x => x.Get("RoomNo") == row.Get("RoomNo")))
                            context.Add(CsvValidationCode.RoomMissing, table.Name, row.SourceLine, "RoomNo", room.Name, row.Get("RoomNo"));
                    string key = string.Join("\u001f", row.Get("RoomNo"), row.Get("PlayerType"), row.Get("AnchorID"));
                    if (!names.Add(key)) context.Add(CsvValidationCode.DuplicateKey, table.Name, row.SourceLine, "AnchorID", key);
                }
            CheckPlacement(context, CsvTableTags.Arrow, "Order");
            CheckPlacement(context, CsvTableTags.Wire, "Port");
        }
        private static void CheckPlacement(CsvValidationContext context, string tag, string column)
        {
            var values = new HashSet<string>(StringComparer.Ordinal);
            foreach (var table in context.Tagged(tag))
                foreach (var row in table.Rows)
                {
                    var anchor = context.Tagged(CsvTableTags.Anchor).Where(x => x.Definition.Kind == "NonInteract")
                        .Select(x => x.Find(row.Get("AnchorRef"))).FirstOrDefault(x => x != null);
                    if (anchor == null) continue;
                    string key = string.Join("\u001f", anchor.Get("RoomNo"), anchor.Get("PlayerType"), row.Get(column));
                    if (!values.Add(key)) context.Add(CsvValidationCode.DuplicateKey, table.Name, row.SourceLine, column, key);
                }
        }
    }
}

namespace EscapeGame.Data.CsvValidation.Rules
{
    public sealed class CsvPuzzleRule : ICsvValidationRule
    {
        public void Validate(CsvValidationContext context)
        {
            foreach (var table in context.Tagged(CsvTableTags.Puzzle))
                foreach (var puzzle in table.Rows)
                {
                    context.Token.ThrowIfCancellationRequested();
                    if (!int.TryParse(puzzle.Get("SlotCount"), out int count) || count < 1) continue;
                    var slots = context.Tagged(CsvTableTags.Slot).SelectMany(x => x.Rows.Select(row => (Table: x, Row: row)))
                        .Where(x => x.Row.Get("PuzzleID") == puzzle.Get("ID")).ToArray();
                    if (slots.Length != count) context.Add(CsvValidationCode.SlotCount, table.Name, puzzle.SourceLine, "SlotCount", count.ToString(), slots.Length.ToString());
                    foreach (var slot in slots)
                        if (int.TryParse(slot.Row.Get("Slot"), out int position) && (position < 1 || position > count))
                            context.Add(CsvValidationCode.SlotRange, slot.Table.Name, slot.Row.SourceLine, "Slot", count.ToString());
                    var solver = FindRoom(context, puzzle.Get("SolverRoom"));
                    CheckRoom(context, table, puzzle, "SolverRoom", "Solver");
                    CheckRoom(context, table, puzzle, "ClueRoom", "Clue");
                    var input = context.Tagged(CsvTableTags.Anchor).Where(x => x.Definition.Kind == "Interact")
                        .Select(x => x.Find(puzzle.Get("InputAnchor"))).FirstOrDefault(x => x != null);
                    if (solver != null && input != null)
                    {
                        var room = solver.Find(puzzle.Get("SolverRoom"));
                        if (room.Get("RoomNo") != input.Get("RoomNo") || (input.Get("PlayerType") != solver.Definition.Player && input.Get("PlayerType") != "ALL"))
                            context.Add(CsvValidationCode.InputRoom, table.Name, puzzle.SourceLine, "InputAnchor");
                    }
                    foreach (var console in context.Tagged(CsvTableTags.Console))
                        foreach (var row in console.Rows.Where(x => x.Get("AnchorRef") == puzzle.Get("InputAnchor")))
                            if (row.Get("SlotCount") != puzzle.Get("SlotCount")) context.Add(CsvValidationCode.ConsoleSlots, console.Name, row.SourceLine, "SlotCount");
                }
        }
        private static CsvValidationTable FindRoom(CsvValidationContext context, string id)
            => context.Tagged(CsvTableTags.Room).FirstOrDefault(x => x.Find(id) != null);
        private static void CheckRoom(CsvValidationContext context, CsvValidationTable table, CsvRow puzzle, string column, string role)
        {
            var room = FindRoom(context, puzzle.Get(column))?.Find(puzzle.Get(column));
            if (room != null && (room.Get("PuzzleID") != puzzle.Get("ID") || room.Get("Role") != role))
                context.Add(CsvValidationCode.PuzzleRoom, table.Name, puzzle.SourceLine, column, role);
        }
    }
}

namespace EscapeGame.Data.CsvValidation.Rules
{
    public sealed class CsvValueRelationRule : ICsvValidationRule
    {
        public void Validate(CsvValidationContext context)
        {
            foreach (var table in context.Tagged(CsvTableTags.Config))
                foreach (var row in table.Rows)
                {
                    string type = row.Get("Type"), value = row.Get("Value");
                    bool valid = type == "int" ? int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _) : type != "float" || CsvValidationContext.Number(value, out _);
                    if (!valid) context.Add(CsvValidationCode.InvalidValue, table.Name, row.SourceLine, "Value", value, type);
                }
            foreach (var table in context.Tagged(CsvTableTags.Sound))
                foreach (var row in table.Rows)
                {
                    string min = row.Get("MinDist"), max = row.Get("MaxDist");
                    if ((min.Length == 0) != (max.Length == 0) || (CsvValidationContext.Number(min, out double low) && CsvValidationContext.Number(max, out double high) && low > high))
                        context.Add(CsvValidationCode.InvalidDistance, table.Name, row.SourceLine, "MaxDist");
                }
            foreach (var table in context.Tagged(CsvTableTags.Lock))
                foreach (var row in table.Rows)
                    if (row.Get("DialFace") == "Letter9" && (CsvValidationContext.List(row.Get("DialLetters")).Length != 9 || CsvValidationContext.List(row.Get("DialLetters")).Distinct().Count() != 9))
                        context.Add(CsvValidationCode.InvalidLetters, table.Name, row.SourceLine, "DialLetters");
            foreach (var table in context.Tagged(CsvTableTags.SoundLink))
                foreach (var row in table.Rows)
                {
                    string target = row.Get("TargetSheet");
                    if (target == "Global")
                    {
                        if (row.Get("TargetID") != "0") context.Add(CsvValidationCode.InvalidValue, table.Name, row.SourceLine, "TargetID", row.Get("TargetID"), "Global=0");
                        continue;
                    }
                    var definition = context.Catalog.Find(target);
                    if (definition == null || !new[] { CsvTableTags.Room, CsvTableTags.Prefab, CsvTableTags.Anchor, CsvTableTags.Puzzle }.Contains(definition.Tag))
                        context.Add(CsvValidationCode.UnknownSheet, table.Name, row.SourceLine, "TargetSheet", target);
                    else context.Reference(table, row, "TargetID", context.Tables.Where(x => x.IsUsable && x.Name == target));
                }
        }
    }
}
