using EscapeGame.Data.Csv;

namespace EscapeGame.Data.Tables
{
    // TV.csv의 한 행을 나타내는 읽기 전용 정의입니다.
    public sealed class TvData
    {
        public static CsvTableMapping<TvData> Table
        {
            get;
        } = new CsvTableMapping<TvData>
            ("TV", row => new TvData(row));
        public int AnchorRef
        {
            get;
        }
        public string Letter
        {
            get;
        }
        public int Number
        {
            get;
        }
        public bool IsAnswer
        {
            get;
        }

        public TvData(CsvRow row)
        {
            var value = new CsvRowReader(row, Table.SheetName);
            AnchorRef = value.Integer("AnchorRef");
            Letter = value.Text("Letter");
            Number = value.Integer("Number");
            IsAnswer = value.Flag("IsAnswer");
        }
    }
}
