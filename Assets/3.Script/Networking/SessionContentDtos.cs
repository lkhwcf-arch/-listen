using System;
using System.Collections.Generic;
using EscapeGame.Data;
using EscapeGame.Data.Csv;

namespace EscapeGame.Networking
{
    [Serializable] internal sealed class CsvBundleDto
    {
        public bool success;
        public string code;
        public string version;
        public CsvTextDto[] tables;

        internal CsvDataSet Read()
        {
            if (string.IsNullOrWhiteSpace(version) || tables == null || tables.Length == 0)
                throw new InvalidOperationException("서버 CSV 묶음이 비어 있습니다.");
            var parser = new CsvParser();
            var documents = new Dictionary<string, CsvDocument>(StringComparer.Ordinal);
            foreach (var table in tables)
            {
                if (table == null || string.IsNullOrWhiteSpace(table.name) || documents.ContainsKey(table.name))
                    throw new InvalidOperationException("CSV 이름이 없거나 중복됩니다.");
                documents.Add(table.name, parser.Read(table.csv, table.name));
            }
            return new CsvDataSet(documents);
        }
    }
    [Serializable] internal sealed class CsvTextDto { public string name; public string csv; }
    [Serializable] internal sealed class PuzzleSubmissionDto { public string[] answers; }
}
