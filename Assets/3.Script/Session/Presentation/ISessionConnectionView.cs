using System;

namespace EscapeGame.Session.Presentation
{
    public interface ISessionConnectionView
    {
        string ServerAddress { get; }
        event Action JoinRequested;
        event Action LeaveRequested;
        void ShowDisconnected(string message);
        void Render(SessionConnectionPresentation presentation);
    }
}
