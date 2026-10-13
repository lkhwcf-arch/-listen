using System;
namespace EscapeGame.Networking
{
    [Serializable]
    public sealed class PlayerDto
    {
        public string id;
        public string role;
        public bool ready;
    }

    [Serializable]
    public sealed class JoinSessionResponse
    {
        public bool success;
        public string sessionId;
        public PlayerDto player;
        public string participantToken;
        public string code;
    }
    [Serializable]
    public sealed class SessionSnapshotDto
    {
        public string sessionId;
        public string phase;
        public PlayerDto[] players;
    }
    [Serializable]
    public sealed class ReadySessionResponse
    {
        public bool success;
        public SessionSnapshotDto snapshot;
        public string code;
    }

    [Serializable]
    public sealed class QuerySessionResponse
    {
        public bool success;
        public SessionSnapshotDto snapshot;
        public string code;
    }
}

