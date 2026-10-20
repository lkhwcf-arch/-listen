using EscapeGame.Session.Presentation;

namespace EscapeGame.Interaction
{
    // 표시 이름을 바꿔도 서버로 보내는 데이터 값은 바꾸지 않습니다.
    public static class AnswerInputMessages
    {
        public static string Describe(PuzzleAnswerController controller) => controller.Phase switch
        {
            AnswerInputPhase.Submitting => "서버에서 정답을 확인하고 있습니다.",
            AnswerInputPhase.Correct => "정답입니다. 문에서 진행 가능 여부를 확인해 주세요.",
            AnswerInputPhase.Incorrect => "오답입니다. 입력값을 다시 확인해 주세요.",
            AnswerInputPhase.Failed => SessionErrorMessages.Describe(controller.Failure),
            AnswerInputPhase.Cancelled => "요청이 취소되었습니다.",
            _ => controller.Plan.Kind == PuzzleInputKind.Dial ? "각 슬롯의 이전·다음 버튼으로 값을 고른 뒤 제출하세요." : "슬롯을 선택하고 아래 버튼을 순서대로 누르세요."
        };
        public static string Value(string value) => value switch
        {
            "Up" => "위", "Down" => "아래", "Left" => "왼쪽", "Right" => "오른쪽",
            "Red" => "빨강", "Green" => "초록", "Blue" => "파랑", "Gray" => "회색", "Purple" => "보라",
            "Crimson" => "진홍", "Yellow" => "노랑", "Pink" => "분홍", "" => "미입력", _ => value
        };
    }
}
