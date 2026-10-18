using EscapeGame.Data.Csv;

namespace EscapeGame.Data.Tables
{
    // COLOR_LETTER.csv의 한 행을 나타내는 읽기 전용 정의입니다.
    public sealed class ColorLetterData
    {
        public static CsvTableMapping<ColorLetterData> Table { get; } = new CsvTableMapping<ColorLetterData>
            ("COLOR_LETTER", row => new ColorLetterData(row));
        public int Id { get; }
        public int PuzzleId { get; }
        public string Letter { get; }
        public string Color { get; }
        public int SignAnchor { get; }

        public ColorLetterData(CsvRow row)
        {
            var value = new CsvRowReader(row, Table.SheetName);
            Id = value.Integer("ID");
            PuzzleId = value.Integer("PuzzleID");
            Letter = value.Text("Letter");
            Color = value.Text("Color");
            SignAnchor = value.Integer("SignAnchor");
        }
    }
}
