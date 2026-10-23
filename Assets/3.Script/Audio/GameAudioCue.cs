namespace EscapeGame.Audio
{
    // 동작 종류만 코드로 정의하고 음원·볼륨·순서·UI 제한은 GameFeedback 에셋이 소유합니다.
    // 번호는 직렬화 계약입니다. 새 큐는 새 번호로 추가하고 기존 번호를 재사용하지 않습니다.
    public enum GameAudioCue
    {
        Footstep = 0,
        Click = 1,
        ClickClose = 2,
        CloseupOpen = 3,
        CloseupClose = 4,
        DialTurn = 5,
        Confirm = 6,
        Button = 7,
        Reset = 8,
        DoorOpen = 9,
        DoorClose = 10,
        DoorLocked = 11,
        Correct = 12,
        Wrong = 13,
    }
}
