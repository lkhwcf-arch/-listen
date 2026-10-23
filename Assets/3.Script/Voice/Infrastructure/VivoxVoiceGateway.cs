using System;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Vivox;
using EscapeGame.Voice.Contracts;
namespace EscapeGame.Voice
{
    // 이 파일만 Vivox SDK를 참조합니다. 프로세스 내 단일 SDK 로그인 소유권을 지킵니다.
    internal sealed class VivoxVoiceGateway : IVoiceGateway
    {
        private static VivoxVoiceGateway owner;
        private IVivoxService sdk;
        private string channel;
        private bool subscribed;
        private bool expectedDisconnect;
        private bool loginStarted;
        public event Action ParticipantsChanged;
        public event Action<VoiceFailureCode> Faulted;
        public event Action<bool> RecoveryChanged;
        public int ParticipantCount => sdk != null && channel != null && sdk.ActiveChannels.TryGetValue(channel, out var players) ? players.Count : 0;
        public async Task ConnectAsync(VoiceConnectionRequest request, string channelName, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (owner != null && owner != this)
                throw new VoiceGatewayException(VoiceFailureCode.InUse);
            owner = this;
            try
            {
                try
                {
                    if (UnityServices.State != ServicesInitializationState.Initialized)
                        await UnityServices.InitializeAsync();
                    token.ThrowIfCancellationRequested();
                    sdk = VivoxService.Instance;
                    if (sdk.InitializationState != VivoxInitializationState.Initialized)
                        await sdk.InitializeAsync();
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    throw new VoiceGatewayException(VoiceFailureCode.InitializationFailed, exception);
                }
                token.ThrowIfCancellationRequested();
                // 기존 샘플/다른 기능이 로그인한 SDK를 임의로 로그아웃시키지 않습니다.
                if (sdk.IsLoggedIn && !loginStarted)
                    throw new VoiceGatewayException(VoiceFailureCode.InUse);
                try
                {
                    if (!AuthenticationService.Instance.IsSignedIn)
                        await AuthenticationService.Instance.SignInAnonymouslyAsync();
                }
                catch (Exception exception)
                {
                    throw new VoiceGatewayException(VoiceFailureCode.AuthenticationFailed, exception);
                }
                token.ThrowIfCancellationRequested();
                Subscribe();
                loginStarted = true;
                try
                {
                    await sdk.LoginAsync(new LoginOptions { DisplayName = request.DisplayName, PlayerId = request.PlayerId });
                }
                catch (Exception exception)
                {
                    throw new VoiceGatewayException(VoiceFailureCode.LoginFailed, exception);
                }
                Mute();
                token.ThrowIfCancellationRequested();
                channel = channelName;
                try
                {
                    await sdk.JoinGroupChannelAsync(channel, ChatCapability.AudioOnly);
                }
                catch (Exception exception)
                {
                    throw new VoiceGatewayException(VoiceFailureCode.JoinFailed, exception);
                }
                token.ThrowIfCancellationRequested();
                sdk.UnmuteOutputDevice();
                sdk.UnmuteInputDevice();
            }
            catch
            {
                Mute();
                throw;
            }
        }
        public void Mute()
        {
            if (owner != this || !loginStarted || sdk == null || !sdk.IsLoggedIn)
                return;
            sdk.MuteInputDevice();
            sdk.MuteOutputDevice();
        }
        public async Task DisconnectAsync()
        {
            if (owner != this)
                return;
            expectedDisconnect = true;
            try
            {
                Mute();
                if (sdk != null && loginStarted && sdk.IsLoggedIn)
                    await sdk.LogoutAsync();
                // 초기화만 수행했거나 기존 외부 로그인 때문에 실패한 경우에도 외부 로그인을 건드리지 않습니다.
                Unsubscribe();
                loginStarted = false;
                channel = null;
                owner = null;
            }
            finally
            {
                expectedDisconnect = false;
            }
        }
        private void Subscribe()
        {
            if (subscribed)
                return;
            subscribed = true;
            sdk.ParticipantAddedToChannel += OnParticipant;
            sdk.ParticipantRemovedFromChannel += OnParticipant;
            sdk.ConnectionRecovering += OnRecovering;
            sdk.ConnectionRecovered += OnRecovered;
            sdk.ConnectionFailedToRecover += OnRecoveryFailed;
            sdk.ChannelLeft += OnChannelLeft;
            sdk.LoggedOut += OnLoggedOut;
        }
        private void Unsubscribe()
        {
            if (!subscribed)
                return;
            subscribed = false;
            sdk.ParticipantAddedToChannel -= OnParticipant;
            sdk.ParticipantRemovedFromChannel -= OnParticipant;
            sdk.ConnectionRecovering -= OnRecovering;
            sdk.ConnectionRecovered -= OnRecovered;
            sdk.ConnectionFailedToRecover -= OnRecoveryFailed;
            sdk.ChannelLeft -= OnChannelLeft;
            sdk.LoggedOut -= OnLoggedOut;
        }
        private void OnParticipant(VivoxParticipant participant)
        {
            if (!expectedDisconnect)
                ParticipantsChanged?.Invoke();
        }
        private void OnRecovering()
        {
            if (!expectedDisconnect)
                RecoveryChanged?.Invoke(true);
        }
        private void OnRecovered()
        {
            if (!expectedDisconnect)
                RecoveryChanged?.Invoke(false);
        }
        private void OnRecoveryFailed()
        {
            if (!expectedDisconnect)
                Faulted?.Invoke(VoiceFailureCode.ConnectionLost);
        }
        private void OnChannelLeft(string name)
        {
            if (!expectedDisconnect && name == channel)
                Faulted?.Invoke(VoiceFailureCode.ConnectionLost);
        }
        private void OnLoggedOut()
        {
            if (!expectedDisconnect)
                Faulted?.Invoke(VoiceFailureCode.ConnectionLost);
        }
    }
}
