using EscapeGame.Data.Csv;

namespace EscapeGame.Data.Tables
{
    // ARROW.csv의 한 행을 나타내는 읽기 전용 정의입니다.
    public sealed class ArrowData
    {
        public static CsvTableMapping<ArrowData> Table { get; } = new CsvTableMapping<ArrowData>
            ("ARROW", row => new ArrowData(row));
        public int AnchorRef { get; }
        public int Order { get; }
        public string Dir { get; }
        public bool IsAnswer { get; }

        public ArrowData(CsvRow row)
        {
            var value = new CsvRowReader(row, Table.SheetName);
            AnchorRef = value.Integer("AnchorRef");
            Order = value.Integer("Order");
            Dir = value.Text("Dir");
            IsAnswer = value.Flag("IsAnswer");
        }
    }
}
