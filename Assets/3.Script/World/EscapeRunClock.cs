using System;
using System.Globalization;

namespace EscapeGame.World
{
    // 클라이언트 표시용 단조 시계입니다. 서버 판정·순위 기록에는 사용하지 않습니다.
    public sealed class EscapeRunClock
    {
        private readonly long enteredAtMs;
        public long? ElapsedMs { get; private set; }
        public EscapeRunClock(long enteredAtMs)
        { if (enteredAtMs < 0) throw new ArgumentOutOfRangeException(nameof(enteredAtMs)); this.enteredAtMs = enteredAtMs; }
        public void FreezeAtVideoStart(long nowMs)
        {
            if (nowMs < enteredAtMs) throw new ArgumentOutOfRangeException(nameof(nowMs));
            if (!ElapsedMs.HasValue) ElapsedMs = nowMs - enteredAtMs;
        }
        public static string Format(long milliseconds)
        {
            if (milliseconds < 0) throw new ArgumentOutOfRangeException(nameof(milliseconds));
            long totalSeconds = milliseconds / 1000;
            return (totalSeconds / 3600).ToString("00", CultureInfo.InvariantCulture) + ":" +
                (totalSeconds / 60 % 60).ToString("00", CultureInfo.InvariantCulture) + ":" +
                (totalSeconds % 60).ToString("00", CultureInfo.InvariantCulture);
        }
    }
}
