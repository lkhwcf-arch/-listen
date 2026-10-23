namespace EscapeGame.Voice.Contracts
{
    public enum VoiceConnectionStatus
    {
        Idle = 0, Connecting = 1, Connected = 2, Disconnecting = 3, Failed = 4, Recovering = 5
    }
    public enum VoiceFailureCode
    {
        None = 0, InitializationFailed = 1, AuthenticationFailed = 2, LoginFailed = 3,
        JoinFailed = 4, ConnectionLost = 5, InUse = 6, CleanupFailed = 7, UnexpectedFailure = 8
    }
    // SDK 객체와 인증 정보를 공개하지 않는 읽기 전용 상태입니다.
    public sealed class VoiceInfo
    {
        public VoiceConnectionStatus Status
        {
            get;
        }
        public VoiceFailureCode FailureCode
        {
            get;
        }
        public string SessionId
        {
            get;
        }
        public string PlayerId
        {
            get;
        }
        public string ChannelName
        {
            get;
        }
        public int ParticipantCount
        {
            get;
        }
        public VoiceInfo(VoiceConnectionStatus status, VoiceFailureCode code = VoiceFailureCode.None,
            string sessionId = null, string playerId = null, string channelName = null, int participantCount = 0)
        {
            Status = status;
            FailureCode = code;
            SessionId = sessionId;
            PlayerId = playerId;
            ChannelName = channelName;
            ParticipantCount = participantCount;
        }
    }
}
