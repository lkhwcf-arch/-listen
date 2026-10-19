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
        public GameEntryPresenter(GameEntryService service, IGameEntryView view, ClientConnectionSettings settings, CancellationToken token)
        {
            this.service = service; this.view = view; this.token = token;
            connection = new ConfiguredServerConnection(settings, (address, ct) => service.EnterAsync(false, address, null, ct));
            view.JoinRequested += Join;
            view.LocalRequested += Local;
            service.Changed += Render;
            Render();
        }
        public Task StartAsync() => connection.StartAsync(token);
        private async void Join() => await connection.ConnectAsync(token);
        private async void Local(string role) => await service.EnterAsync(true, null, role, token);
        private void Render() => view.Render(new SessionConnectionPresentation(service.IsBusy ? "로딩 중" : "입장 선택",
            service.Role ?? "-", "-", service.Message, !service.IsBusy, false));
        public void Dispose()
        {
            view.JoinRequested -= Join; view.LocalRequested -= Local; service.Changed -= Render;
        }
    }
}
