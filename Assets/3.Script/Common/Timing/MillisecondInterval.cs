using System;

namespace EscapeGame.Timing
{
    // 놓친 주기를 누적하지 않고 실제 실행 시점부터 다음 간격을 계산합니다.
    public sealed class MillisecondInterval
    {
        private readonly IMillisecondClock clock;
        private readonly int intervalMs;
        private long lastRunMs;
        private long lastObservedMs;
        private bool immediate;

        public MillisecondInterval(IMillisecondClock clock, int intervalMs)
        {
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            if (intervalMs <= 0)
                throw new ArgumentOutOfRangeException(nameof(intervalMs));
            this.intervalMs = intervalMs;
            Restart();
        }

        public void Restart(bool runImmediately = true)
        {
            long nowMs = clock.ElapsedMilliseconds;
            if (nowMs < 0)
                throw new InvalidOperationException("경과 시간은 음수일 수 없습니다.");
            lastRunMs = lastObservedMs = nowMs;
            immediate = runImmediately;
        }

        public bool TryConsume()
        {
            long nowMs = clock.ElapsedMilliseconds;
            if (nowMs < lastObservedMs)
                throw new InvalidOperationException("경과 시간은 역행할 수 없습니다.");
            lastObservedMs = nowMs;

            if (!immediate && nowMs - lastRunMs < intervalMs)
                return false;

            immediate = false;
            lastRunMs = nowMs;
            return true;
        }
    }
}
