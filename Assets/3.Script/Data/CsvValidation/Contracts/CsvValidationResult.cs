using System;
using System.Collections.Generic;
using System.Linq;

namespace EscapeGame.Data.CsvValidation.Contracts
{
    public sealed class CsvValidationReport
    {
        public CsvDataSet Data
        {
            get;
        }
        public int FileCount => Data.FileCount;
        public IReadOnlyList<CsvValidationIssue> Issues
        {
            get;
        }
        public int ErrorCount
        {
            get;
        }
        public int WarningCount => Issues.Count - ErrorCount;
        public bool IsValid => ErrorCount == 0;
        internal CsvValidationReport(CsvDataSet data, IEnumerable<CsvValidationIssue> issues)
        {
            Data = data;
            Issues = Array.AsReadOnly(issues.ToArray());
            ErrorCount = Issues.Count(x => x.Severity == CsvValidationSeverity.Error);
        }
    }
}

namespace EscapeGame.Data.CsvValidation.Contracts
{
    public enum CsvValidationSeverity
    {
        Error, Warning
    }
    public enum CsvValidationCode
    {
        ReadFailed, ParseError, UnknownSchema, MissingColumn, UnknownColumn, EmptyTable,
        EmptyValue, Whitespace, InvalidValue, DuplicateKey, MissingRegistry,
        MissingTable, UnknownSheet, MissingReference, InactiveReference, UnregisteredTable,
        RoomMissing, RoomCycle, SlotCount, SlotRange, PuzzleRoom, InputRoom, ConsoleSlots,
        InvalidDistance, InvalidLetters, MissingResource, RoomStartCount, RoomDisconnected
    }

    // 결과는 불변 값입니다. 사용자 문구는 Presentation에서 해석합니다.
    public sealed class CsvValidationIssue
    {
        public CsvValidationSeverity Severity
        {
            get;
        }
        public CsvValidationCode Code
        {
            get;
        }
        public string Sheet
        {
            get;
        }
        public int Line
        {
            get;
        }
        public string Column
        {
            get;
        }
        public IReadOnlyList<string> Details
        {
            get;
        }

        public CsvValidationIssue(CsvValidationCode code, string sheet, int line, string column,
            CsvValidationSeverity severity = CsvValidationSeverity.Error, params string[] details)
        {
            Code = code;
            Sheet = sheet;
            Line = line;
            Column = column;
            Severity = severity;
            Details = Array.AsReadOnly((details ?? Array.Empty<string>()).ToArray());
        }
        public override string ToString() => $"[{Severity}] {Sheet}.csv:{Line} [{Column}] {Code} {string.Join(", ", Details)}";
    }
}
