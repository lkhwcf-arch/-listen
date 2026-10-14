using EscapeGame.Session.Contracts;
using System;

namespace EscapeGame.Networking
{
    // 서버의 단계 문자열을 클라이언트 상태값으로 해석합니다.
    // 게임 시작 조건이나 상태 전환을 결정하지 않습니다.
    internal static class SessionPhaseParser
    {
        public static ClientSessionPhase Parse(string phase)
        {
            return phase switch
            {
                "Waiting" => ClientSessionPhase.Waiting,
                "Loading" => ClientSessionPhase.Loading,
                "Playing" => ClientSessionPhase.Playing,
                _ => throw new ArgumentException("알 수 없는 세션 단계입니다.")
            };
        }
    }
}
