using System;
using System.Threading;
using System.Threading.Tasks;
using EscapeGame.Session.Contracts;

namespace EscapeGame.World
{
    // 게임 입력이 사용하는 공개 계약입니다. HTTP·생성 메시지·내부 통신 예외를 노출하지 않습니다.
    public interface IRoomAnswerService
    {
        Task<RoomAnswerResult> SubmitAnswerAsync(int roomNo, string[] answers, CancellationToken token);
    }
    public sealed class RoomAnswerResult
    {
        public bool IsSuccess => Failure == SessionFailureCode.None;
        public bool Correct
        {
            get;
        }
        public SessionFailureCode Failure
        {
            get;
        }
        private RoomAnswerResult(bool correct, SessionFailureCode failure)
        {
            Correct = correct;
            Failure = failure;
        }
        public static RoomAnswerResult Decision(bool correct) => new RoomAnswerResult(correct, SessionFailureCode.None);
        public static RoomAnswerResult Failed(SessionFailureCode failure)
        {
            if (failure == SessionFailureCode.None)
                throw new ArgumentOutOfRangeException(nameof(failure));
            return new RoomAnswerResult(false, failure);
        }
    }
}
