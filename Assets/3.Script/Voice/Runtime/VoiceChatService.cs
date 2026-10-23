using System;
using System.Threading;
using System.Threading.Tasks;
using EscapeGame.Voice.Contracts;
namespace EscapeGame.Voice
{
    // SDK와 화면 사이의 Facade입니다. 연결/정리는 하나의 비동기 실행 경로만 사용합니다.
    internal sealed class VoiceChatService : IVoiceChatService
    {
        private readonly IVoiceGateway gateway;
        private readonly Action<Exception> diagnostic;
        private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);
        private CancellationTokenSource connecting;
        private int generation;
        private bool shuttingDown;
        private VoiceFailureCode connectionFault;
        public VoiceInfo State
        {
            get;
            private set;
        } = new VoiceInfo(VoiceConnectionStatus.Idle);
        public event Action<VoiceInfo> StateChanged;
        public VoiceChatService(IVoiceGateway gateway, Action<Exception> diagnostic = null)
        {
            this.gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
            this.diagnostic = diagnostic;
            gateway.ParticipantsChanged += OnParticipantsChanged;
            gateway.Faulted += OnFaulted;
            gateway.RecoveryChanged += OnRecoveryChanged;
        }
        public async Task ConnectAsync(VoiceConnectionRequest request, CancellationToken token)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            await gate.WaitAsync(token);
            try
            {
                token.ThrowIfCancellationRequested();
                if (shuttingDown)
                    return;
                if (State.Status == VoiceConnectionStatus.Connected && State.SessionId == request.SessionId && State.PlayerId == request.PlayerId)
                    return;
                generation++;
                connectionFault = VoiceFailureCode.None;
                if (!await CleanupAsync())
                {
                    Publish(VoiceConnectionStatus.Failed, VoiceFailureCode.CleanupFailed);
                    return;
                }
                using var attempt = CancellationTokenSource.CreateLinkedTokenSource(token);
                connecting = attempt;
                string channel = VoiceChannelPolicy.ForSession(request.SessionId);
                Publish(new VoiceInfo(VoiceConnectionStatus.Connecting, VoiceFailureCode.None, request.SessionId, request.PlayerId, channel));
                try
                {
                    // 취소 불가능한 SDK 작업도 완료를 기다린 후 보상 정리합니다.
                    await gateway.ConnectAsync(request, channel, attempt.Token);
                    attempt.Token.ThrowIfCancellationRequested();
                    Publish(VoiceConnectionStatus.Connected);
                }
                catch (Exception exception)
                {
                    bool cancelled = attempt.IsCancellationRequested || exception is OperationCanceledException;
                    var code = exception is VoiceGatewayException failure ? failure.Code : VoiceFailureCode.UnexpectedFailure;
                    if (!cancelled)
                        diagnostic?.Invoke(exception);
                    bool cleaned = await CleanupAsync();
                    if (!cleaned)
                        Publish(VoiceConnectionStatus.Failed, VoiceFailureCode.CleanupFailed);
                    else if (connectionFault != VoiceFailureCode.None)
                        Publish(VoiceConnectionStatus.Failed, connectionFault);
                    else if (cancelled)
                        Publish(new VoiceInfo(VoiceConnectionStatus.Idle));
                    else
                        Publish(VoiceConnectionStatus.Failed, code);
                }
                finally
                {
                    connecting = null;
                }
            }
            finally
            {
                gate.Release();
            }
        }
        public void StopTransmission()
        {
            connecting?.Cancel();
            try
            {
                gateway.Mute();
            }
            catch (Exception exception)
            {
                diagnostic?.Invoke(exception);
            }
        }
        public async Task DisconnectAsync(CancellationToken token)
        {
            StopTransmission();
            // 한번 시작한 정리는 호출자 취소와 무관하게 끝냅니다. 늦은 SDK 완료가 다음 접속에 섞이지 않습니다.
            await gate.WaitAsync();
            try
            {
                generation++;
                if (State.Status != VoiceConnectionStatus.Idle)
                    Publish(VoiceConnectionStatus.Disconnecting);
                bool cleaned = await CleanupAsync();
                Publish(new VoiceInfo(cleaned ? VoiceConnectionStatus.Idle : VoiceConnectionStatus.Failed,
                    cleaned ? VoiceFailureCode.None : VoiceFailureCode.CleanupFailed));
            }
            finally
            {
                gate.Release();
            }
        }
        public async Task ShutdownAsync()
        {
            shuttingDown = true;
            gateway.ParticipantsChanged -= OnParticipantsChanged;
            gateway.Faulted -= OnFaulted;
            gateway.RecoveryChanged -= OnRecoveryChanged;
            await DisconnectAsync(CancellationToken.None);
        }
        private async Task<bool> CleanupAsync()
        {
            try
            {
                gateway.Mute();
                await gateway.DisconnectAsync();
                return true;
            }
            catch (Exception exception)
            {
                diagnostic?.Invoke(exception);
                return false;
            }
        }
        private void OnParticipantsChanged()
        {
            if (State.Status == VoiceConnectionStatus.Connected || State.Status == VoiceConnectionStatus.Recovering)
                Publish(State.Status);
        }
        private void OnRecoveryChanged(bool recovering)
        {
            if (State.Status == VoiceConnectionStatus.Connected || State.Status == VoiceConnectionStatus.Recovering)
                Publish(recovering ? VoiceConnectionStatus.Recovering : VoiceConnectionStatus.Connected);
        }
        private void OnFaulted(VoiceFailureCode code)
        {
            connectionFault = code;
            StopTransmission();
            Publish(VoiceConnectionStatus.Failed, code);
            _ = CleanupFaultAsync(generation);
        }
        private async Task CleanupFaultAsync(int failedGeneration)
        {
            await gate.WaitAsync();
            try
            {
                if (failedGeneration == generation && !await CleanupAsync())
                    Publish(VoiceConnectionStatus.Failed, VoiceFailureCode.CleanupFailed);
            }
            finally
            {
                gate.Release();
            }
        }
        private void Publish(VoiceConnectionStatus status, VoiceFailureCode code = VoiceFailureCode.None)
            => Publish(new VoiceInfo(status, code, State.SessionId, State.PlayerId, State.ChannelName, gateway.ParticipantCount));
        private void Publish(VoiceInfo state)
        {
            State = state;
            StateChanged?.Invoke(state);
        }
    }
}
