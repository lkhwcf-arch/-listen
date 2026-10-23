using EscapeGame.Data.Csv;

namespace EscapeGame.Data.Tables
{
    // PUZZLE.csv의 한 행을 나타내는 읽기 전용 정의입니다.
    public sealed class PuzzleData
    {
        public static CsvTableMapping<PuzzleData> Table
        {
            get;
        } = new CsvTableMapping<PuzzleData>
            ("PUZZLE", row => new PuzzleData(row));
        public int Id
        {
            get;
        }
        public string Type
        {
            get;
        }
        public int SolverRoom
        {
            get;
        }
        public int? ClueRoom
        {
            get;
        }
        public int InputAnchor
        {
            get;
        }
        public int SlotCount
        {
            get;
        }
        public bool Active
        {
            get;
        }

        public PuzzleData(CsvRow row)
        {
            var value = new CsvRowReader(row, Table.SheetName);
            Id = value.Integer("ID");
            Type = value.Text("Type");
            SolverRoom = value.Integer("SolverRoom");
            ClueRoom = value.OptionalInteger("ClueRoom");
            InputAnchor = value.Integer("InputAnchor");
            SlotCount = value.Integer("SlotCount");
            Active = value.Flag("Active");
        }
    }
}
