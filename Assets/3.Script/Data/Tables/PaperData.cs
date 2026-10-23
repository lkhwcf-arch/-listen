using EscapeGame.Data.Csv;

namespace EscapeGame.Data.Tables
{
    // PAPER.csv의 한 행을 나타내는 읽기 전용 정의입니다.
    public sealed class PaperData
    {
        public static CsvTableMapping<PaperData> Table
        {
            get;
        } = new CsvTableMapping<PaperData>
            ("PAPER", row => new PaperData(row));
        public int AnchorRef
        {
            get;
        }
        public string Equation1
        {
            get;
        }
        public string Equation2
        {
            get;
        }
        public bool IsAnswer
        {
            get;
        }

        public PaperData(CsvRow row)
        {
            var value = new CsvRowReader(row, Table.SheetName);
            AnchorRef = value.Integer("AnchorRef");
            Equation1 = value.Text("Equation1");
            Equation2 = value.Text("Equation2");
            IsAnswer = value.Flag("IsAnswer");
        }
    }
}
