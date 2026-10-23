using EscapeGame.Data.Csv;

namespace EscapeGame.Data.Tables
{
    // 동일 구조의 CSV는 공통 필드와 변환을 재사용합니다.
    public abstract class AnchorData
    {
        public int Id
        {
            get;
        }
        public int RoomNo
        {
            get;
        }
        public string PlayerType
        {
            get;
        }
        public string AnchorId
        {
            get;
        }
        public int PrefabId
        {
            get;
        }
        public bool Active
        {
            get;
        }

        protected AnchorData(CsvRow row, string sheet)
        {
            var value = new CsvRowReader(row, sheet);
            Id = value.Integer("ID");
            RoomNo = value.Integer("RoomNo");
            PlayerType = value.Text("PlayerType");
            AnchorId = value.Text("AnchorID");
            PrefabId = value.Integer("PrefabID");
            Active = value.Flag("Active");
        }
    }
}
