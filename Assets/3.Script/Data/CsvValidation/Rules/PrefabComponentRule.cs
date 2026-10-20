using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using EscapeGame.Data.CsvValidation.Contracts;
using EscapeGame.Data.CsvValidation.Runtime;
using EscapeGame.Data.CsvValidation.Schema;

namespace EscapeGame.Data.CsvValidation.Rules
{
    public sealed class CsvPrefabComponentRule : ICsvValidationRule
    {
        public void Validate(CsvValidationContext context)
        {
            var targets = context.Tagged(CsvTableTags.Component).ToArray();
            foreach (var table in context.Tagged(CsvTableTags.Prefab)) foreach (var row in table.Rows)
            {
                context.Token.ThrowIfCancellationRequested();
                string value = row.Get("Components");
                if (value.Length == 0) continue;
                var seen = new HashSet<int>();
                foreach (string part in value.Split('|'))
                {
                    if (!int.TryParse(part.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int id) || id <= 0)
                    { context.Add(CsvValidationCode.InvalidValue, table.Name, row.SourceLine, "Components", part, "양의 int32 ID를 | 로 연결합니다."); continue; }
                    if (!seen.Add(id)) context.Add(CsvValidationCode.DuplicateKey, table.Name, row.SourceLine, "Components", id.ToString());
                    if (!targets.Any(target => target.Find(id.ToString(CultureInfo.InvariantCulture)) != null))
                        context.Add(CsvValidationCode.MissingReference, table.Name, row.SourceLine, "Components", id.ToString(), "COMPONENT");
                }
            }
        }
    }
}
