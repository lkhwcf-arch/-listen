namespace EscapeGame.Session.Contracts
{
    public enum SessionFailureCode
    {
        None = 0,
        ConnectionFailed = 1,
        SessionFull = 2,
        SessionNotFound = 3,
        InvalidParticipant = 4,
        ParticipantTokenRequired = 5,
        InvalidInput = 6,
        InvalidState = 7,
        Busy = 8,
        InvalidResponse = 9,
        UnexpectedFailure = 10
    }
}
