using System;
using System.Threading;
using System.Threading.Tasks;
using EscapeGame.Session.Contracts;

namespace EscapeGame.Session.Presentation
{
    public sealed class SessionConnectionPresenter : IDisposable
    {
        private readonly ISessionConnectionService service;
        private readonly ISessionConnectionView view;
        private readonly CancellationToken token;
        private bool joining;
        private bool disposed;
        private SessionFailureCode failure;
        public event Action<SessionFailureCode> ErrorObserved;

        public SessionConnectionPresenter(ISessionConnectionService service, ISessionConnectionView view, CancellationToken token)
        {
            this.service = service ?? throw new ArgumentNullException(nameof(service));
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.token = token;
            view.JoinRequested += OnJoinRequested;
            service.StateChanged += OnStateChanged;
            service.BackgroundFailed += OnBackgroundFailed;
            Render();
        }
        private async void OnJoinRequested() => await JoinAsync();
        public async Task JoinAsync()
        {
            if (disposed || token.IsCancellationRequested || joining || service.State.IsJoined || service.IsBusy) return;
            joining = true;
            failure = SessionFailureCode.None;
            Render();
            var result = await service.JoinAsync(view.ServerAddress?.Trim(), token);
            joining = false;
            if (disposed || token.IsCancellationRequested) return;
            if (result.Outcome == SessionCommandOutcome.Failed)
            {
                failure = result.Code;
                ErrorObserved?.Invoke(result.Code);
            }
            Render();
        }
        private void OnStateChanged(SessionInfo state)
        {
            failure = SessionFailureCode.None;
            Render();
        }
        private void OnBackgroundFailed(SessionFailureCode code)
        {
            failure = code;
            ErrorObserved?.Invoke(code);
            Render();
        }
        private void Render()
        {
            if (disposed || token.IsCancellationRequested) return;
            var state = service.State;
            string status = joining ? "접속 중" : !state.IsJoined ? "접속 전" :
                failure != SessionFailureCode.None ? "연결 확인 필요" : state.Phase switch
                {
                    ClientSessionPhase.Waiting => "상대방 입장 대기",
                    ClientSessionPhase.Loading => "플레이 준비 중",
                    ClientSessionPhase.Playing => "플레이 중",
                    _ => "입장 완료"
                };
            string message = failure != SessionFailureCode.None ? SessionErrorMessages.Describe(failure) :
                joining ? "서버에 입장 요청을 보내고 있습니다." :
                !state.IsJoined ? "서버 주소를 입력하고 입장 버튼을 눌러 주세요." :
                !state.IsSceneLoaded ? "씬과 필수 객체의 준비가 끝나면 자동으로 보고합니다." :
                state.Phase == ClientSessionPhase.Playing ? "두 참가자의 준비가 완료되었습니다." :
                "내 준비는 완료되었습니다. 상대방을 기다립니다.";
            bool editable = !joining && !state.IsJoined;
            view.Render(new SessionConnectionPresentation(status, state.Role ?? "-",
                state.Phase.HasValue ? state.ParticipantCount.ToString() : "-", message, editable && !service.IsBusy, editable));
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            view.JoinRequested -= OnJoinRequested;
            service.StateChanged -= OnStateChanged;
            service.BackgroundFailed -= OnBackgroundFailed;
        }
    }
}
