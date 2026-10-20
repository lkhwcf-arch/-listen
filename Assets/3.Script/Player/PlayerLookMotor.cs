using System;

namespace EscapeGame.Player
{
    public readonly struct PlayerLookCommand
    {
        public int RightMilliPixels { get; }
        public int UpMilliPixels { get; }
        public PlayerLookCommand(int rightMilliPixels, int upMilliPixels)
        {
            if (Math.Abs((long)rightMilliPixels) > 10000000 || Math.Abs((long)upMilliPixels) > 10000000)
                throw new ArgumentOutOfRangeException("마우스 델타는 프레임당 ±10000 픽셀 범위입니다.");
            RightMilliPixels = rightMilliPixels; UpMilliPixels = upMilliPixels;
        }
    }

    // 시간에 곱하지 않습니다. 마우스 델타는 이미 이번 입력 갱신의 이동량입니다.
    public sealed class PlayerLookMotor
    {
        private readonly int sensitivityMilli, unitMilliDegreesPerPixel, pitchLimit;
        private long yawRemainder, pitchRemainder;
        public int YawMilliDegrees { get; private set; }
        public int PitchMilliDegrees { get; private set; }
        public PlayerLookMotor(PlayerCharacterSettings settings, int unitMilliDegreesPerPixel, int initialYawMilliDegrees)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (unitMilliDegreesPerPixel < 1 || unitMilliDegreesPerPixel > 1000) throw new ArgumentOutOfRangeException(nameof(unitMilliDegreesPerPixel));
            sensitivityMilli = settings.MouseSensitivityMilli;
            pitchLimit = settings.PitchLimitMilliDegrees;
            this.unitMilliDegreesPerPixel = unitMilliDegreesPerPixel;
            YawMilliDegrees = Wrap(initialYawMilliDegrees);
        }
        public void Apply(PlayerLookCommand command)
        {
            long right = Integrate(command.RightMilliPixels, ref yawRemainder);
            long up = Integrate(command.UpMilliPixels, ref pitchRemainder);
            YawMilliDegrees = Wrap(YawMilliDegrees + right);
            long pitch = PitchMilliDegrees - up;
            PitchMilliDegrees = (int)Math.Max(-pitchLimit, Math.Min(pitchLimit, pitch));
            if (pitch != PitchMilliDegrees || (PitchMilliDegrees == pitchLimit && command.UpMilliPixels < 0) ||
                (PitchMilliDegrees == -pitchLimit && command.UpMilliPixels > 0)) pitchRemainder = 0;
        }
        public void ClearPendingInput() { yawRemainder = 0; pitchRemainder = 0; }
        private long Integrate(int delta, ref long remainder)
        {
            long numerator = (long)delta * sensitivityMilli * unitMilliDegreesPerPixel + remainder;
            long angle = numerator / 1000000;
            remainder = numerator % 1000000;
            return angle;
        }
        private static int Wrap(long value) => (int)((value % 360000 + 360000) % 360000);
    }
}
