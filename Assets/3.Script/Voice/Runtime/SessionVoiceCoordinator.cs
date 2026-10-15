using System;
using System.Threading;
using System.Threading.Tasks;
using EscapeGame.Session.Contracts;
using EscapeGame.Voice.Contracts;
namespace EscapeGame.Voice
{
    // Observer + 직렬 조정기: 게임 상태를 구독하되 게임 내부 상태/토큰/HTTP를 알지 못합니다.
    internal sealed class SessionVoiceCoordinator
    {
        private readonly ISessionConnectionService session;
        private readonly IVoiceChatService voice;
        private VoiceConnectionRequest desired;
        private int version;
        private bool pumping;
        private bool stopped;
        public Task WhenSettled { get; private set; } = Task.CompletedTask;
        public SessionVoiceCoordinator(ISessionConnectionService session, IVoiceChatService voice)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.voice = voice ?? throw new ArgumentNullException(nameof(voice));
            session.StateChanged += OnSessionChanged;
            OnSessionChanged(session.State);
        }
        private void OnSessionChanged(SessionInfo info)
        {
            if (stopped) return;
            var next = info.IsJoined ? new VoiceConnectionRequest(info.SessionId, info.PlayerId, "Player " + info.Role) : null;
            if (desired?.SessionId == next?.SessionId && desired?.PlayerId == next?.PlayerId) return;
            desired = next;
            version++;
            voice.StopTransmission();
            StartPump();
        }
        private void StartPump()
        {
            if (!pumping) WhenSettled = PumpAsync();
        }
        private async Task PumpAsync()
        {
            pumping = true;
            try
            {
                int handled;
                do
                {
                    handled = version;
                    var request = desired;
                    if (request == null) await voice.DisconnectAsync(CancellationToken.None);
                    else await voice.ConnectAsync(request, CancellationToken.None);
                } while (handled != version);
            }
            finally { pumping = false; }
        }
        public Task StopAsync()
        {
            if (!stopped)
            {
                stopped = true;
                session.StateChanged -= OnSessionChanged;
                desired = null;
                version++;
                voice.StopTransmission();
                StartPump();
            }
            return WhenSettled;
        }
    }
}
