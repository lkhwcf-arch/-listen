using System;
using EscapeGame.Data.Csv;

namespace EscapeGame.Data.Tables
{
    // GAME_CONFIG.csv의 한 행을 나타내는 읽기 전용 정의입니다.
    public sealed class GameConfigData
    {
        private readonly int? integerValue;
        private readonly float? numberValue;
        public static CsvTableMapping<GameConfigData> Table { get; } = new CsvTableMapping<GameConfigData>
            ("GAME_CONFIG", row => new GameConfigData(row));
        public int Id { get; }
        public string Value { get; }
        public string Type { get; }
        public string Unit { get; }

        public GameConfigData(CsvRow row)
        {
            var value = new CsvRowReader(row, Table.SheetName);
            Id = value.Integer("ID");
            Value = value.Text("Value");
            Type = value.Text("Type");
            Unit = value.Text("Unit");
            if (Type == "int") integerValue = value.Integer("Value", allowSign: true);
            if (Type == "float") numberValue = value.Number("Value");
        }

        // Type에 맞는 값만 반환하며 문자열·색상·기호를 임의로 숫자로 해석하지 않습니다.
        public int AsInt() => integerValue ?? throw new InvalidOperationException("GAME_CONFIG value is not int: " + Id);
        public float AsFloat() => numberValue ?? throw new InvalidOperationException("GAME_CONFIG value is not float: " + Id);
    }
}
