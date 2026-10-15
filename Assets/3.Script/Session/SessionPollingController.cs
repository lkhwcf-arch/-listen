using System;
using System.Threading;
using System.Threading.Tasks;
using EscapeGame.Session.Contracts;
using EscapeGame.Timing;

namespace EscapeGame.Session
{
    // 공개 계약만 사용하는 자동화입니다. 통신 구현/내부 상태를 참조하지 않습니다.
    public sealed class SessionPollingController
    {
        private readonly ISessionConnectionService service;
        private readonly MillisecondInterval interval;
        private string playerId;
        private bool stopped;
        private bool connectionFailed;
        public event Action<SessionFailureCode> ConnectionFailed;
        public event Action ConnectionRecovered;
        public event Action<SessionFailureCode> Stopped;

        public SessionPollingController(ISessionConnectionService service, MillisecondInterval interval)
        {
            this.service = service ?? throw new ArgumentNullException(nameof(service));
            this.interval = interval ?? throw new ArgumentNullException(nameof(interval));
        }
        public void Tick(CancellationToken token)
        {
            if (token.IsCancellationRequested || !service.State.IsJoined) return;
            if (playerId != service.State.PlayerId)
            {
                playerId = service.State.PlayerId;
                stopped = connectionFailed = false;
                interval.Restart();
            }
            if (stopped || service.IsBusy) return;
            if (interval.TryConsume()) _ = PollAsync(token);
        }
        private async Task PollAsync(CancellationToken token)
        {
            string requestedPlayer = service.State.PlayerId;
            var result = await service.QueryAsync(token);
            if (token.IsCancellationRequested || !service.State.IsJoined || service.State.PlayerId != requestedPlayer) return;
            if (result.Outcome != SessionCommandOutcome.Completed)
            {
                switch (SessionPollingFailurePolicy.Resolve(result))
                {
                    case SessionPollingFailureAction.Retry:
                        if (!connectionFailed)
                        {
                            connectionFailed = true;
                            ConnectionFailed?.Invoke(result.Code);
                        }
                        return;
                    case SessionPollingFailureAction.Stop:
                        stopped = true;
                        Stopped?.Invoke(result.Code);
                        return;
                    default: return;
                }
            }
            if (connectionFailed)
            {
                connectionFailed = false;
                ConnectionRecovered?.Invoke();
            }
        }
    }
}
