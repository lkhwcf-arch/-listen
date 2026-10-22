using System;

namespace EscapeGame.World
{
    // 문 좌표계: X=문 폭, Y=높이, Z=문 앞뒤. 얇은 경계에서 떨려도 중복 입장하지 않습니다.
    public sealed class DoorwayCrossing
    {
        private int side;
        private readonly double margin;
        public DoorwayCrossing(double margin) { if (margin <= 0) throw new ArgumentOutOfRangeException(nameof(margin)); this.margin = margin; }
        public void Reset() => side = 0;
        public bool Tick(double x0, double y0, double z0, double x1, double y1, double z1,
            double halfWidth, double halfHeight, bool open)
        {
            int next = z1 > margin ? 1 : z1 < -margin ? -1 : 0;
            if (!open) { side = next; return false; }
            if (next == 0) return false;
            int previous = side; side = next;
            if (previous == 0 || previous == next || Math.Abs(z1 - z0) < .000001) return false;
            double t = -z0 / (z1 - z0);
            // 경계의 작은 중립 구간 안에서 시작한 프레임도 안정된 반대편에 도달한 뒤 확정합니다.
            t = Math.Max(0, Math.Min(1, t));
            double x = x0 + (x1 - x0) * t, y = y0 + (y1 - y0) * t;
            return Math.Abs(x) <= halfWidth && Math.Abs(y) <= halfHeight;
        }
    }
}
