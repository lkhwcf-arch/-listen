using System;

namespace EscapeGame.Data
{
    // CSV 표기는 유지하고, 진행 판정에서 알 수 없는 규칙으로 대체하지 않습니다.
    public static class RoomExitRules
    {
        public const string SolvedEach = "SolvedEach";
        public const string BothSolved = "BothSolved";
        public static bool RequiresBothPuzzles(string rule) => rule switch
        {
            SolvedEach => false,
            BothSolved => true,
            _ => throw new ArgumentException("지원하지 않는 방 탈출 규칙입니다: " + rule, nameof(rule))
        };
    }
}
