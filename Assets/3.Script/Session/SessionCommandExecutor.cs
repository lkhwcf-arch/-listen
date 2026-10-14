using System;
using System.Threading;
using System.Threading.Tasks;
using EscapeGame.Session.Contracts;

namespace EscapeGame.Session
{
    internal static class SessionCommandExecutor
    {
        internal static async Task<SessionCommandResult> ExecuteAsync(
            Func<CancellationToken, Task> command, CancellationToken token, Action<Exception> diagnostic = null)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            try
            {
                token.ThrowIfCancellationRequested();
                await command(token);
                token.ThrowIfCancellationRequested();
                return SessionCommandResult.Completed();
            }
            catch (OperationCanceledException) { return SessionCommandResult.Cancelled(); }
            catch (Exception exception)
            {
                diagnostic?.Invoke(exception);
                var code = exception is SessionResultException failure ? failure.Code :
                    exception is ArgumentException ? SessionFailureCode.InvalidInput :
                    exception is InvalidOperationException ? SessionFailureCode.InvalidState :
                    SessionFailureCode.UnexpectedFailure;
                return SessionCommandResult.Failed(code);
            }
        }
    }
}
