using EscapeGame.Session.Contracts;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace EscapeGame.Session
{
    // Unity 메인 스레드에서 호출하는 세션 요청 실행기입니다.
    internal sealed class SessionRequestRunner
    {
        private bool running;
        private bool pendingCommand;

        public bool IsRunning => running;
        public bool IsBusy => running || pendingCommand;
        public bool HasPendingCommand => pendingCommand;

        public async Task RunAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken)
        {
            ValidateAction(action, cancellationToken);

            if (IsBusy)
            {
                throw new SessionResultException(SessionFailureCode.Busy);
            }

            await ExecuteAsync(action, cancellationToken);
        }

        public async Task RunAfterCurrentAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken)
        {
            ValidateAction(action, cancellationToken);

            if (pendingCommand)
            {
                throw new SessionResultException(SessionFailureCode.Busy);
            }

            pendingCommand = true;

            try
            {
                while (running)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await Task.Yield();
                }

                cancellationToken.ThrowIfCancellationRequested();


            }
            finally
            {
                pendingCommand = false;
            }

            await ExecuteAsync(action, cancellationToken);
        }

        private async Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken)
        {
            running = true;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                await action(cancellationToken);
            }
            finally
            {
                running = false;
            }
        }

        private static void ValidateAction(Func<CancellationToken, Task> action, CancellationToken cancellationToken)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            cancellationToken.ThrowIfCancellationRequested();
        }
    }
}