using EscapeGame.Data.Csv;

namespace EscapeGame.Data.Tables
{
    // ROOM_A.csv의 한 행을 나타내는 읽기 전용 정의입니다.
    public sealed class RoomAData : RoomData
    {
        public static CsvTableMapping<RoomAData> Table
        {
            get;
        } = new CsvTableMapping<RoomAData>
            ("ROOM_A", row => new RoomAData(row));

        public RoomAData(CsvRow row) : base(row, Table.SheetName) { }
    }
}
