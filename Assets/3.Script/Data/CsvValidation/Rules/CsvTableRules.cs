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
    public sealed class CsvSchemaRule : ICsvValidationRule
    {
        public void Validate(CsvValidationContext context)
        {
            foreach (var table in context.Tables)
            {
                context.Token.ThrowIfCancellationRequested();
                var schema = table.Definition;
                if (schema == null)
                    context.Warn(CsvValidationCode.UnknownSchema, table.Name, 1, "");
                else
                {
                    var expected = schema.Columns.Select(x => x.Name).ToArray();
                    foreach (string column in expected.Except(table.Document.Headers))
                        context.Add(CsvValidationCode.MissingColumn, table.Name, 1, column);
                    foreach (string column in table.Document.Headers.Except(expected))
                        context.Warn(CsvValidationCode.UnknownColumn, table.Name, 1, column);
                }
                if (!table.IsUsable)
                    continue;
                if (table.Document.Rows.Count == 0)
                    context.Warn(CsvValidationCode.EmptyTable, table.Name, 1, "");
                if (table.Key != null)
                    Unique(context, table, new[] { table.Key });
                if (schema == null)
                    continue; // 스키마가 없어도 문법·일반 ID 중복 검사는 수행합니다.
                foreach (var key in schema.UniqueKeys)
                    Unique(context, table, key);
                foreach (var row in table.Rows)
                {
                    context.Token.ThrowIfCancellationRequested();
                    foreach (var column in schema.Columns)
                        CheckCell(context, table, row, column);
                }
            }
            foreach (var group in context.Tables.Where(x => x.IsUsable && !string.IsNullOrEmpty(x.Definition?.KeyGroup)).GroupBy(x => x.Definition.KeyGroup))
            {
                var keys = new HashSet<string>(StringComparer.Ordinal);
                foreach (var table in group)
                    foreach (var row in table.Rows)
                        if (!keys.Add(row.Get(table.Key)))
                            context.Add(CsvValidationCode.DuplicateKey, table.Name, row.SourceLine, table.Key, row.Get(table.Key), group.Key);
            }
        }
        internal static void Unique(CsvValidationContext context, CsvValidationTable table, IReadOnlyList<string> columns)
        {
            // 길이 접두사를 붙여 구분자가 포함된 문자열도 충돌하지 않게 합니다.
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in table.Rows)
            {
                string key = string.Concat(columns.Select(column => row.Get(column).Length + ":" + row.Get(column)));
                if (!keys.Add(key))
                    context.Add(CsvValidationCode.DuplicateKey, table.Name, row.SourceLine, string.Join(",", columns), key);
            }
        }
        private static void CheckCell(CsvValidationContext context, CsvValidationTable table, CsvRow row, CsvColumnDefinition column)
        {
            string value = row.Get(column.Name);
            if (value.Length == 0)
            {
                if (!column.Optional)
                    context.Add(CsvValidationCode.EmptyValue, table.Name, row.SourceLine, column.Name);
                return;
            }
            if (value != value.Trim())
                context.Warn(CsvValidationCode.Whitespace, table.Name, row.SourceLine, column.Name);
            double number = 0;
            bool valid = column.Kind switch
            {
                CsvValueKind.Integer => int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out _) && CsvValidationContext.Number(value, out number),
                CsvValueKind.Number => CsvValidationContext.Number(value, out number),
                _ => true
            };
            if (column.Kind != CsvValueKind.Text && (column.Minimum.HasValue && number < column.Minimum || column.Maximum.HasValue && number > column.Maximum))
                valid = false;
            if (column.Values.Count > 0 && !column.Values.Contains(value))
                valid = false;
            if (!valid)
                context.Add(CsvValidationCode.InvalidValue, table.Name, row.SourceLine, column.Name, value, column.Kind.ToString(),
                column.Minimum?.ToString(CultureInfo.InvariantCulture) ?? "", column.Maximum?.ToString(CultureInfo.InvariantCulture) ?? "", string.Join(",", column.Values));
        }
    }
}

namespace EscapeGame.Data.CsvValidation.Rules
{
    public sealed class CsvReferenceRule : ICsvValidationRule
    {
        public void Validate(CsvValidationContext context)
        {
            foreach (var table in context.Tables.Where(x => x.IsUsable && x.Definition != null))
                foreach (var reference in table.Definition.References)
                    foreach (var row in table.Rows)
                    {
                        context.Token.ThrowIfCancellationRequested();
                        var targets = reference.SameTable ? new[] { table } : context.Tagged(reference.Target);
                        if (reference.TargetKind.Length > 0)
                            targets = targets.Where(x => x.Definition.Kind == reference.TargetKind);
                        context.Reference(table, row, reference.Column, targets, reference.Sentinel);
                    }
            CheckRegistry(context);
        }
        private static void CheckRegistry(CsvValidationContext context)
        {
            var registry = context.Tagged(CsvTableTags.Registry).FirstOrDefault();
            if (registry == null)
            {
                context.Add(CsvValidationCode.MissingRegistry, context.Catalog.Tables.First(x => x.Tag == CsvTableTags.Registry).Name, 1, "");
                return;
            }
            var rows = registry.Rows.ToArray();
            var names = new HashSet<string>(rows.Select(x => x.Get("Sheet")), StringComparer.Ordinal);
            CsvSchemaRule.Unique(context, registry, new[] { "Sheet" });
            foreach (var row in rows)
            {
                context.Token.ThrowIfCancellationRequested();
                string name = row.Get("Sheet");
                var table = context.Find(name);
                if (CsvValidationContext.Active(row) && table != null && table.Definition == null)
                    context.Add(CsvValidationCode.UnknownSchema, registry.Name, row.SourceLine, "Sheet", name);
                if (CsvValidationContext.Active(row) && table == null)
                {
                    if (row.Get("Required") == "Y")
                        context.Add(CsvValidationCode.MissingTable, registry.Name, row.SourceLine, "Sheet", name);
                    else
                        context.Warn(CsvValidationCode.MissingTable, registry.Name, row.SourceLine, "Sheet", name);
                }
                foreach (string target in CsvValidationContext.List(row.Get("Parent")).Concat(CsvValidationContext.List(row.Get("Uses"))))
                    if (!names.Contains(target))
                        context.Add(CsvValidationCode.UnknownSheet, registry.Name, row.SourceLine, "Parent/Uses", target);
                if (table == null)
                    continue;
                string[] keys = CsvValidationContext.List(row.Get("ParentKey"));
                foreach (string key in keys)
                    if (!table.Document.Headers.Contains(key))
                        context.Add(CsvValidationCode.MissingColumn, registry.Name, row.SourceLine, "ParentKey", name, key);
                if (keys.Length != 1 || !table.Document.Headers.Contains(keys[0]))
                    continue;
                var parents = CsvValidationContext.List(row.Get("Parent")).Select(context.Find).Where(x => x != null).ToArray();
                foreach (var child in table.Rows)
                    context.Reference(table, child, keys[0], parents);
            }
            foreach (var table in context.Tables)
                if (!names.Contains(table.Name))
                    context.Warn(CsvValidationCode.UnregisteredTable, table.Name, 1, "");
        }
    }
}

namespace EscapeGame.Data.CsvValidation.Rules
{
    public sealed class CsvResourceRule : ICsvValidationRule
    {
        private readonly ICsvResourceResolver resolver;
        public CsvResourceRule(ICsvResourceResolver resolver) => this.resolver = resolver;
        public void Validate(CsvValidationContext context)
        {
            foreach (var table in context.Tables)
            {
                if (!table.IsUsable || table.Definition == null || table.Definition.Resource == CsvResourceKind.None)
                    continue;
                foreach (var row in table.Rows)
                {
                    context.Token.ThrowIfCancellationRequested();
                    if (row.TryGet("Path", out var path) && path.Length > 0 && !resolver.Exists(path, table.Definition.Resource))
                        context.Warn(CsvValidationCode.MissingResource, table.Name, row.SourceLine, "Path", path);
                }
            }
        }
    }
}
