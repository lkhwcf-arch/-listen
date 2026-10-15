using EscapeGame.Session.Contracts;

namespace EscapeGame.Session.Presentation
{
    // 문구는 표시 계층의 책임이며, 통신 예외나 Wire 코드를 참조하지 않습니다.
    public static class SessionErrorMessages
    {
        public static string Describe(SessionFailureCode code) => code switch
        {
            SessionFailureCode.LeaveUnconfirmed => "내 연결은 종료했습니다. 서버에서 퇴장 확인을 받지 못한 자리는 생존 확인 만료 후 정리됩니다.",
            SessionFailureCode.ConnectionTimedOut => "서버와 연결이 끊겼습니다. 제한 시간 동안 생존 확인 응답을 받지 못했습니다. 다시 입장해 주세요.",
            SessionFailureCode.ConnectionFailed => "서버에 연결할 수 없습니다. 서버 주소와 실행 상태를 확인해 주세요.",
            SessionFailureCode.SessionFull => "참가 인원이 가득 찼습니다. 서버의 참가 상태를 확인해 주세요.",
            SessionFailureCode.SessionNotFound => "참가한 세션이 없습니다. 접속 상태를 확인해 주세요.",
            SessionFailureCode.InvalidParticipant => "참가 정보가 만료되었습니다. 새로 입장해야 합니다.",
            SessionFailureCode.ParticipantTokenRequired => "참가 정보가 없어 요청할 수 없습니다.",
            SessionFailureCode.InvalidInput => "서버 주소와 연결 설정을 확인해 주세요.",
            SessionFailureCode.InvalidState => "현재 연결 상태에서 요청을 완료할 수 없습니다.",
            SessionFailureCode.Busy => "다른 요청을 처리 중입니다. 잠시 후 다시 시도해 주세요.",
            SessionFailureCode.InvalidResponse => "서버 응답을 처리할 수 없습니다. 서버와 클라이언트 버전을 확인해 주세요.",
            _ => "요청 처리 중 오류가 발생했습니다. 연결 상태를 확인해 주세요."
        };
    }
}
