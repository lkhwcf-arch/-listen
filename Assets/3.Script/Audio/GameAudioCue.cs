namespace EscapeGame.Audio
{
    // 동작 종류만 코드로 정의하고 음원·볼륨·순서·UI 제한은 GameFeedback 에셋이 소유합니다.
    public enum GameAudioCue
    {
        Footstep, Click, ClickClose, CloseupOpen, CloseupClose, DialTurn, Confirm,
        Button, Reset, DoorOpen, DoorClose, DoorLocked, Correct, Wrong
    }
}
