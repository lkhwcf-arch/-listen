using EscapeGame.Data.Csv;

namespace EscapeGame.Data.Tables
{
    // FORMULA.csv의 한 행을 나타내는 읽기 전용 정의입니다.
    public sealed class FormulaData
    {
        public static CsvTableMapping<FormulaData> Table { get; } = new CsvTableMapping<FormulaData>
            ("FORMULA", row => new FormulaData(row));
        public int AnchorRef { get; }
        public string Symbol { get; }
        public bool IsAnswer { get; }

        public FormulaData(CsvRow row)
        {
            var value = new CsvRowReader(row, Table.SheetName);
            AnchorRef = value.Integer("AnchorRef");
            Symbol = value.Text("Symbol");
            IsAnswer = value.Flag("IsAnswer");
        }
    }
}
