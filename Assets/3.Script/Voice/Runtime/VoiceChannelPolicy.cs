using System;
using System.Security.Cryptography;
using System.Text;
namespace EscapeGame.Voice
{
    internal static class VoiceChannelPolicy
    {
        // 같은 서버 세션은 접속 주소(localhost/LAN)가 달라도 같은 음성방을 사용합니다.
        public static string ForSession(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId)) throw new ArgumentException(nameof(sessionId));
            using var hash = SHA256.Create();
            var bytes = hash.ComputeHash(Encoding.UTF8.GetBytes(sessionId));
            return "escape_" + BitConverter.ToString(bytes, 0, 16).Replace("-", "").ToLowerInvariant();
        }
    }
}
