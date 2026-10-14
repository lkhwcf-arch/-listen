using EscapeGame.Session.Contracts;
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
        private CancellationTokenSource lifetime;

        private void OnEnable()
        {
            if (view == null)
                view = GetComponent<SessionConnectionView>();

            if (bootstrapper == null || view == null)
                throw new InvalidOperationException("접속 화면에 Bootstrapper와 View를 연결하세요.");

            view.ValidateConfiguration();
            lifetime = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken, bootstrapper.destroyCancellationToken);
            presenter = new SessionConnectionPresenter(bootstrapper.Service, view, lifetime.Token);
            presenter.ErrorObserved += OnErrorObserved;
        }
        private void OnDisable()
        {
            lifetime?.Cancel();
            if (presenter != null)
            {
                presenter.ErrorObserved -= OnErrorObserved;
                presenter.Dispose();
            }
            lifetime?.Dispose();
            lifetime = null;
            presenter = null;
        }
        private void OnErrorObserved(SessionFailureCode code) =>
            Debug.LogWarning($"접속 화면 요청 오류: {code}");
    }
}
