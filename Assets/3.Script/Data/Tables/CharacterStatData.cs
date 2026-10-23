using EscapeGame.Data.Csv;

namespace EscapeGame.Data.Tables
{
    // CHARACTER_STAT.csv의 한 행을 나타내는 읽기 전용 정의입니다.
    public sealed class CharacterStatData
    {
        public static CsvTableMapping<CharacterStatData> Table
        {
            get;
        } = new CsvTableMapping<CharacterStatData>
            ("CHARACTER_STAT", row => new CharacterStatData(row));
        public int Id
        {
            get;
        }
        public float WalkSpeed
        {
            get;
        }
        public float Acceleration
        {
            get;
        }
        public float Deceleration
        {
            get;
        }
        public float MouseSensitivity
        {
            get;
        }
        public float PitchLimit
        {
            get;
        }
        public float Fov
        {
            get;
        }
        public float EyeHeight
        {
            get;
        }
        public float CapsuleHeight
        {
            get;
        }
        public float CapsuleRadius
        {
            get;
        }
        public float StepOffset
        {
            get;
        }
        public float InteractDistance
        {
            get;
        }
        public int CloseUpOpenMs
        {
            get;
        }
        public float HeadBob
        {
            get;
        }

        public CharacterStatData(CsvRow row)
        {
            var value = new CsvRowReader(row, Table.SheetName);
            Id = value.Integer("ID");
            WalkSpeed = value.Number("WalkSpeed");
            Acceleration = value.Number("Acceleration");
            Deceleration = value.Number("Deceleration");
            MouseSensitivity = value.Number("MouseSensitivity");
            PitchLimit = value.Number("PitchLimit");
            Fov = value.Number("FOV");
            EyeHeight = value.Number("EyeHeight");
            CapsuleHeight = value.Number("CapsuleHeight");
            CapsuleRadius = value.Number("CapsuleRadius");
            StepOffset = value.Number("StepOffset");
            InteractDistance = value.Number("InteractDistance");
            CloseUpOpenMs = value.Integer("CloseUpOpenMs");
            HeadBob = value.Number("HeadBob");
        }
    }
}
