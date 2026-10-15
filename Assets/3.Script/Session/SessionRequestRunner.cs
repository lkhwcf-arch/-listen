using EscapeGame.Session.Contracts;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace EscapeGame.Session
{
    // Unity 메인 스레드에서 호출하는 세션 요청 실행기입니다.
    internal sealed class SessionRequestRunner
    {
        private bool exclusivePending;
        private bool running;
        private bool heartbeatRunning;
        public bool IsHeartbeatRunning => heartbeatRunning;
        private bool pendingCommand;

        public bool IsRunning => running;
        public bool IsBusy => running || pendingCommand || exclusivePending;
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

            if (pendingCommand || exclusivePending)
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

        // 생존 확인은 일반 조회/보고 대기열과 독립적이며 이 경로에서도 하나만 실행합니다.
        public async Task RunHeartbeatAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken)
        {
            ValidateAction(action, cancellationToken);
            if (heartbeatRunning || exclusivePending) throw new SessionResultException(SessionFailureCode.Busy);
            await ExecuteAsync(action, cancellationToken, true);
        }

        // 종료 명령은 취소된 기존 요청이 정리될 때까지 기다린 후 독점 실행합니다.
        public async Task RunExclusiveAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken)
        {
            ValidateAction(action, cancellationToken);
            if (exclusivePending) throw new SessionResultException(SessionFailureCode.Busy);
            exclusivePending = true;
            try
            {
                while (running || heartbeatRunning || pendingCommand)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await Task.Yield();
                }
                await ExecuteAsync(action, cancellationToken);
            }
            finally { exclusivePending = false; }
        }

        private async Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken, bool heartbeat = false)
        {
            if (heartbeat) heartbeatRunning = true; else running = true;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                await action(cancellationToken);
            }
            finally
            {
                if (heartbeat) heartbeatRunning = false; else running = false;
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
