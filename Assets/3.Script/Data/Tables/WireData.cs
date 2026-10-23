using EscapeGame.Data.Csv;

namespace EscapeGame.Data.Tables
{
    // WIRE.csv의 한 행을 나타내는 읽기 전용 정의입니다.
    public sealed class WireData
    {
        public static CsvTableMapping<WireData> Table
        {
            get;
        } = new CsvTableMapping<WireData>
            ("WIRE", row => new WireData(row));
        public int AnchorRef
        {
            get;
        }
        public string Color
        {
            get;
        }
        public float LengthM
        {
            get;
        }
        public bool IsAnswer
        {
            get;
        }
        public int Port
        {
            get;
        }

        public WireData(CsvRow row)
        {
            var value = new CsvRowReader(row, Table.SheetName);
            AnchorRef = value.Integer("AnchorRef");
            Color = value.Text("Color");
            LengthM = value.Number("LengthM");
            IsAnswer = value.Flag("IsAnswer");
            Port = value.Integer("Port");
        }
    }
}
