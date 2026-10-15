namespace EscapeGame.Session.Contracts
{
    // 소비자에게 공개하는 값입니다. 인증 정보와 상태 변경 메서드는 포함하지 않습니다.
    public sealed class SessionInfo
    {
        public string SessionId { get; }
        public string PlayerId { get; }
        public string Role { get; }
        public ClientSessionPhase? Phase { get; }
        public int ParticipantCount { get; }
        public bool IsSceneLoaded { get; }
        public SessionConnectionStatus ConnectionStatus { get; }
        public SessionFailureCode DisconnectReason { get; }
        public bool IsJoined => ConnectionStatus == SessionConnectionStatus.Connected && !string.IsNullOrEmpty(SessionId);

        public SessionInfo(string sessionId, string playerId, string role,
            ClientSessionPhase? phase, int participantCount, bool isSceneLoaded)
            : this(sessionId, playerId, role, phase, participantCount, isSceneLoaded,
                string.IsNullOrEmpty(sessionId) ? SessionConnectionStatus.NotConnected : SessionConnectionStatus.Connected,
                SessionFailureCode.None) { }

        public SessionInfo(string sessionId, string playerId, string role,
            ClientSessionPhase? phase, int participantCount, bool isSceneLoaded,
            SessionConnectionStatus connectionStatus, SessionFailureCode disconnectReason)
        {
            ConnectionStatus = connectionStatus;
            DisconnectReason = disconnectReason;
            SessionId = sessionId;
            PlayerId = playerId;
            Role = role;
            Phase = phase;
            ParticipantCount = participantCount;
            IsSceneLoaded = isSceneLoaded;
        }
    }
}
