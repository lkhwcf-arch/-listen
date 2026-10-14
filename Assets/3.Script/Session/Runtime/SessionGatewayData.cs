using System;
using System.Collections.Generic;
using EscapeGame.Session.Contracts;

namespace EscapeGame.Session
{
    internal sealed class SessionPlayerData
    {
        internal string Id { get; }
        internal string Role { get; }
        internal bool SceneLoaded { get; }
        internal SessionPlayerData(string id, string role, bool sceneLoaded)
        { Id = id; Role = role; SceneLoaded = sceneLoaded; }
    }
    internal sealed class SessionJoinData
    {
        internal string SessionId { get; }
        internal SessionPlayerData Player { get; }
        internal string ParticipantToken { get; }
        internal SessionJoinData(string sessionId, SessionPlayerData player, string participantToken)
        { SessionId = sessionId; Player = player; ParticipantToken = participantToken; }
    }
    internal sealed class SessionSnapshotData
    {
        internal string SessionId { get; }
        internal ClientSessionPhase Phase { get; }
        internal IReadOnlyList<SessionPlayerData> Players { get; }
        internal SessionSnapshotData(string sessionId, ClientSessionPhase phase, SessionPlayerData[] players)
        {
            SessionId = sessionId;
            Phase = phase;
            Players = players == null ? null : Array.AsReadOnly((SessionPlayerData[])players.Clone());
        }
    }
}
