using EscapeGame.Session.Contracts;
using System;
using System.Threading;
using EscapeGame.Readiness;
using EscapeGame.Timing;
using UnityEngine;

namespace EscapeGame.Session
{
    public sealed class SessionSceneLoadedDriver : MonoBehaviour
    {

        [SerializeField]
        private Behaviour[] requiredComponents = Array.Empty<Behaviour>();
        [SerializeField, Min(1)]
        private int reportRetryIntervalMs = 1000;

        private ISessionConnectionService service;
        private IMillisecondClock clock;
        private CancellationToken ownerLifetime;
        private SessionSceneLoadedController reporting;
        private CancellationTokenSource lifetime;

        public void ValidateConfiguration()
        {
            if (reportRetryIntervalMs <= 0)
                throw new ArgumentOutOfRangeException(nameof(reportRetryIntervalMs));
            if (!gameObject.scene.IsValid())
                throw new InvalidOperationException("준비를 확인할 씬이 유효하지 않습니다.");
            _ = new ActiveComponentsReadinessCheck(requiredComponents);
        }

        public void Initialize(ISessionConnectionService service,
            IMillisecondClock clock, CancellationToken ownerLifetime)
        {
            if (this.service != null) throw new InvalidOperationException("이미 초기화되었습니다.");
            if (service == null) throw new ArgumentNullException(nameof(service));
            if (clock == null) throw new ArgumentNullException(nameof(clock));
            ValidateConfiguration();
            this.service = service;
            this.clock = clock;
            this.ownerLifetime = ownerLifetime;
            if (isActiveAndEnabled) StartReporting();
        }

        private void OnEnable()
        {
            if (service != null) StartReporting();
        }

        private void StartReporting()
        {
            var readiness = new AllReadinessCheck(
                new LoadedSceneReadinessCheck(gameObject.scene),
                new ActiveComponentsReadinessCheck(requiredComponents));
            reporting = new SessionSceneLoadedController(service, readiness,
                new MillisecondInterval(clock, reportRetryIntervalMs));
            reporting.Prepared += OnPrepared;
            reporting.ReportConfirmed += OnReportConfirmed;
            reporting.ConnectionFailed += OnConnectionFailed;
            reporting.Failed += OnFailed;
            lifetime = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken, ownerLifetime);
        }

        // 활성 컴포넌트의 Start 이후 호출됩니다. 입장 응답만으로 준비 완료를 판정하지 않습니다.
        private void Update()
        {
            if (lifetime != null) reporting.Tick(lifetime.Token);
        }

        private void OnDisable()
        {
            lifetime?.Cancel();
            if (reporting != null)
            {
                reporting.Prepared -= OnPrepared;
                reporting.ReportConfirmed -= OnReportConfirmed;
                reporting.ConnectionFailed -= OnConnectionFailed;
                reporting.Failed -= OnFailed;
            }
            lifetime?.Dispose();
            lifetime = null;
            reporting = null;
        }

        private void OnPrepared() => Debug.Log("플레이 준비 완료: 씬 로드 및 필수 컴포넌트 활성 확인");
        private void OnReportConfirmed() => Debug.Log("로딩 완료 자동 보고: 서버 확인 완료");
        private void OnConnectionFailed(SessionFailureCode code)
        {
            Debug.LogWarning($"로딩 완료 자동 보고: 통신 실패, 재시도 예정. {code}");
        }
        private void OnFailed(SessionFailureCode code)
        {
            Debug.LogError($"로딩 완료 자동 보고 중단: {code}");
        }
    }
}
