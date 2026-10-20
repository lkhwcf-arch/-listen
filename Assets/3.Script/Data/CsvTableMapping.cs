using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using EscapeGame.Data.Csv;

namespace EscapeGame.Data
{
    // CSV 이름과 변환 함수를 한 곳에 연결합니다. Reflection 없이 Unity 빌드에서도 같은 경로를 사용합니다.
    public sealed class CsvTableMapping<T>
    {
        public string SheetName { get; }
        public Func<CsvRow, T> Convert { get; }
        public CsvTableMapping(string sheetName, Func<CsvRow, T> convert)
        {
            if (string.IsNullOrWhiteSpace(sheetName)) throw new ArgumentException("CSV sheet name is required.", nameof(sheetName));
            SheetName = sheetName;
            Convert = convert ?? throw new ArgumentNullException(nameof(convert));
        }
    }

    // 검증 후 문자열을 데이터 클래스의 자료형으로 변환하는 공통 경계입니다.
    // 값의 범위·참조 규칙은 기존 검증기에 두고 여기서는 변환 실패의 위치를 제공합니다.
    internal sealed class CsvRowReader
    {
        private readonly CsvRow row;
        private readonly string sheet;
        public CsvRowReader(CsvRow row, string sheet)
        { this.row = row ?? throw new ArgumentNullException(nameof(row)); this.sheet = sheet; }

        public string Text(string column)
        {
            if (row.TryGet(column, out var value)) return value;
            throw Error(column, "Missing column.");
        }
        public int Integer(string column, bool allowSign = false)
        {
            if (int.TryParse(Text(column), allowSign ? NumberStyles.Integer : NumberStyles.None, CultureInfo.InvariantCulture, out int value)) return value;
            throw Error(column, "Expected an integer.");
        }
        public int? OptionalInteger(string column) => Text(column).Length == 0 ? (int?)null : Integer(column);
        public float Number(string column)
        {
            if (float.TryParse(Text(column), NumberStyles.Float, CultureInfo.InvariantCulture, out float value) && !float.IsNaN(value) && !float.IsInfinity(value)) return value;
            throw Error(column, "Expected a finite single-precision number.");
        }
        public float? OptionalNumber(string column) => Text(column).Length == 0 ? (float?)null : Number(column);
        public bool Flag(string column)
        {
            string value = Text(column);
            if (value == "Y") return true;
            if (value == "N") return false;
            throw Error(column, "Expected Y or N.");
        }
        public IReadOnlyList<int> IdList(string column, char separator)
        {
            string text = Text(column);
            if (text.Length == 0) return Array.AsReadOnly(Array.Empty<int>());
            return Array.AsReadOnly(text.Split(separator).Select(part =>
            {
                if (!int.TryParse(part.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int id) || id <= 0)
                    throw Error(column, "Expected positive int32 IDs.");
                return id;
            }).ToArray());
        }
        public IReadOnlyList<string> List(string column, bool dashMeansEmpty = false)
        {
            string value = Text(column);
            if (value.Length == 0 || (dashMeansEmpty && value == "-")) return Array.AsReadOnly(Array.Empty<string>());
            return Array.AsReadOnly(value.Split(',').Select(x => x.Trim()).ToArray());
        }
        private FormatException Error(string column, string reason) => new FormatException($"CSV mapping: {sheet}.csv:{row.SourceLine} [{column}] {reason}");
    }
}
