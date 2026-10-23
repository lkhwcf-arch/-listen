using System;
using System.Threading;
using System.Threading.Tasks;

namespace EscapeGame.Session.Contracts
{
    public sealed class RoomDecision
    {
        public int RoomNo
        {
            get;
        }
        public bool Value
        {
            get;
        }
        public bool DoorAllowed
        {
            get;
        }
        public RoomDecision(int roomNo, bool value, bool doorAllowed)
        {
            if (roomNo < 1)
                throw new ArgumentOutOfRangeException(nameof(roomNo));
            RoomNo = roomNo;
            Value = value;
            DoorAllowed = doorAllowed;
        }
    }
    // Public domain contract. No generated message, header or HTTP types reach gameplay consumers.
    public interface ISessionRoomService
    {
        Task<RoomDecision> SubmitRoomAnswerAsync(int roomNo, string[] answers, CancellationToken token);
        Task<RoomDecision> CheckRoomDoorAsync(int roomNo, CancellationToken token);
    }
}
