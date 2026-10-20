using System;

namespace EscapeGame.Player
{
    public readonly struct PlayerMoveStep
    {
        public int RightMm { get; }
        public int UpMm { get; }
        public int ForwardMm { get; }
        public PlayerMoveStep(int rightMm, int upMm, int forwardMm)
        { RightMm = rightMm; UpMm = upMm; ForwardMm = forwardMm; }
    }

    // Unity/입력/HTTP를 모르는 가변 이동 상태. 1ms 적분으로 프레임 분할에 따른 절삭 차이를 줄입니다.
    public sealed class PlayerMovementMotor
    {
        private readonly PlayerCharacterSettings settings;
        private readonly int gravityMmPerSecondSquared;
        private readonly long maximumFallSpeedUmPerSecond;
        private long rightVelocity, forwardVelocity, upVelocity; // 정수 μm/s
        private long rightRemainder, forwardRemainder, upRemainder; // 2배 적분 분자, mm 미만 보존
        public long RightVelocityUmPerSecond => rightVelocity;
        public long ForwardVelocityUmPerSecond => forwardVelocity;
        public long UpVelocityUmPerSecond => upVelocity;

        public PlayerMovementMotor(PlayerCharacterSettings settings, int gravityMmPerSecondSquared, int maximumFallSpeedMmPerSecond)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            if (gravityMmPerSecondSquared >= 0 || gravityMmPerSecondSquared < -1000000 ||
                maximumFallSpeedMmPerSecond <= 0 || maximumFallSpeedMmPerSecond > 100000)
                throw new ArgumentOutOfRangeException("중력·최대 낙하 속도의 정수 범위를 확인하세요.");
            this.gravityMmPerSecondSquared = gravityMmPerSecondSquared;
            maximumFallSpeedUmPerSecond = (long)maximumFallSpeedMmPerSecond * 1000;
        }

        public PlayerMoveStep Tick(PlayerMoveCommand command, int elapsedMs)
        {
            if (elapsedMs < 0 || elapsedMs > 100) throw new ArgumentOutOfRangeException(nameof(elapsedMs), "한 번에 0..100ms만 처리합니다.");
            long length = CeilingSqrt((long)command.Right * command.Right + (long)command.Forward * command.Forward);
            long divisor = Math.Max(PlayerMoveCommand.AxisScale, length);
            long speed = (long)settings.WalkSpeedMmPerSecond * 1000;
            long targetRight = speed * command.Right / divisor;
            long targetForward = speed * command.Forward / divisor;
            long acceleration = command.IsIdle ? settings.DecelerationMmPerSecondSquared : settings.AccelerationMmPerSecondSquared;
            for (int ms = 0; ms < elapsedMs; ms++)
            {
                long oldRight = rightVelocity, oldForward = forwardVelocity, oldUp = upVelocity;
                Approach(targetRight, targetForward, acceleration);
                upVelocity = Math.Max(-maximumFallSpeedUmPerSecond, upVelocity + gravityMmPerSecondSquared);
                // (이전 속도 + 새 속도) / 2 × 1ms. μm/s에서 mm로의 분모는 2,000,000입니다.
                rightRemainder += oldRight + rightVelocity;
                forwardRemainder += oldForward + forwardVelocity;
                upRemainder += oldUp + upVelocity;
            }
            return new PlayerMoveStep(Consume(ref rightRemainder), Consume(ref upRemainder), Consume(ref forwardRemainder));
        }

        private void Approach(long targetRight, long targetForward, long amount)
        {
            long dx = targetRight - rightVelocity, dz = targetForward - forwardVelocity;
            long length = CeilingSqrt(dx * dx + dz * dz);
            if (length <= amount) { rightVelocity = targetRight; forwardVelocity = targetForward; return; }
            long sx = dx * amount / length, sz = dz * amount / length;
            // 최저 단위 가속에서도 양 축 절삭 때문에 영구 정지하지 않습니다.
            if (sx == 0 && sz == 0)
            {
                if (Math.Abs(dx) >= Math.Abs(dz)) sx = Math.Sign(dx);
                else sz = Math.Sign(dz);
            }
            rightVelocity += sx; forwardVelocity += sz;
        }

        private static int Consume(ref long remainder)
        { int result = (int)(remainder / 2000000); remainder %= 2000000; return result; }

        private static long CeilingSqrt(long value)
        {
            if (value == 0) return 0;
            long estimate = value, next = (estimate + 1) / 2;
            while (next < estimate) { estimate = next; next = (estimate + value / estimate) / 2; }
            return estimate * estimate == value ? estimate : estimate + 1;
        }

        public void OnGrounded() { if (upVelocity < 0) { upVelocity = 0; upRemainder = 0; } }
        public void Reset()
        {
            rightVelocity = forwardVelocity = upVelocity = 0;
            rightRemainder = forwardRemainder = upRemainder = 0;
        }
    }
}
