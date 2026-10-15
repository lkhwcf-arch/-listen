using System;
using System.Threading;
using System.Threading.Tasks;
using EscapeGame.Session.Contracts;
using EscapeGame.Timing;

namespace EscapeGame.Session
{
    // 공개 계약과 정수 시계만 사용하는 생존 확인/연결 감시입니다.
    public sealed class SessionHeartbeatController : IDisposable
    {
        private readonly ISessionConnectionService service;
        private readonly IMillisecondClock clock;
        private readonly SessionHeartbeatSettings settings;
        private readonly MillisecondInterval interval;
        private string playerId;
        private long lastConfirmedMs;
        private bool sending;
        private bool disposed;

        public SessionHeartbeatController(ISessionConnectionService service, IMillisecondClock clock,
            SessionHeartbeatSettings settings)
        {
            this.service = service ?? throw new ArgumentNullException(nameof(service));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            interval = new MillisecondInterval(clock, settings.IntervalMs);
            service.StateChanged += OnStateChanged;
            OnStateChanged(service.State);
        }

        private void OnStateChanged(SessionInfo state)
        {
            string current = state.IsJoined ? state.PlayerId : null;
            if (current == playerId) return;
            playerId = current;
            lastConfirmedMs = clock.ElapsedMilliseconds;
            interval.Restart(false);
        }

        public void Tick(CancellationToken token)
        {
            if (disposed || token.IsCancellationRequested || !service.State.IsJoined) return;
            if (clock.ElapsedMilliseconds - lastConfirmedMs >= settings.ConnectionTimeoutMs)
            {
                service.Disconnect(SessionFailureCode.ConnectionTimedOut);
                return;
            }
            if (sending || !interval.TryConsume()) return;
            sending = true;
            _ = SendAsync(playerId, token);
        }

        private async Task SendAsync(string requestedPlayer, CancellationToken token)
        {
            try
            {
                var result = await service.HeartbeatAsync(token);
                if (disposed || token.IsCancellationRequested || !service.State.IsJoined || requestedPlayer != playerId) return;
                if (clock.ElapsedMilliseconds - lastConfirmedMs >= settings.ConnectionTimeoutMs)
                {
                    service.Disconnect(SessionFailureCode.ConnectionTimedOut);
                    return;
                }
                if (result.Outcome == SessionCommandOutcome.Completed)
                    lastConfirmedMs = clock.ElapsedMilliseconds;
                else if (result.Outcome == SessionCommandOutcome.Failed &&
                    result.Code != SessionFailureCode.ConnectionFailed && result.Code != SessionFailureCode.Busy)
                    service.Disconnect(result.Code);
            }
            finally { sending = false; }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            service.StateChanged -= OnStateChanged;
        }
    }
}
