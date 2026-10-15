using System;
using System.Collections.Generic;
using System.Text;

namespace EscapeGame.Data.Csv
{
    // TextAsset, Unity, 파일 시스템과 콘텐츠 컬럼을 모르는 CSV 파서입니다.
    public sealed class CsvParser
    {
        private enum FieldState { Start, Unquoted, Quoted, AfterQuote }
        private static readonly char[] TrimCharacters = { ' ', '\t', '\r', '\n' };

        public CsvDocument Read(string text, string sourceName = "(memory)")
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            if (text.Length > 0 && text[0] == '\uFEFF') text = text.Substring(1);
            var state = FieldState.Start;
            var field = new StringBuilder();
            var fields = new List<string>();
            bool hasSyntax = false;
            int line = 1, recordLine = 1;
            List<string> headers = null;
            var rows = new List<CsvRow>();

            void Fail(CsvErrorCode code, int errorLine, int column)
                => throw new CsvParseException(code, sourceName, errorLine, column);
            void EndField()
            {
                fields.Add(field.ToString()); field.Clear(); state = FieldState.Start;
            }
            void EndRecord()
            {
                EndField();
                bool blank = !hasSyntax;
                if (blank)
                    foreach (string cell in fields)
                        if (cell.Trim(TrimCharacters).Length != 0) { blank = false; break; }
                if (!blank)
                {
                    if (headers == null)
                    {
                        headers = new List<string>();
                        var names = new HashSet<string>(StringComparer.Ordinal);
                        for (int column = 0; column < fields.Count; column++)
                        {
                            string header = fields[column].Trim(TrimCharacters);
                            if (header.Length == 0) Fail(CsvErrorCode.EmptyHeader, recordLine, column + 1);
                            if (!names.Add(header)) Fail(CsvErrorCode.DuplicateHeader, recordLine, column + 1);
                            headers.Add(header);
                        }
                    }
                    else
                    {
                        if (fields.Count != headers.Count)
                            Fail(CsvErrorCode.FieldCountMismatch, recordLine, Math.Min(fields.Count, headers.Count) + 1);
                        rows.Add(new CsvRow(recordLine, headers, fields));
                    }
                }
                fields.Clear(); hasSyntax = false;
            }

            int index = 0;
            while (index < text.Length)
            {
                char character = text[index];
                int newlineSize = character == '\r' ? (index + 1 < text.Length && text[index + 1] == '\n' ? 2 : 1) : character == '\n' ? 1 : 0;
                if (state == FieldState.Quoted)
                {
                    if (character == '"') { state = FieldState.AfterQuote; index++; }
                    else if (newlineSize != 0) { field.Append(text, index, newlineSize); index += newlineSize; line++; }
                    else { field.Append(character); index++; }
                    continue;
                }
                if (state == FieldState.AfterQuote && character == '"')
                {
                    field.Append('"'); state = FieldState.Quoted; index++; continue;
                }
                if (character == ',') { EndField(); hasSyntax = true; index++; continue; }
                if (newlineSize != 0) { EndRecord(); index += newlineSize; line++; recordLine = line; continue; }
                if (state == FieldState.AfterQuote) Fail(CsvErrorCode.InvalidQuote, line, fields.Count + 1);
                if (character == '"')
                {
                    if (state != FieldState.Start) Fail(CsvErrorCode.InvalidQuote, line, fields.Count + 1);
                    state = FieldState.Quoted; hasSyntax = true; index++; continue;
                }
                field.Append(character); state = FieldState.Unquoted; index++;
            }
            if (state == FieldState.Quoted) Fail(CsvErrorCode.UnclosedQuote, line, fields.Count + 1);
            EndRecord();
            if (headers == null) Fail(CsvErrorCode.MissingHeader, 1, 1);
            return new CsvDocument(sourceName, headers, rows);
        }
    }
}
