using System;
using System.Threading;
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
        public GameEntryPresenter(GameEntryService service, IGameEntryView view, CancellationToken token)
        {
            this.service = service; this.view = view; this.token = token;
            view.JoinRequested += Join;
            view.LocalRequested += Local;
            service.Changed += Render;
            Render();
        }
        private async void Join() => await service.EnterAsync(false, view.ServerAddress?.Trim(), null, token);
        private async void Local(string role) => await service.EnterAsync(true, null, role, token);
        private void Render() => view.Render(new SessionConnectionPresentation(service.IsBusy ? "로딩 중" : "입장 선택",
            service.Role ?? "-", "-", service.Message, !service.IsBusy, !service.IsBusy));
        public void Dispose()
        {
            view.JoinRequested -= Join; view.LocalRequested -= Local; service.Changed -= Render;
        }
    }
}
