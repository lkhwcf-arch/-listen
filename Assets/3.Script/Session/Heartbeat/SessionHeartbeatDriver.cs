using System;
using System.Threading;
using EscapeGame.Session.Contracts;
using EscapeGame.Timing;
using UnityEngine;

namespace EscapeGame.Session
{
    public sealed class SessionHeartbeatDriver : MonoBehaviour
    {
        [SerializeField, Min(1)] private int heartbeatIntervalMs = 3000;
        [SerializeField, Min(1)] private int connectionTimeoutMs = 10000;
        [SerializeField, Min(1)] private int requestTimeoutMs = 2000;
        private ISessionConnectionService service;
        private IMillisecondClock clock;
        private CancellationToken ownerLifetime;
        private CancellationTokenSource lifetime;
        private SessionHeartbeatController heartbeat;

        public SessionHeartbeatSettings Settings => new SessionHeartbeatSettings(heartbeatIntervalMs, connectionTimeoutMs, requestTimeoutMs);
        public void Initialize(ISessionConnectionService service, IMillisecondClock clock, CancellationToken ownerLifetime)
        {
            if (this.service != null) throw new InvalidOperationException("이미 초기화되었습니다.");
            this.service = service ?? throw new ArgumentNullException(nameof(service));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.ownerLifetime = ownerLifetime;
            _ = Settings;
            if (isActiveAndEnabled) StartHeartbeat();
        }
        private void OnEnable() { if (service != null) StartHeartbeat(); }
        private void StartHeartbeat()
        {
            lifetime = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken, ownerLifetime);
            heartbeat = new SessionHeartbeatController(service, clock, Settings);
        }
        private void Update() { if (lifetime != null) heartbeat.Tick(lifetime.Token); }
        private void OnDisable()
        {
            lifetime?.Cancel();
            heartbeat?.Dispose();
            lifetime?.Dispose();
            heartbeat = null;
            lifetime = null;
        }
    }
}
