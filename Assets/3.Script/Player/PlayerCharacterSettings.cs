using System;
using System.Linq;
using EscapeGame.Data.Tables;

namespace EscapeGame.Player
{
    // CSV 정의를 한 번 선택·정규화합니다. 스폰과 이동이 같은 프로필을 공유합니다.
    public sealed class PlayerCharacterSettings
    {
        public int ProfileId { get; }
        public int WalkSpeedMmPerSecond { get; }
        public int AccelerationMmPerSecondSquared { get; }
        public int DecelerationMmPerSecondSquared { get; }
        public int EyeHeightMm { get; }
        public int CapsuleHeightMm { get; }
        public int CapsuleRadiusMm { get; }
        public int StepOffsetMm { get; }
        public int FovMilliDegrees { get; }

        public static PlayerCharacterSettings Select(CsvGameData data, int configId)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            var config = data.GameConfigs.SingleOrDefault(row => row.Id == configId)
                ?? throw new InvalidOperationException("캐릭터 설정 선택 행이 없습니다: " + configId);
            int profileId = config.AsInt();
            var profile = data.CharacterStats.SingleOrDefault(row => row.Id == profileId)
                ?? throw new InvalidOperationException("캐릭터 설정이 없습니다: " + profileId);
            return new PlayerCharacterSettings(profile);
        }

        private PlayerCharacterSettings(CharacterStatData profile)
        {
            ProfileId = profile.Id;
            WalkSpeedMmPerSecond = ToMilliUnit(profile.WalkSpeed, "WalkSpeed");
            AccelerationMmPerSecondSquared = ToMilliUnit(profile.Acceleration, "Acceleration");
            DecelerationMmPerSecondSquared = ToMilliUnit(profile.Deceleration, "Deceleration");
            EyeHeightMm = ToMilliUnit(profile.EyeHeight, "EyeHeight");
            CapsuleHeightMm = ToMilliUnit(profile.CapsuleHeight, "CapsuleHeight");
            CapsuleRadiusMm = ToMilliUnit(profile.CapsuleRadius, "CapsuleRadius");
            StepOffsetMm = ToMilliUnit(profile.StepOffset, "StepOffset");
            FovMilliDegrees = ToMilliUnit(profile.Fov, "FOV");
            if (EyeHeightMm <= 0 || EyeHeightMm >= CapsuleHeightMm || CapsuleRadiusMm <= 0 ||
                CapsuleHeightMm < 2L * CapsuleRadiusMm || StepOffsetMm < 0 || StepOffsetMm >= CapsuleHeightMm ||
                FovMilliDegrees <= 0 || FovMilliDegrees >= 179000)
                throw new InvalidOperationException("캐릭터의 눈높이·캡슐·시야각 설정 범위를 확인하세요.");
            // long 제곱·곱 연산의 안전 범위입니다. 게임 최고속도를 이 값으로 지정하는 것이 아닙니다.
            if (WalkSpeedMmPerSecond <= 0 || WalkSpeedMmPerSecond > 100000 ||
                AccelerationMmPerSecondSquared <= 0 || AccelerationMmPerSecondSquared > 1000000 ||
                DecelerationMmPerSecondSquared <= 0 || DecelerationMmPerSecondSquared > 1000000)
                throw new InvalidOperationException("이동 속도는 1..100000mm/s, 가감속은 1..1000000mm/s² 범위여야 합니다.");
        }

        public static int ToMilliUnit(float value, string field)
        {
            double scaled = Math.Round((double)value * 1000, MidpointRounding.AwayFromZero);
            if (double.IsNaN(scaled) || double.IsInfinity(scaled) || scaled > int.MaxValue || scaled < int.MinValue)
                throw new InvalidOperationException("캐릭터 설정을 정수 단위로 변환할 수 없습니다: " + field);
            return (int)scaled;
        }
    }
}
