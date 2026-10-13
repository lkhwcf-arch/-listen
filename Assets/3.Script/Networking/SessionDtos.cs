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
    public sealed class JoinSessionResponseDto
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
    public sealed class SceneLoadedResponseDto
    {
        public bool success;
        public SessionSnapshotDto snapshot;
        public string code;
    }

    [Serializable]
    public sealed class QuerySessionResponseDto
    {
        public bool success;
        public SessionSnapshotDto snapshot;
        public string code;
    }
}

