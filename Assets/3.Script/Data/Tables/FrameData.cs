using EscapeGame.Data.Csv;

namespace EscapeGame.Data.Tables
{
    // FRAME.csv의 한 행을 나타내는 읽기 전용 정의입니다.
    public sealed class FrameData
    {
        public static CsvTableMapping<FrameData> Table
        {
            get;
        } = new CsvTableMapping<FrameData>
            ("FRAME", row => new FrameData(row));
        public int AnchorRef
        {
            get;
        }
        public string Color
        {
            get;
        }
        public string Wall
        {
            get;
        }
        public bool IsAnswer
        {
            get;
        }

        public FrameData(CsvRow row)
        {
            var value = new CsvRowReader(row, Table.SheetName);
            AnchorRef = value.Integer("AnchorRef");
            Color = value.Text("Color");
            Wall = value.Text("Wall");
            IsAnswer = value.Flag("IsAnswer");
        }
    }
}
