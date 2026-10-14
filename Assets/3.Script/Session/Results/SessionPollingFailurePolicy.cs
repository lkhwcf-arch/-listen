using System;
using EscapeGame.Session.Contracts;

namespace EscapeGame.Session
{
    public enum SessionPollingFailureAction { Ignore, Retry, Stop }
    public static class SessionPollingFailurePolicy
    {
        public static SessionPollingFailureAction Resolve(SessionCommandResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (result.Outcome != SessionCommandOutcome.Failed || result.Code == SessionFailureCode.Busy)
                return SessionPollingFailureAction.Ignore;
            return result.Code == SessionFailureCode.ConnectionFailed
                ? SessionPollingFailureAction.Retry : SessionPollingFailureAction.Stop;
        }
    }
}
