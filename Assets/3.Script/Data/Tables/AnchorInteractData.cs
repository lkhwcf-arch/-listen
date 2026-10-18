using EscapeGame.Data.Csv;

namespace EscapeGame.Data.Tables
{
    // ANCHOR_INTERACT.csv의 한 행을 나타내는 읽기 전용 정의입니다.
    public sealed class AnchorInteractData : AnchorData
    {
        public static CsvTableMapping<AnchorInteractData> Table { get; } = new CsvTableMapping<AnchorInteractData>
            ("ANCHOR_INTERACT", row => new AnchorInteractData(row));

        public AnchorInteractData(CsvRow row) : base(row, Table.SheetName) { }
    }
}
