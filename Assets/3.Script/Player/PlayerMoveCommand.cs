using System;

namespace EscapeGame.Player
{
    // 장치·엔진 객체 참조 대신 직렬화 가능한 정수 입력을 코어에 전달합니다.
    public readonly struct PlayerMoveCommand
    {
        public const int AxisScale = 1000;
        public int Right { get; }
        public int Forward { get; }
        public bool IsIdle => Right == 0 && Forward == 0;
        public PlayerMoveCommand(int right, int forward)
        {
            if (right < -AxisScale || right > AxisScale || forward < -AxisScale || forward > AxisScale)
                throw new ArgumentOutOfRangeException("입력 축은 -1000..1000 범위입니다.");
            Right = right; Forward = forward;
        }
    }
}
