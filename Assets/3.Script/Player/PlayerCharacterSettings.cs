using System;
using System.Linq;
using EscapeGame.Data.Tables;

namespace EscapeGame.Player
{
    // CSV 정의를 한 번 선택·정규화합니다. 스폰과 이동이 같은 프로필을 공유합니다.
    public sealed class PlayerCharacterSettings
    {
        public int ProfileId
        {
            get;
        }
        public float HeadBobMeters
        {
            get;
        }
        public int InteractDistanceMm
        {
            get;
        }
        public int WalkSpeedMmPerSecond
        {
            get;
        }
        public int AccelerationMmPerSecondSquared
        {
            get;
        }
        public int DecelerationMmPerSecondSquared
        {
            get;
        }
        public int EyeHeightMm
        {
            get;
        }
        public int CapsuleHeightMm
        {
            get;
        }
        public int CapsuleRadiusMm
        {
            get;
        }
        public int StepOffsetMm
        {
            get;
        }
        public int FovMilliDegrees
        {
            get;
        }
        public int MouseSensitivityMilli
        {
            get;
        }
        public int PitchLimitMilliDegrees
        {
            get;
        }

        public static PlayerCharacterSettings Select(CsvGameData data, int configId)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
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
            if (float.IsNaN(profile.HeadBob) || float.IsInfinity(profile.HeadBob) || profile.HeadBob < 0 || profile.HeadBob > .2f)
                throw new InvalidOperationException("HeadBob는 0~0.2m 범위여야 합니다.");
            HeadBobMeters = profile.HeadBob;
            var units = new CharacterStatUnits(profile);
            InteractDistanceMm = units.InteractDistanceMm;
            WalkSpeedMmPerSecond = units.WalkSpeedMmPerSecond;
            AccelerationMmPerSecondSquared = units.AccelerationMmPerSecondSquared;
            DecelerationMmPerSecondSquared = units.DecelerationMmPerSecondSquared;
            EyeHeightMm = units.EyeHeightMm;
            CapsuleHeightMm = units.CapsuleHeightMm;
            CapsuleRadiusMm = units.CapsuleRadiusMm;
            StepOffsetMm = units.StepOffsetMm;
            FovMilliDegrees = units.FovMilliDegrees;
            MouseSensitivityMilli = units.MouseSensitivityMilli;
            PitchLimitMilliDegrees = units.PitchLimitMilliDegrees;
        }

        public static int ToMilliUnit(float value, string field) => CharacterStatUnits.ToMilliUnit(value, field);
    }
}
