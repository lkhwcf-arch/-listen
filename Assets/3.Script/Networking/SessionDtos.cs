using System;
namespace EscapeGame.Networking
{
    [Serializable]
    internal sealed class LeaveResponseDto
    {
        public bool success;
        public string code;
    }

    [Serializable]
    internal sealed class HeartbeatResponseDto
    {
        public bool success;
        public string code;
    }

    [Serializable]
    internal sealed class PlayerDto
    {
        public string id;
        public string role;
        public bool ready;
    }

    [Serializable]
    internal sealed class JoinSessionResponseDto
    {
        public bool success;
        public string sessionId;
        public PlayerDto player;
        public string participantToken;
        public string code;
    }
    [Serializable]
    internal sealed class SessionSnapshotDto
    {
        public string sessionId;
        public string phase;
        public PlayerDto[] players;
    }
    [Serializable]
    internal sealed class SceneLoadedResponseDto
    {
        public bool success;
        public SessionSnapshotDto snapshot;
        public string code;
    }

    [Serializable]
    internal sealed class QuerySessionResponseDto
    {
        public bool success;
        public SessionSnapshotDto snapshot;
        public string code;
    }
}
