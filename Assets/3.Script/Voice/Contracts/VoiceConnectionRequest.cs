using System;
namespace EscapeGame.Voice.Contracts
{
    public sealed class VoiceConnectionRequest
    {
        public string SessionId
        {
            get;
        }
        public string PlayerId
        {
            get;
        }
        public string DisplayName
        {
            get;
        }
        public VoiceConnectionRequest(string sessionId, string playerId, string displayName)
        {
            if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(playerId) || string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("음성 접속에는 세션, 참가자 ID와 표시 이름이 필요합니다.");
            SessionId = sessionId;
            PlayerId = playerId;
            DisplayName = displayName;
        }
    }
}
