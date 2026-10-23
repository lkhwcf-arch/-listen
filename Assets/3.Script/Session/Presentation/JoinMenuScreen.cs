using System;
using System.Threading;
using EscapeGame.Configuration;
using EscapeGame.World;
using UnityEngine;

namespace EscapeGame.Session.Presentation
{
    // 주소 선택과 화면 수명만 조립합니다. 실제 입장은 탈출하기 명령으로 시작합니다.
    [RequireComponent(typeof(JoinMenuView))]
    public sealed class JoinMenuScreen : MonoBehaviour
    {
        [SerializeField] private SessionBootstrapper bootstrapper;
        private GameEntryPresenter presenter;
        private CancellationTokenSource lifetime;

        public void ValidateConfiguration()
        {
            if (bootstrapper == null || bootstrapper.GetComponent<GameFlowBootstrapper>() == null)
                throw new InvalidOperationException("Join 메뉴에 게임 세션과 GameFlowBootstrapper를 연결하세요.");
            GetComponent<JoinMenuView>().ValidateConfiguration();
        }
        private void OnEnable()
        {
            var view = GetComponent<JoinMenuView>();
            try
            {
                ValidateConfiguration();
                view.Initialize();
                lifetime = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken, bootstrapper.destroyCancellationToken);
                presenter = new GameEntryPresenter(bootstrapper.GetComponent<GameFlowBootstrapper>().Service,
                    view, UnityClientConnectionConfig.Load(), lifetime.Token);
                // autoConnect 설정과 무관하게 이 메뉴는 사용자 버튼 입력을 기다립니다.
            }
            catch (Exception error)
            {
                Debug.LogError("[시작 메뉴] " + error.Message);
                view.ShowConfigurationError(error.Message);
            }
        }
        private void OnDisable()
        {
            lifetime?.Cancel();
            presenter?.Dispose(); presenter = null;
            lifetime?.Dispose(); lifetime = null;
        }
    }
}
