using System;
using EscapeGame.Voice.Contracts;
namespace EscapeGame.Voice.Presentation
{
    public sealed class VoiceStatusPresenter : IDisposable
    {
        private readonly IVoiceChatService service;
        private readonly IVoiceStatusView view;
        public VoiceStatusPresenter(IVoiceChatService service, IVoiceStatusView view)
        {
            this.service = service;
            this.view = view;
            service.StateChanged += OnChanged;
            OnChanged(service.State);
        }
        private void OnChanged(VoiceInfo info)
        {
            string status = info.Status switch
            {
                VoiceConnectionStatus.Connecting => "연결 중",
                VoiceConnectionStatus.Connected => "연결됨 (" + info.ParticipantCount + "명)",
                VoiceConnectionStatus.Recovering => "연결 복구 중",
                VoiceConnectionStatus.Disconnecting => "종료 중",
                VoiceConnectionStatus.Failed => Describe(info.FailureCode),
                _ => "접속 대기"
            };
            view.Render("음성: " + status);
        }
        private static string Describe(VoiceFailureCode code) => code switch
        {
            VoiceFailureCode.InUse => "다른 음성 기능이 사용 중입니다.",
            VoiceFailureCode.InitializationFailed => "초기화 실패 (Unity Services 설정 확인)",
            VoiceFailureCode.AuthenticationFailed => "인증 실패",
            VoiceFailureCode.LoginFailed => "로그인 실패",
            VoiceFailureCode.JoinFailed => "음성방 입장 실패",
            VoiceFailureCode.ConnectionLost => "연결이 종료되었습니다.",
            VoiceFailureCode.CleanupFailed => "연결 정리를 완료하지 못했습니다.",
            _ => "연결 실패"
        };
        public void Dispose() => service.StateChanged -= OnChanged;
    }
}
