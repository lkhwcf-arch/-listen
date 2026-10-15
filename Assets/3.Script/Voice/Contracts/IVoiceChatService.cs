using System;
using System.Threading;
using System.Threading.Tasks;
namespace EscapeGame.Voice.Contracts
{
    public interface IVoiceChatService
    {
        VoiceInfo State { get; }
        event Action<VoiceInfo> StateChanged;
        // 완료된 작업의 성공 여부는 State에서 확인합니다. SDK 예외는 공개 경계를 넘지 않습니다.
        Task ConnectAsync(VoiceConnectionRequest request, CancellationToken token);
        Task DisconnectAsync(CancellationToken token);
        // 네이티브 비동기 작업의 종료를 기다리기 전에 송수신을 즉시 차단합니다.
        void StopTransmission();
    }
}
