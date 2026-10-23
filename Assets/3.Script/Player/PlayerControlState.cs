using System;

namespace EscapeGame.Player
{
    // 로컬 조작 상태만 소유합니다. 나중에 Closeup이 같은 잠금 계약을 사용합니다.
    public sealed class PlayerControlState
    {
        private bool active, focused = true, paused, captured;
        private int uiBlocks;
        private bool gameplayAllowed = true;
        public bool PointerCaptured => captured;
        public bool CanCapture => gameplayAllowed && active && focused && !paused && uiBlocks == 0;
        public bool GameplayEnabled => CanCapture && captured;
        public event Action Changed;
        // 외부 시작 조건을 공통 조작 상태에 반영합니다. 통신 타입이나 세션을 참조하지 않습니다.
        public void SetGameplayAllowed(bool value)
        {
            if (gameplayAllowed == value)
                return;
            gameplayAllowed = value;
            if (!value)
                captured = false;
            Changed?.Invoke();
        }
        public void SetActive(bool value)
        {
            if (active == value)
                return;
            active = value;
            if (!value)
                captured = false;
            Changed?.Invoke();
        }
        public void SetFocused(bool value)
        {
            if (focused == value)
                return;
            focused = value;
            if (!value)
                captured = false;
            Changed?.Invoke();
        }
        public void SetPaused(bool value)
        {
            if (paused == value)
                return;
            paused = value;
            if (value)
                captured = false;
            Changed?.Invoke();
        }
        public void CapturePointer()
        {
            if (!CanCapture || captured)
                return;
            captured = true;
            Changed?.Invoke();
        }
        public void ReleasePointer()
        {
            if (!captured)
                return;
            captured = false;
            Changed?.Invoke();
        }
        public IDisposable BlockForUi()
        {
            uiBlocks++;
            captured = false;
            Changed?.Invoke();
            return new UiBlock(this);
        }
        private sealed class UiBlock : IDisposable
        {
            private PlayerControlState owner;
            public UiBlock(PlayerControlState owner) => this.owner = owner;
            public void Dispose()
            {
                if (owner == null)
                    return;
                var target = owner;
                owner = null;
                target.uiBlocks--;
                target.Changed?.Invoke();
            }
        }
    }
}
