using System;
using System.Threading;
using System.Threading.Tasks;
using EscapeGame.Session.Contracts;
using EscapeGame.Configuration;

namespace EscapeGame.Session.Presentation
{
    public sealed class SessionConnectionPresenter : IDisposable
    {
        private readonly ISessionConnectionService service;
        private readonly ISessionConnectionView view;
        private readonly CancellationToken token;
        private readonly ConfiguredServerConnection connection;
        private bool joining, leaving, disposed;
        private SessionFailureCode failure;
        public event Action<SessionFailureCode> ErrorObserved;

        public SessionConnectionPresenter(ISessionConnectionService service, ISessionConnectionView view, ClientConnectionSettings settings, CancellationToken token)
        {
            this.service = service ?? throw new ArgumentNullException(nameof(service));
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.token = token;
            connection = new ConfiguredServerConnection(settings, JoinCoreAsync);
            view.JoinRequested += OnJoinRequested;
            view.LeaveRequested += OnLeaveRequested;
            service.StateChanged += OnStateChanged;
            service.BackgroundFailed += OnBackgroundFailed;
            service.Disconnected += OnDisconnected;
            Render();
        }
        private async void OnJoinRequested() => await JoinAsync();
        private async void OnLeaveRequested() => await LeaveAsync();
        public Task StartAsync() => connection.StartAsync(token);
        public Task JoinAsync() => connection.ConnectAsync(token);
        private async Task JoinCoreAsync(string address, CancellationToken connectionToken)
        {
            if (disposed || token.IsCancellationRequested || joining || leaving || service.State.IsJoined || service.IsBusy) return;
            joining = true; failure = SessionFailureCode.None; Render();
            var result = await service.JoinAsync(address, connectionToken);
            joining = false;
            if (disposed || token.IsCancellationRequested) return;
            ObserveResult(result); Render();
        }
        public async Task LeaveAsync()
        {
            if (disposed || token.IsCancellationRequested || joining || leaving || !service.State.IsJoined) return;
            leaving = true; failure = SessionFailureCode.None; Render();
            var result = await service.LeaveAsync(token);
            leaving = false;
            if (disposed || token.IsCancellationRequested) return;
            ObserveResult(result); Render();
        }
        private void ObserveResult(SessionCommandResult result)
        {
            if (result.Outcome != SessionCommandOutcome.Failed) return;
            failure = result.Code;
            ErrorObserved?.Invoke(result.Code);
        }
        private void OnStateChanged(SessionInfo state) { failure = SessionFailureCode.None; Render(); }
        private void OnDisconnected(SessionFailureCode code)
        {
            if (disposed || token.IsCancellationRequested) return;
            failure = code;
            view.ShowDisconnected(SessionErrorMessages.Describe(code));
            ErrorObserved?.Invoke(code); Render();
        }
        private void OnBackgroundFailed(SessionFailureCode code)
        { failure = code; ErrorObserved?.Invoke(code); Render(); }
        private void Render()
        {
            if (disposed || token.IsCancellationRequested) return;
            var state = service.State;
            bool exiting = leaving || state.ConnectionStatus == SessionConnectionStatus.Leaving;
            bool disconnected = state.ConnectionStatus == SessionConnectionStatus.Disconnected;
            string status = exiting ? "퇴장 중" : joining ? "접속 중" : disconnected ? "연결 종료" : !state.IsJoined ? "접속 전" :
                failure != SessionFailureCode.None ? "연결 확인 필요" : state.Phase switch
                {
                    ClientSessionPhase.Waiting => "상대방 입장 대기",
                    ClientSessionPhase.Loading => "플레이 준비 중",
                    ClientSessionPhase.Playing => "플레이 중",
                    _ => "입장 완료"
                };
            string message = exiting ? "서버에 퇴장을 알리고 연결을 정리하고 있습니다." :
                failure != SessionFailureCode.None ? SessionErrorMessages.Describe(failure) :
                disconnected && !joining ? SessionErrorMessages.Describe(state.DisconnectReason) :
                joining ? "서버에 입장 요청을 보내고 있습니다." :
                !state.IsJoined ? "설정된 서버로 연결을 재시도하려면 입장 버튼을 눌러 주세요." :
                !state.IsSceneLoaded ? "씬과 필수 객체의 준비가 끝나면 자동으로 보고합니다." :
                state.Phase == ClientSessionPhase.Playing ? "두 참가자의 준비가 완료되었습니다." :
                "내 준비는 완료되었습니다. 상대방을 기다립니다.";
            bool editable = !joining && !exiting && !state.IsJoined;
            view.Render(new SessionConnectionPresentation(status, state.Role ?? "-",
                state.Phase.HasValue ? state.ParticipantCount.ToString() : "-", message,
                editable && !service.IsBusy, false, state.IsJoined && !joining && !exiting, exiting));
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            view.JoinRequested -= OnJoinRequested;
            view.LeaveRequested -= OnLeaveRequested;
            service.StateChanged -= OnStateChanged;
            service.BackgroundFailed -= OnBackgroundFailed;
            service.Disconnected -= OnDisconnected;
        }
    }
}
