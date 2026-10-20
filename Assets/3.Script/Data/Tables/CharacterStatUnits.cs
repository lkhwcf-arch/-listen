using System;

namespace EscapeGame.Data.Tables
{
    public sealed class CharacterStatValueException : InvalidOperationException
    {
        public string Column { get; }
        public CharacterStatValueException(string column, string reason) : base(column + ": " + reason) => Column = column;
    }

    // CSV 검증과 플레이어 생성이 함께 사용하는 불변 정수 정의입니다.
    public sealed class CharacterStatUnits
    {
        public int WalkSpeedMmPerSecond { get; }
        public int AccelerationMmPerSecondSquared { get; }
        public int DecelerationMmPerSecondSquared { get; }
        public int EyeHeightMm { get; }
        public int CapsuleHeightMm { get; }
        public int CapsuleRadiusMm { get; }
        public int StepOffsetMm { get; }
        public int FovMilliDegrees { get; }
        public int MouseSensitivityMilli { get; }
        public int PitchLimitMilliDegrees { get; }

        public CharacterStatUnits(CharacterStatData profile) : this(field => field switch
        {
            "WalkSpeed" => profile.WalkSpeed, "Acceleration" => profile.Acceleration, "Deceleration" => profile.Deceleration,
            "EyeHeight" => profile.EyeHeight, "CapsuleHeight" => profile.CapsuleHeight, "CapsuleRadius" => profile.CapsuleRadius,
            "StepOffset" => profile.StepOffset, "FOV" => profile.Fov,
            "MouseSensitivity" => profile.MouseSensitivity, "PitchLimit" => profile.PitchLimit,
            _ => throw new ArgumentException("알 수 없는 캐릭터 필드: " + field)
        }) { }

        public CharacterStatUnits(Func<string, float> read)
        {
            if (read == null) throw new ArgumentNullException(nameof(read));
            WalkSpeedMmPerSecond = ToMilliUnit(read("WalkSpeed"), "WalkSpeed");
            AccelerationMmPerSecondSquared = ToMilliUnit(read("Acceleration"), "Acceleration");
            DecelerationMmPerSecondSquared = ToMilliUnit(read("Deceleration"), "Deceleration");
            EyeHeightMm = ToMilliUnit(read("EyeHeight"), "EyeHeight");
            CapsuleHeightMm = ToMilliUnit(read("CapsuleHeight"), "CapsuleHeight");
            CapsuleRadiusMm = ToMilliUnit(read("CapsuleRadius"), "CapsuleRadius");
            StepOffsetMm = ToMilliUnit(read("StepOffset"), "StepOffset");
            FovMilliDegrees = ToMilliUnit(read("FOV"), "FOV");
            MouseSensitivityMilli = ToMilliUnit(read("MouseSensitivity"), "MouseSensitivity");
            PitchLimitMilliDegrees = ToMilliUnit(read("PitchLimit"), "PitchLimit");
            Require("MouseSensitivity", MouseSensitivityMilli > 0 && MouseSensitivityMilli <= 100000, "1..100000 milli 배율");
            Require("PitchLimit", PitchLimitMilliDegrees >= 0 && PitchLimitMilliDegrees < 90000, "0 <= PitchLimit < 90도");
            Require("WalkSpeed", WalkSpeedMmPerSecond > 0 && WalkSpeedMmPerSecond <= 100000, "1..100000 mm/s");
            Require("Acceleration", AccelerationMmPerSecondSquared > 0 && AccelerationMmPerSecondSquared <= 1000000, "1..1000000 mm/s²");
            Require("Deceleration", DecelerationMmPerSecondSquared > 0 && DecelerationMmPerSecondSquared <= 1000000, "1..1000000 mm/s²");
            Require("EyeHeight", EyeHeightMm > 0 && EyeHeightMm < CapsuleHeightMm, "0 < 눈높이 < 캡슐 높이");
            Require("CapsuleRadius", CapsuleRadiusMm > 0, "반지름 > 0");
            Require("CapsuleHeight", CapsuleHeightMm >= 2L * CapsuleRadiusMm, "높이 >= 2 × 반지름");
            Require("StepOffset", StepOffsetMm >= 0 && StepOffsetMm < CapsuleHeightMm, "0 <= 단차 < 캡슐 높이");
            Require("FOV", FovMilliDegrees > 0 && FovMilliDegrees < 179000, "0 < FOV < 179도");
        }
        private static void Require(string field, bool valid, string reason)
        { if (!valid) throw new CharacterStatValueException(field, reason); }
        public static int ToMilliUnit(float value, string field)
        {
            double scaled = Math.Round((double)value * 1000, MidpointRounding.AwayFromZero);
            if (double.IsNaN(scaled) || double.IsInfinity(scaled) || scaled > int.MaxValue || scaled < int.MinValue)
                throw new CharacterStatValueException(field, "int32 정수 milli 단위로 변환할 수 없습니다.");
            return (int)scaled;
        }
    }
}
