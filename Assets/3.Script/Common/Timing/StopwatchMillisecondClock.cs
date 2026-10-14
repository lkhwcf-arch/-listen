using System.Diagnostics;

namespace EscapeGame.Timing
{
    // 로컬 경과 시간용입니다. 서버 시각이나 게임 시뮬레이션 tick이 아닙니다.
    public sealed class StopwatchMillisecondClock : IMillisecondClock
    {
        private readonly Stopwatch stopwatch = Stopwatch.StartNew();
        public long ElapsedMilliseconds => stopwatch.ElapsedMilliseconds;
    }
}
