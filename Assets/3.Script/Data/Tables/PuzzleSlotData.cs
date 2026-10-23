using EscapeGame.Data.Csv;

namespace EscapeGame.Data.Tables
{
    // PUZZLE_SLOT.csv의 한 행을 나타내는 읽기 전용 정의입니다.
    public sealed class PuzzleSlotData
    {
        public static CsvTableMapping<PuzzleSlotData> Table
        {
            get;
        } = new CsvTableMapping<PuzzleSlotData>
            ("PUZZLE_SLOT", row => new PuzzleSlotData(row));
        public int Id
        {
            get;
        }
        public int PuzzleId
        {
            get;
        }
        public int Slot
        {
            get;
        }
        public string Display
        {
            get;
        }
        public string Answer
        {
            get;
        }

        public PuzzleSlotData(CsvRow row)
        {
            var value = new CsvRowReader(row, Table.SheetName);
            Id = value.Integer("ID");
            PuzzleId = value.Integer("PuzzleID");
            Slot = value.Integer("Slot");
            Display = value.Text("Display");
            Answer = value.Text("Answer");
        }
    }
}
