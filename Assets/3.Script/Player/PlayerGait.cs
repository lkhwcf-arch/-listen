using System;

namespace EscapeGame.Player
{
    // 충돌 처리 후 실제로 걸은 거리(m)가 보행 위상과 발소리를 진행시킵니다.
    public sealed class PlayerGait
    {
        private readonly double stepMeters, amplitudeMeters, returnSeconds;
        private double distance, phase;
        public double OffsetMeters { get; private set; }
        public event Action Stepped;
        public PlayerGait(double stepMeters, double amplitudeMeters, double returnSeconds)
        {
            if (!Finite(stepMeters) || stepMeters <= 0 || !Finite(amplitudeMeters) || amplitudeMeters < 0 ||
                !Finite(returnSeconds) || returnSeconds <= 0) throw new ArgumentOutOfRangeException(nameof(stepMeters));
            this.stepMeters = stepMeters; this.amplitudeMeters = amplitudeMeters; this.returnSeconds = returnSeconds;
        }
        public void Tick(double horizontalMeters, bool grounded, bool allowed, double seconds)
        {
            if (!Finite(horizontalMeters) || !Finite(seconds) || horizontalMeters < 0 || seconds < 0)
                throw new ArgumentOutOfRangeException(nameof(horizontalMeters));
            if (allowed && grounded && horizontalMeters > .0001)
            {
                distance += horizontalMeters; phase = (phase + horizontalMeters / stepMeters * Math.PI * 2) % (Math.PI * 2);
                OffsetMeters = Math.Sin(phase) * amplitudeMeters;
                while (distance >= stepMeters) { distance -= stepMeters; Stepped?.Invoke(); }
            }
            else
            {
                distance = phase = 0;
                OffsetMeters *= Math.Exp(-seconds / returnSeconds);
                if (Math.Abs(OffsetMeters) < .00001) OffsetMeters = 0;
            }
        }
        public void Reset() { distance = phase = OffsetMeters = 0; }
        private static bool Finite(double x) => !double.IsNaN(x) && !double.IsInfinity(x);
    }
}
