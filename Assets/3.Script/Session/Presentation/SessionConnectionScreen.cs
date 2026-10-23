using EscapeGame.Session.Contracts;
using EscapeGame.Configuration;
using System;
using System.Threading;
using UnityEngine;

namespace EscapeGame.Session.Presentation
{
    [RequireComponent(typeof(SessionConnectionView))]
    public sealed class SessionConnectionScreen : MonoBehaviour
    {
        [SerializeField] private SessionBootstrapper bootstrapper;
        [SerializeField] private SessionConnectionView view;
        private SessionConnectionPresenter presenter;
        private EscapeGame.World.GameEntryPresenter entryPresenter;
        private CancellationTokenSource lifetime;

        private async void OnEnable()
        {
            if (view == null)
                view = GetComponent<SessionConnectionView>();
            if (bootstrapper == null || view == null)
                throw new InvalidOperationException("접속 화면에 Bootstrapper와 View를 연결하세요.");
            view.ValidateConfiguration();
            lifetime = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken, bootstrapper.destroyCancellationToken);
            var activation = lifetime;
            try
            {
                var settings = UnityClientConnectionConfig.Load();
                var flow = bootstrapper.GetComponent<EscapeGame.World.GameFlowBootstrapper>();
                if (flow != null)
                {
                    entryPresenter = new EscapeGame.World.GameEntryPresenter(flow.Service, view, settings, activation.Token);
                    await entryPresenter.StartAsync();
                }
                else
                {
                    presenter = new SessionConnectionPresenter(bootstrapper.Service, view, settings, activation.Token);
                    presenter.ErrorObserved += OnErrorObserved;
                    await presenter.StartAsync();
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception error)
            {
                if (ReferenceEquals(lifetime, activation) && !activation.IsCancellationRequested)
                {
                    Debug.LogError(error.Message);
                    view.ShowConfigurationError(error.Message);
                }
            }
        }
        private void OnDisable()
        {
            lifetime?.Cancel();
            entryPresenter?.Dispose();
            entryPresenter = null;
            if (presenter != null)
            {
                presenter.ErrorObserved -= OnErrorObserved;
                presenter.Dispose();
            }
            lifetime?.Dispose();
            lifetime = null;
            presenter = null;
        }
        private void OnErrorObserved(SessionFailureCode code) => Debug.LogWarning($"접속 화면 요청 오류: {code}");
    }
}
