using EscapeGame.Data.Csv;

namespace EscapeGame.Data.Tables
{
    // 동일 구조의 CSV는 공통 필드와 변환을 재사용합니다.
    public abstract class RoomData
    {
        public int Id
        {
            get;
        }
        public int RoomNo
        {
            get;
        }
        public string Role
        {
            get;
        }
        public int RoomPrefabId
        {
            get;
        }
        public int PuzzleId
        {
            get;
        }
        public int TargetMs
        {
            get;
        }
        public string ExitRule
        {
            get;
        }
        public bool Active
        {
            get;
        }
        public int Next
        {
            get;
        }

        protected RoomData(CsvRow row, string sheet)
        {
            var value = new CsvRowReader(row, sheet);
            Id = value.Integer("ID");
            RoomNo = value.Integer("RoomNo");
            Role = value.Text("Role");
            RoomPrefabId = value.Integer("RoomPrefabID");
            PuzzleId = value.Integer("PuzzleID");
            TargetMs = value.Integer("TargetMs");
            ExitRule = value.Text("ExitRule");
            Active = value.Flag("Active");
            Next = value.Integer("Next");
        }
    }
}
