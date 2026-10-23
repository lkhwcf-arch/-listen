using System;

namespace EscapeGame.Session.Contracts
{
    public enum PuzzleSubmissionOutcome
    {
        Rejected = 0, Incorrect = 1, Correct = 2
    }

    // 전송 형식과 독립적인 읽기 전용 판정 값입니다. 통신 실패/취소는 별도 경로로 전달됩니다.
    public sealed class PuzzleSubmissionResult
    {
        public PuzzleSubmissionOutcome Outcome
        {
            get;
        }
        public bool Accepted => Outcome != PuzzleSubmissionOutcome.Rejected;
        public bool Correct => Outcome == PuzzleSubmissionOutcome.Correct;
        private PuzzleSubmissionResult(PuzzleSubmissionOutcome outcome) => Outcome = outcome;
        public static PuzzleSubmissionResult FromDecision(bool accepted, bool correct)
        {
            if (!accepted && correct)
                throw new ArgumentException("거절된 제출은 정답일 수 없습니다.");
            return new PuzzleSubmissionResult(!accepted ? PuzzleSubmissionOutcome.Rejected :
                correct ? PuzzleSubmissionOutcome.Correct : PuzzleSubmissionOutcome.Incorrect);
        }
    }
}
