using System;
using System.Threading;
using System.Threading.Tasks;
using EscapeGame.Networking;
using EscapeGame.Session.Contracts;
using EscapeGame.Timing;
using UnityEngine;

namespace EscapeGame.Session
{
    [RequireComponent(typeof(SessionPollingDriver), typeof(SessionSceneLoadedDriver), typeof(SessionHeartbeatDriver))]
    public sealed class SessionBootstrapper : MonoBehaviour
    {
        [SerializeField] private SessionPollingDriver pollingDriver;
        [SerializeField] private SessionSceneLoadedDriver sceneLoadedDriver;
        private SessionHeartbeatDriver heartbeatDriver;
        private SessionClient client;

        public ISessionConnectionService Service { get { EnsureInitialized(); return client; } }
        public SessionInfo State => Service.State;
        public bool IsBusy => Service.IsBusy;
        public string ServerBaseUrl => Service.ServerBaseUrl;

        private void Awake() => EnsureInitialized();

        private void EnsureInitialized()
        {
            if (client != null) return;
            if (pollingDriver == null) pollingDriver = GetComponent<SessionPollingDriver>();
            if (sceneLoadedDriver == null) sceneLoadedDriver = GetComponent<SessionSceneLoadedDriver>();
            if (pollingDriver == null || sceneLoadedDriver == null)
                throw new InvalidOperationException("자동 조회와 준비 보고 컴포넌트를 연결하세요.");
            heartbeatDriver = GetComponent<SessionHeartbeatDriver>();
            if (heartbeatDriver == null) throw new InvalidOperationException("SessionHeartbeatDriver를 연결하세요.");
            var heartbeatSettings = heartbeatDriver.Settings;
            pollingDriver.ValidateConfiguration();
            sceneLoadedDriver.ValidateConfiguration();

            client = new SessionClient(new HttpSessionGatewayFactory(heartbeatSettings.RequestTimeoutMs), destroyCancellationToken,
                exception => Debug.LogWarning($"세션 내부 진단: {exception.GetType().Name}: {exception.Message}"));
            var clock = new StopwatchMillisecondClock();
            heartbeatDriver.Initialize(client, clock, destroyCancellationToken);
            sceneLoadedDriver.Initialize(client, clock, destroyCancellationToken);
            pollingDriver.Initialize(client, clock, destroyCancellationToken);
        }

        // 기존 테스트 메뉴의 호출 경로를 유지하는 얇은 연결 메서드입니다.
        public Task<SessionCommandResult> JoinAsync(string address, CancellationToken token) => Service.JoinAsync(address, token);
        public Task<SessionCommandResult> ReportSceneLoadedAsync(CancellationToken token) => Service.ReportSceneLoadedAsync(token);
        public Task<SessionCommandResult> QueryAsync(CancellationToken token) => Service.QueryAsync(token);

        private void OnDestroy() => client?.Dispose();
    }
}
