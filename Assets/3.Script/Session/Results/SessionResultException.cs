using System;
using EscapeGame.Session.Contracts;

namespace EscapeGame.Session
{
    internal sealed class SessionResultException : Exception
    {
        internal SessionFailureCode Code
        {
            get;
        }
        internal SessionResultException(SessionFailureCode code, Exception inner = null)
            : base($"세션 처리 실패: {code}", inner)
        {
            Code = code;
        }
    }
}
