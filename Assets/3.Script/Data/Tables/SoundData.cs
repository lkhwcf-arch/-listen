using EscapeGame.Data.Csv;

namespace EscapeGame.Data.Tables
{
    // SOUND.csv의 한 행을 나타내는 읽기 전용 정의입니다.
    public sealed class SoundData
    {
        public static CsvTableMapping<SoundData> Table
        {
            get;
        } = new CsvTableMapping<SoundData>
            ("SOUND", row => new SoundData(row));
        public int Id
        {
            get;
        }
        public string Category
        {
            get;
        }
        public string Path
        {
            get;
        }
        public bool Loop
        {
            get;
        }
        public float Volume
        {
            get;
        }
        public float? MinDist
        {
            get;
        }
        public float? MaxDist
        {
            get;
        }
        public string Bus
        {
            get;
        }
        public int CooldownMs
        {
            get;
        }
        public bool Active
        {
            get;
        }

        public SoundData(CsvRow row)
        {
            var value = new CsvRowReader(row, Table.SheetName);
            Id = value.Integer("ID");
            Category = value.Text("Category");
            Path = value.Text("Path");
            Loop = value.Flag("Loop");
            Volume = value.Number("Volume");
            MinDist = value.OptionalNumber("MinDist");
            MaxDist = value.OptionalNumber("MaxDist");
            Bus = value.Text("Bus");
            CooldownMs = value.Integer("CooldownMs");
            Active = value.Flag("Active");
        }
    }
}
