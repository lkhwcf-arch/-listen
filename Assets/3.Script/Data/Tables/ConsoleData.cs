using System.Collections.Generic;
using EscapeGame.Data.Csv;

namespace EscapeGame.Data.Tables
{
    // CONSOLE.csv의 한 행을 나타내는 읽기 전용 정의입니다.
    public sealed class ConsoleData
    {
        public static CsvTableMapping<ConsoleData> Table { get; } = new CsvTableMapping<ConsoleData>
            ("CONSOLE", row => new ConsoleData(row));
        public int AnchorRef { get; }
        public int SlotCount { get; }
        public IReadOnlyList<string> Buttons { get; }

        public ConsoleData(CsvRow row)
        {
            var value = new CsvRowReader(row, Table.SheetName);
            AnchorRef = value.Integer("AnchorRef");
            SlotCount = value.Integer("SlotCount");
            Buttons = value.List("Buttons");
        }
    }
}
