using System;

namespace EscapeGame.Audio
{
    // 전환 도중 역주행하거나 세 번째 방으로 이동해도 현재 볼륨에서 이어갑니다.
    public sealed class AmbientCrossfade
    {
        private readonly float[] gains, start;
        private readonly double duration;
        private int target = -1;
        private double elapsed;
        public bool IsTransitioning => elapsed < duration;
        public int Count => gains.Length;
        public float Gain(int channel) => gains[channel];
        public AmbientCrossfade(int channels, double seconds)
        {
            if (channels < 1 || double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds <= 0)
                throw new ArgumentOutOfRangeException();
            gains = new float[channels];
            start = new float[channels];
            duration = seconds;
            elapsed = duration;
        }
        public void Select(int channel, bool immediate = false)
        {
            if (channel < -1 || channel >= gains.Length)
                throw new ArgumentOutOfRangeException(nameof(channel));
            if (target == channel && !immediate)
                return;
            target = channel;
            elapsed = 0;
            Array.Copy(gains, start, gains.Length);
            if (immediate)
            {
                for (int i = 0; i < gains.Length; i++)
                    gains[i] = i == target ? 1 : 0;
                Array.Copy(gains, start, gains.Length);
                elapsed = duration;
            }
        }
        public bool Tick(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0)
                throw new ArgumentOutOfRangeException(nameof(seconds));
            if (!IsTransitioning || seconds == 0)
                return false;
            elapsed = Math.Min(duration, elapsed + seconds);
            float t = (float)(elapsed / duration);
            for (int i = 0; i < gains.Length; i++)
                gains[i] = start[i] + ((i == target ? 1 : 0) - start[i]) * t;
            return true;
        }
    }
}
