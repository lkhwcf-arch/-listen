using EscapeGame.Data.Csv;

namespace EscapeGame.Data.Tables
{
    // ANCHOR_NONINTERACT.csv의 한 행을 나타내는 읽기 전용 정의입니다.
    public sealed class AnchorNonInteractData : AnchorData
    {
        public static CsvTableMapping<AnchorNonInteractData> Table { get; } = new CsvTableMapping<AnchorNonInteractData>
            ("ANCHOR_NONINTERACT", row => new AnchorNonInteractData(row));

        public AnchorNonInteractData(CsvRow row) : base(row, Table.SheetName) { }
    }
}
