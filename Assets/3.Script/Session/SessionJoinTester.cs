using System;
using System.Threading;
using System.Threading.Tasks;
using EscapeGame.Session.Contracts;
using EscapeGame.Session.Presentation;
using UnityEngine;

namespace EscapeGame.Session
{
    [RequireComponent(typeof(SessionBootstrapper))]
    public sealed class SessionJoinTester : MonoBehaviour
    {
        [SerializeField] private string serverBaseUrl = "http://127.0.0.1:3000";
        [SerializeField] private SessionBootstrapper bootstrapper;
        private ISessionConnectionService service;
        private SessionInfo previous;
        private void Awake()
        {
            if (bootstrapper == null) bootstrapper = GetComponent<SessionBootstrapper>();
            if (bootstrapper == null) throw new InvalidOperationException("SessionBootstrapper를 연결하세요.");
            service = bootstrapper.Service;
            previous = service.State;
        }
        private void OnEnable() => service.StateChanged += OnStateChanged;
        private void OnDisable()
        {
            if (service != null) service.StateChanged -= OnStateChanged;
        }
        [ContextMenu("Join Server Session")]
        private async void JoinServerSession() => await ExecuteCommandAsync(token => service.JoinAsync(serverBaseUrl, token));
        [ContextMenu("Report Scene Loaded")]
        private async void ReportSceneLoaded() => await ExecuteCommandAsync(service.ReportSceneLoadedAsync);
        [ContextMenu("Query Server Session")]
        private async void QueryServerSession() => await ExecuteCommandAsync(service.QueryAsync);
        private async Task ExecuteCommandAsync(Func<CancellationToken, Task<SessionCommandResult>> command)
        {
            if (!Application.isPlaying) { Debug.LogWarning("Play 모드에서 실행하세요."); return; }
            var result = await command(destroyCancellationToken);
            if (result.Outcome == SessionCommandOutcome.Failed)
                Debug.LogWarning($"세션 명령 오류: {SessionErrorMessages.Describe(result.Code)} ({result.Code})");
        }
        private void OnStateChanged(SessionInfo state)
        {
            if (!previous.IsJoined && state.IsJoined)
                Debug.Log($"입장 성공: session={state.SessionId}, player={state.PlayerId}, role={state.Role}");
            if (state.Phase.HasValue)
                Debug.Log($"세션 상태 반영: phase={state.Phase}, participants={state.ParticipantCount}, sceneLoaded={state.IsSceneLoaded}");
            if (previous.Phase != state.Phase)
                Debug.Log($"세션 단계 변경: {previous.Phase?.ToString() ?? "None"} -> {state.Phase}");
            previous = state;
        }
    }
}
