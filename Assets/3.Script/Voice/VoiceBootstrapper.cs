using System.Threading.Tasks;
using EscapeGame.Session;
using EscapeGame.Voice.Contracts;
using UnityEngine;
namespace EscapeGame.Voice
{
    // 실제 로컬 플레이어의 Bootstrapper 한 곳에만 붙입니다. 같은 Editor의 보조 테스트 객체에는 붙이지 않습니다.
    [DisallowMultipleComponent]
    public sealed class VoiceBootstrapper : MonoBehaviour
    {
        [SerializeField] private SessionBootstrapper sessionBootstrapper;
        private VoiceChatService service;
        private SessionVoiceCoordinator coordinator;
        private Task stopping = Task.CompletedTask;
        private int activation;
        public IVoiceChatService Service
        {
            get
            {
                if (service == null) service = new VoiceChatService(new VivoxVoiceGateway(),
                    exception => Debug.LogWarning("음성 내부 진단: " + exception.GetType().Name + ": " + exception.Message));
                return service;
            }
        }
        private async void OnEnable()
        {
            int current = ++activation;
            try
            {
                await stopping;
                if (this == null || !isActiveAndEnabled || current != activation) return;
                if (sessionBootstrapper == null) sessionBootstrapper = GetComponent<SessionBootstrapper>();
                if (sessionBootstrapper == null) { Debug.LogError("VoiceBootstrapper에 SessionBootstrapper를 연결하세요."); return; }
                coordinator = new SessionVoiceCoordinator(sessionBootstrapper.Service, Service);
            }
            catch (System.Exception exception) { Debug.LogWarning("음성 시작 실패: " + exception.GetType().Name); }
        }
        private void OnDisable()
        {
            activation++;
            service?.StopTransmission();
            if (coordinator != null) { stopping = ObserveStopAsync(coordinator); coordinator = null; }
        }
        private async Task ObserveStopAsync(SessionVoiceCoordinator current)
        {
            try { await current.StopAsync(); }
            catch (System.Exception exception) { Debug.LogWarning("음성 정리 실패: " + exception.GetType().Name); }
        }
        private async void OnDestroy()
        {
            try { await stopping; if (service != null) await service.ShutdownAsync(); }
            catch (System.Exception exception) { Debug.LogWarning("음성 종료 실패: " + exception.GetType().Name); }
        }
    }
}
