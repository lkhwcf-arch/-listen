using System;
using System.Threading;
using System.Threading.Tasks;
using EscapeGame.Readiness;
using EscapeGame.Session.Contracts;
using EscapeGame.Timing;

namespace EscapeGame.Session
{
    public enum SceneLoadedReportPhase { WaitingForPreparation, WaitingToReport, Reporting, RetryPending, Completed, Failed }

    public sealed class SessionSceneLoadedController
    {
        private readonly ISessionConnectionService service;
        private readonly IReadinessCheck readiness;
        private readonly MillisecondInterval retryInterval;
        private string playerId;
        private bool prepared;
        private bool transportFailed;
        public SceneLoadedReportPhase Phase { get; private set; }
        public event Action Prepared;
        public event Action ReportConfirmed;
        public event Action<SessionFailureCode> ConnectionFailed;
        public event Action<SessionFailureCode> Failed;

        public SessionSceneLoadedController(ISessionConnectionService service,
            IReadinessCheck readiness, MillisecondInterval retryInterval)
        {
            this.service = service ?? throw new ArgumentNullException(nameof(service));
            this.readiness = readiness ?? throw new ArgumentNullException(nameof(readiness));
            this.retryInterval = retryInterval ?? throw new ArgumentNullException(nameof(retryInterval));
        }
        public void Tick(CancellationToken token)
        {
            if (token.IsCancellationRequested || !service.State.IsJoined) return;
            if (playerId != service.State.PlayerId)
            {
                playerId = service.State.PlayerId;
                prepared = transportFailed = false;
                Phase = SceneLoadedReportPhase.WaitingForPreparation;
                retryInterval.Restart();
            }
            if (
                Phase == SceneLoadedReportPhase.Completed || Phase == SceneLoadedReportPhase.Failed ||
                Phase == SceneLoadedReportPhase.Reporting || !readiness.IsReady) return;
            if (!prepared)
            {
                prepared = true;
                Phase = SceneLoadedReportPhase.WaitingToReport;
                Prepared?.Invoke();
            }
            if (service.State.IsSceneLoaded) { Complete(); return; }
            if (!retryInterval.TryConsume()) return;
            Phase = SceneLoadedReportPhase.Reporting;
            _ = ReportAsync(token);
        }
        private async Task ReportAsync(CancellationToken token)
        {
            // 요청 대기 정책은 서비스 내부에 있습니다. 자동화는 Busy 결과만 이해합니다.
            string requestedPlayer = service.State.PlayerId;
            var result = await service.ReportSceneLoadedAsync(token);
            if (token.IsCancellationRequested || !service.State.IsJoined || service.State.PlayerId != requestedPlayer) return;
            if (result.Outcome == SessionCommandOutcome.Completed) { Complete(); return; }
            if (result.Outcome == SessionCommandOutcome.Cancelled || result.Code == SessionFailureCode.Busy)
            {
                Phase = SceneLoadedReportPhase.WaitingToReport;
                retryInterval.Restart(false);
                return;
            }
            if (SessionPollingFailurePolicy.Resolve(result) == SessionPollingFailureAction.Retry)
            {
                Phase = SceneLoadedReportPhase.RetryPending;
                retryInterval.Restart(false);
                if (!transportFailed)
                {
                    transportFailed = true;
                    ConnectionFailed?.Invoke(result.Code);
                }
            }
            else
            {
                Phase = SceneLoadedReportPhase.Failed;
                Failed?.Invoke(result.Code);
            }
        }
        private void Complete()
        {
            Phase = SceneLoadedReportPhase.Completed;
            ReportConfirmed?.Invoke();
        }
    }
}
