using System;

namespace EscapeGame.Session.Contracts
{
    public enum SessionCommandOutcome
    {
        Completed, Cancelled, Failed
    }

    public sealed class SessionCommandResult
    {
        public SessionCommandOutcome Outcome
        {
            get;
        }
        public SessionFailureCode Code
        {
            get;
        }
        private SessionCommandResult(SessionCommandOutcome outcome, SessionFailureCode code)
        {
            Outcome = outcome;
            Code = code;
        }
        public static SessionCommandResult Completed() => new SessionCommandResult(SessionCommandOutcome.Completed, SessionFailureCode.None);
        public static SessionCommandResult Cancelled() => new SessionCommandResult(SessionCommandOutcome.Cancelled, SessionFailureCode.None);
        public static SessionCommandResult Failed(SessionFailureCode code)
        {
            if (code == SessionFailureCode.None || !Enum.IsDefined(typeof(SessionFailureCode), code))
                throw new ArgumentOutOfRangeException(nameof(code));
            return new SessionCommandResult(SessionCommandOutcome.Failed, code);
        }
    }
}
