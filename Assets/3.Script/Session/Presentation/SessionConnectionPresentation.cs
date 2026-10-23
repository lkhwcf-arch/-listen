namespace EscapeGame.Session.Presentation
{
    public sealed class SessionConnectionPresentation
    {
        public string Status
        {
            get;
        }
        public string Role
        {
            get;
        }
        public string Participants
        {
            get;
        }
        public string Message
        {
            get;
        }
        public bool CanJoin
        {
            get;
        }
        public bool CanLeave
        {
            get;
        }
        public bool IsLeaving
        {
            get;
        }
        public bool CanEditAddress
        {
            get;
        }
        public bool ShowWaitingRoom
        {
            get;
        }

        public SessionConnectionPresentation(string status, string role, string participants,
            string message, bool canJoin, bool canEditAddress, bool canLeave = false, bool isLeaving = false, bool showWaitingRoom = false)
        {
            CanLeave = canLeave;
            IsLeaving = isLeaving;
            Status = status;
            Role = role;
            Participants = participants;
            Message = message;
            CanJoin = canJoin;
            CanEditAddress = canEditAddress;
            ShowWaitingRoom = showWaitingRoom;
        }
    }
}
