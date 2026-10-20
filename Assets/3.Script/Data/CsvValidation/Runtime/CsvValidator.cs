using EscapeGame.Data.Csv;
using EscapeGame.Data.CsvValidation.Contracts;
using EscapeGame.Data.CsvValidation.Rules;
using EscapeGame.Data.CsvValidation.Schema;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;

namespace EscapeGame.Data.CsvValidation.Runtime
{
    // 순수 메모리 검증기입니다. 파일 읽기·등록·화면 상태를 소유하지 않습니다.
    public sealed class CsvValidator
    {
        private readonly CsvValidationCatalog catalog;
        private readonly IReadOnlyList<ICsvValidationRule> rules;
        public CsvValidator(CsvValidationCatalog catalog, ICsvResourceResolver resources = null, IEnumerable<ICsvValidationRule> additionalRules = null)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            var defaults = new List<ICsvValidationRule>
            {
                new CsvSchemaRule(), new CsvReferenceRule(), new CsvRoomRule(),
                new CsvPuzzleRule(), new CsvValueRelationRule(), new CsvCharacterStatRule()
            };
            if (resources != null) defaults.Add(new CsvResourceRule(resources));
            if (additionalRules != null) defaults.AddRange(additionalRules);
            rules = Array.AsReadOnly(defaults.ToArray());
        }
        public CsvValidationReport Validate(CsvDataSet data, CancellationToken token = default)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            token.ThrowIfCancellationRequested();
            var context = new CsvValidationContext(data, catalog, token);
            foreach (var rule in rules) { token.ThrowIfCancellationRequested(); rule.Validate(context); }
            token.ThrowIfCancellationRequested();
            return new CsvValidationReport(data, context.CaptureIssues());
        }
    }
}

namespace EscapeGame.Data.CsvValidation.Runtime
{
    public interface ICsvValidationRule
    {
        void Validate(CsvValidationContext context);
    }
    public interface ICsvResourceResolver
    {
        bool Exists(string path, CsvResourceKind kind);
    }

    public sealed class CsvValidationTable
    {
        public string Name { get; }
        public CsvDocument Document { get; }
        public CsvTableDefinition Definition { get; }
        public bool IsUsable { get; }
        public IEnumerable<CsvRow> Rows => IsUsable ? Document.Rows : Enumerable.Empty<CsvRow>();
        private readonly Dictionary<string, CsvRow> index = new Dictionary<string, CsvRow>(StringComparer.Ordinal);
        public string Key => Definition?.Key ?? (Document.Headers.Contains("ID") ? "ID" : null);
        public CsvValidationTable(string name, CsvDocument document, CsvTableDefinition definition)
        {
            Name = name; Document = document; Definition = definition;
            IsUsable = definition == null || definition.Columns.All(x => document.Headers.Contains(x.Name));
            if (IsUsable && Key != null)
                foreach (var row in document.Rows)
                    if (!index.ContainsKey(row.Get(Key))) index.Add(row.Get(Key), row);
        }
        public CsvRow Find(string key) => key != null && index.TryGetValue(key, out var row) ? row : null;
    }

    // 한 실행에만 존재하는 작업 공간입니다. 엔진 인스턴스에 이전 검사 상태를 남기지 않습니다.
    public sealed class CsvValidationContext
    {
        private readonly List<CsvValidationIssue> issues;
        public CsvValidationCatalog Catalog { get; }
        public IReadOnlyList<CsvValidationTable> Tables { get; }
        public CancellationToken Token { get; }
        public CsvValidationContext(CsvDataSet input, CsvValidationCatalog catalog, CancellationToken token)
        {
            Catalog = catalog; Token = token;
            Tables = Array.AsReadOnly(input.Documents.OrderBy(x => x.Key, StringComparer.Ordinal)
                .Select(x => new CsvValidationTable(x.Key, x.Value, Catalog.Find(x.Key))).ToArray());
            issues = new List<CsvValidationIssue>(input.LoadIssues);
        }
        public IEnumerable<CsvValidationTable> Tagged(string tag) => Tables.Where(x => x.IsUsable && x.Definition?.Tag == tag);
        public CsvValidationTable Find(string name) => Tables.FirstOrDefault(x => x.Name == name && x.IsUsable);
        public CsvValidationIssue[] CaptureIssues() => issues.ToArray();
        public void Add(CsvValidationCode code, string sheet, int line, string column, params string[] details)
            => issues.Add(new CsvValidationIssue(code, sheet, line, column, CsvValidationSeverity.Error, details));
        public void Warn(CsvValidationCode code, string sheet, int line, string column, params string[] details)
            => issues.Add(new CsvValidationIssue(code, sheet, line, column, CsvValidationSeverity.Warning, details));
        public void Reference(CsvValidationTable table, CsvRow row, string column, IEnumerable<CsvValidationTable> targets, string sentinel = null)
        {
            string value = row.Get(column);
            if (value.Length == 0 || value == sentinel) return;
            var candidates = targets.ToArray();
            CsvRow match = candidates.Select(x => x.Find(value)).FirstOrDefault(x => x != null);
            if (match == null) Add(CsvValidationCode.MissingReference, table.Name, row.SourceLine, column, value, string.Join(", ", candidates.Select(x => x.Name)));
            else if (Active(row) && !Active(match)) Warn(CsvValidationCode.InactiveReference, table.Name, row.SourceLine, column, value);
        }
        public static bool Active(CsvRow row) => !row.TryGet("Active", out var value) || value == "Y";
        public static string[] List(string value) => value == "-" || value.Length == 0 ? Array.Empty<string>() : value.Split(',').Select(x => x.Trim()).ToArray();
        public static bool Number(string value, out double number) => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out number) && !double.IsNaN(number) && !double.IsInfinity(number);
    }
}
