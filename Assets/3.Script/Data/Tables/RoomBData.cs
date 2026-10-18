using EscapeGame.Data.Csv;

namespace EscapeGame.Data.Tables
{
    // ROOM_B.csv의 한 행을 나타내는 읽기 전용 정의입니다.
    public sealed class RoomBData : RoomData
    {
        public static CsvTableMapping<RoomBData> Table { get; } = new CsvTableMapping<RoomBData>
            ("ROOM_B", row => new RoomBData(row));

        public RoomBData(CsvRow row) : base(row, Table.SheetName) { }
    }
}
