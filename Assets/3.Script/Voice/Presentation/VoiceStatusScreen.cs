using TMPro;
using UnityEngine;
namespace EscapeGame.Voice.Presentation
{
    [DisallowMultipleComponent]
    public sealed class VoiceStatusScreen : MonoBehaviour, IVoiceStatusView
    {
        [SerializeField] private VoiceBootstrapper bootstrapper;
        [SerializeField] private TMP_Text statusText;
        private VoiceStatusPresenter presenter;
        private void OnEnable()
        {
            if (bootstrapper == null || statusText == null) { Debug.LogError("음성 Bootstrapper와 상태 텍스트를 연결하세요."); return; }
            presenter = new VoiceStatusPresenter(bootstrapper.Service, this);
        }
        public void Render(string status) { if (statusText != null) statusText.text = status; }
        private void OnDisable() { presenter?.Dispose(); presenter = null; }
    }
}
