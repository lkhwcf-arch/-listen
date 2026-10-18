using EscapeGame.Data.Csv;

namespace EscapeGame.Data.Tables
{
    // SOUND_LINK.csv의 한 행을 나타내는 읽기 전용 정의입니다.
    public sealed class SoundLinkData
    {
        public static CsvTableMapping<SoundLinkData> Table { get; } = new CsvTableMapping<SoundLinkData>
            ("SOUND_LINK", row => new SoundLinkData(row));
        public int Id { get; }
        public string Trigger { get; }
        public string TargetSheet { get; }
        public int TargetId { get; }
        public int SoundId { get; }
        public bool Active { get; }

        public SoundLinkData(CsvRow row)
        {
            var value = new CsvRowReader(row, Table.SheetName);
            Id = value.Integer("ID");
            Trigger = value.Text("Trigger");
            TargetSheet = value.Text("TargetSheet");
            TargetId = value.Integer("TargetID");
            SoundId = value.Integer("SoundID");
            Active = value.Flag("Active");
        }
    }
}
