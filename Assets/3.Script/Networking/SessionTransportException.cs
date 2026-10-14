using System;

namespace EscapeGame.Networking
{
    internal sealed class SessionTransportException : Exception
    {
        public SessionTransportException(string message) : base(message) { }
    }
}