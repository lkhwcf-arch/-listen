using System.Globalization;
using EscapeGame.Data.CsvValidation.Contracts;
using EscapeGame.Data.CsvValidation.Runtime;
using EscapeGame.Data.CsvValidation.Schema;
using EscapeGame.Data.Tables;

namespace EscapeGame.Data.CsvValidation.Rules
{
    public sealed class CsvCharacterStatRule : ICsvValidationRule
    {
        public void Validate(CsvValidationContext context)
        {
            foreach (var table in context.Tagged(CsvTableTags.CharacterStat))
                foreach (var row in table.Rows)
                {
                    context.Token.ThrowIfCancellationRequested();
                    float Read(string field)
                    {
                        if (float.TryParse(row.Get(field), NumberStyles.Float, CultureInfo.InvariantCulture, out float value) &&
                            !float.IsNaN(value) && !float.IsInfinity(value))
                            return value;
                        throw new CharacterStatValueException(field, "유한한 float32 값이 필요합니다.");
                    }
                    try
                    {
                        _ = new CharacterStatUnits(Read);
                        _ = Read("HeadBob");
                    }
                    catch (CharacterStatValueException error)
                    {
                        context.Add(CsvValidationCode.InvalidValue, table.Name, row.SourceLine, error.Column, row.Get(error.Column), error.Message);
                    }
                }
        }
    }
}
