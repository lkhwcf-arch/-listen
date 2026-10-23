using System.Threading;
using System.Threading.Tasks;
using EscapeGame.Session.Contracts;

namespace EscapeGame.Session
{
    internal sealed class SessionRoomDecisionData
    {
        internal SessionSnapshotData Snapshot
        {
            get;
        }
        internal RoomDecision Decision
        {
            get;
        }
        internal SessionRoomDecisionData(SessionSnapshotData snapshot, RoomDecision decision)
        {
            Snapshot = snapshot;
            Decision = decision;
        }
    }
    internal interface ISessionRoomGateway
    {
        Task<SessionRoomDecisionData> DecideRoomAsync(string sessionId, string participantToken, int roomNo, string[] answers, CancellationToken token);
    }
}
