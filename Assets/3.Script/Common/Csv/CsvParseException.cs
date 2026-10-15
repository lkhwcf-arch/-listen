using System;

namespace EscapeGame.Data.Csv
{
    public enum CsvErrorCode
    {
        MissingHeader = 1, InvalidQuote = 2, UnclosedQuote = 3,
        EmptyHeader = 4, DuplicateHeader = 5, FieldCountMismatch = 6
    }

    public sealed class CsvParseException : FormatException
    {
        public CsvErrorCode Code { get; }
        public string SourceName { get; }
        public int LineNumber { get; }
        public int FieldNumber { get; }
        public CsvParseException(CsvErrorCode code, string sourceName, int lineNumber, int fieldNumber)
            : base($"CSV {code}: {sourceName}, {lineNumber}행 {fieldNumber}열")
        {
            Code = code; SourceName = sourceName; LineNumber = lineNumber; FieldNumber = fieldNumber;
        }
    }
}
