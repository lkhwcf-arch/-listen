using System;
namespace EscapeGame.Session
{
    public sealed class SessionHeartbeatSettings
    {
        public int IntervalMs
        {
            get;
        }
        public int ConnectionTimeoutMs
        {
            get;
        }
        public int RequestTimeoutMs
        {
            get;
        }
        public SessionHeartbeatSettings(int intervalMs, int connectionTimeoutMs, int requestTimeoutMs)
        {
            if (intervalMs <= 0 || connectionTimeoutMs <= intervalMs || requestTimeoutMs <= 0 || requestTimeoutMs >= intervalMs)
                throw new ArgumentException("생존 확인은 0 < 요청 제한 < 전송 간격 < 연결 제한 순서의 정수 ms가 필요합니다.");
            IntervalMs = intervalMs;
            ConnectionTimeoutMs = connectionTimeoutMs;
            RequestTimeoutMs = requestTimeoutMs;
        }
    }
}
