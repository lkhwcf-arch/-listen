using EscapeGame.Session.Contracts;
using System;
using System.Threading;
using EscapeGame.Timing;
using UnityEngine;

namespace EscapeGame.Session
{
    // Unity 생명주기와 표시만 담당합니다. 시간/조회 규칙은 일반 C# 모듈에 있습니다.
    public sealed class SessionPollingDriver : MonoBehaviour
    {

        [SerializeField, Min(1)]
        private int pollingIntervalMs = 1000;

        [SerializeField, Min(1)]
        private int playingPollingIntervalMs = 5000;

        private ISessionConnectionService service;
        private IMillisecondClock clock;
        private SessionPollingController polling;
        private CancellationTokenSource lifetime;
        private CancellationToken ownerLifetime;

        public void Initialize(ISessionConnectionService service,
            IMillisecondClock clock, CancellationToken ownerLifetime = default)
        {
            if (this.service != null)
                throw new InvalidOperationException("이미 초기화되었습니다.");
            if (service == null)
                throw new ArgumentNullException(nameof(service));
            if (clock == null)
                throw new ArgumentNullException(nameof(clock));
            ValidateConfiguration();
            this.service = service;
            this.clock = clock;
            this.ownerLifetime = ownerLifetime;
            if (isActiveAndEnabled)
                StartPolling();
        }

        public void ValidateConfiguration()
        {
            if (playingPollingIntervalMs <= 0)
                throw new ArgumentOutOfRangeException(nameof(playingPollingIntervalMs), "게임 중 조회 간격은 1ms 이상이어야 합니다.");
            if (pollingIntervalMs <= 0)
                throw new ArgumentOutOfRangeException(nameof(pollingIntervalMs), "조회 간격은 1ms 이상이어야 합니다.");
        }

        private void OnEnable()
        {
            if (service != null)
                StartPolling();
        }

        private void StartPolling()
        {
            if (lifetime != null)
                return;
            polling = new SessionPollingController(service,
                new MillisecondInterval(clock, pollingIntervalMs),
                new MillisecondInterval(clock, playingPollingIntervalMs));
            polling.ConnectionFailed += OnConnectionFailed;
            polling.ConnectionRecovered += OnConnectionRecovered;
            polling.Stopped += OnStopped;
            lifetime = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken, ownerLifetime);
        }

        private void Update()
        {
            if (lifetime != null)
                polling.Tick(lifetime.Token);
        }

        private void OnDisable()
        {
            lifetime?.Cancel();
            if (polling != null)
            {
                polling.ConnectionFailed -= OnConnectionFailed;
                polling.ConnectionRecovered -= OnConnectionRecovered;
                polling.Stopped -= OnStopped;
            }
            lifetime?.Dispose();
            lifetime = null;
            polling = null;
        }

        private void OnConnectionFailed(SessionFailureCode code)
        {
            Debug.LogWarning($"세션 자동 조회: 연결 실패, 재시도 예정. {code}");
        }
        private void OnConnectionRecovered() => Debug.Log("세션 자동 조회: 서버 통신 복구");
        private void OnStopped(SessionFailureCode code)
        {
            Debug.LogError($"세션 자동 조회 중단: {code}");
        }
    }
}
