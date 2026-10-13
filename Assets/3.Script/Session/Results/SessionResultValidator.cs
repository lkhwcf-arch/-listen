using System;
using EscapeGame.Networking;

namespace EscapeGame.Session
{
    public static class SessionResultValidator
    {
        public static void ValidateJoin(long status, JoinSessionResponse response)
        {
            if (status != 201 && status != 409)
                throw new InvalidOperationException($"예상하지 못한 HTTP 응답: {status}");
            if (response == null)
                throw new InvalidOperationException("입장 응답이 없습니다.");
            if (status == 201 && !response.success)
                throw new InvalidOperationException("입장 응답이 일치하지 않습니다.");
            if (status == 409 &&
                (response.success || response.code != SessionResultCodes.SessionFull))
                throw new InvalidOperationException("입장 실패 응답이 일치하지 않습니다.");
        }

        public static void ValidateSceneLoaded(long status, ReadySessionResponse response)
        {
            if (response == null)
                throw new InvalidOperationException("로딩 완료 응답이 없습니다.");
            ValidateSnapshotResponse(status, response.success, response.snapshot, response.code);
        }

        public static void ValidateQuery(long status, QuerySessionResponse response)
        {
            if (response == null)
                throw new InvalidOperationException("세션 조회 응답이 없습니다.");
            ValidateSnapshotResponse(status, response.success, response.snapshot, response.code);
        }

        // 두 작업은 같은 HTTP 성공/실패 계약을 사용하지만 공개 진입점은 구분합니다.
        private static void ValidateSnapshotResponse(
            long status, bool success, SessionSnapshotDto snapshot, string code)
        {
            if (status != 200 && status != 400 && status != 403 && status != 404)
                throw new InvalidOperationException($"예상하지 못한 HTTP 응답: {status}");
            if (status == 200)
            {
                if (!success || snapshot == null)
                    throw new InvalidOperationException("스냅샷 성공 응답이 일치하지 않습니다.");
                return;
            }
            string expectedCode = status switch
            {
                400 => SessionResultCodes.ParticipantTokenRequired,
                403 => SessionResultCodes.InvalidParticipant,
                404 => SessionResultCodes.SessionNotFound,
                _ => throw new InvalidOperationException()
            };
            if (success || code != expectedCode)
                throw new InvalidOperationException("스냅샷 실패 응답이 일치하지 않습니다.");
        }
    }
}
