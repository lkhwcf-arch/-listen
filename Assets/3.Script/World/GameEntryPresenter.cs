using System;
using System.Threading;
using System.Threading.Tasks;
using EscapeGame.Configuration;
using EscapeGame.Session.Contracts;
using EscapeGame.Session.Presentation;

namespace EscapeGame.World
{
    public interface IGameEntryView : ISessionConnectionView { event Action<string> LocalRequested; }

    public sealed class GameEntryPresenter : IDisposable
    {
        private readonly GameEntryService service;
        private readonly IGameEntryView view;
        private readonly CancellationToken token;
        private readonly ConfiguredServerConnection connection;
        private bool showWaitingRoom, disposed;
        public GameEntryPresenter(GameEntryService service, IGameEntryView view, ClientConnectionSettings settings, CancellationToken token)
        {
            this.service = service; this.view = view; this.token = token;
            connection = new ConfiguredServerConnection(settings, (address, ct) => service.EnterAsync(false, address, null, ct));
            view.JoinRequested += Join;
            view.LocalRequested += Local;
            service.Changed += Render;
            Render();
        }
        // 자동 실행은 기존 테스트/호환 화면용입니다. JoinMenuScreen은 버튼만 연결합니다.
        public Task StartAsync() => disposed ? Task.CompletedTask : connection.StartAsync(token);
        public Task JoinAsync() => disposed ? Task.CompletedTask : connection.ConnectAsync(token);
        private async void Join()
        { try { await JoinAsync(); } catch (OperationCanceledException) { } }
        private async void Local(string role)
        { try { await service.EnterAsync(true, null, role, token); } catch (OperationCanceledException) { } }
        private void Render()
        {
            if (disposed || token.IsCancellationRequested) return;
            if (!service.IsBusy) showWaitingRoom = false;
            else if (service.IsWaitingForParticipants) showWaitingRoom = true;
            // 먼저 입장한 사용자는 씬 전환 완료까지 같은 대기 UI를 유지합니다.
            // 두 번째 사용자는 이 값이 한 번도 true가 되지 않습니다.
            view.Render(new SessionConnectionPresentation(service.IsBusy ? "로딩 중" : "입장 선택",
                service.Role ?? "-", service.StartGate.ParticipantCount.ToString(), service.Message,
                !service.IsBusy, false, showWaitingRoom: showWaitingRoom));
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            view.JoinRequested -= Join; view.LocalRequested -= Local; service.Changed -= Render;
        }
    }
}
