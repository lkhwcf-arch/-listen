using System;

namespace EscapeGame.Session.Presentation
{
    public interface ISessionConnectionView
    {
        string ServerAddress { get; }
        event Action JoinRequested;
        void Render(SessionConnectionPresentation presentation);
    }
}
