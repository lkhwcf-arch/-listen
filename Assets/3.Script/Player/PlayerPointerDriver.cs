using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace EscapeGame.Player
{
    // 포인터 입력과 커서 수명을 한곳에서 소유합니다. 시점/상호작용은 같은 프레임 결과를 소비합니다.
    [DefaultExecutionOrder(-150)]
    public sealed class PlayerPointerDriver : MonoBehaviour
    {
        private IPlayerPointerInput input;
        private bool skipFrame, pausedByApplication, ownsCursor;
        private CursorLockMode savedLock;
        private bool savedVisible;
        public PlayerControlState Controls { get; private set; }
        public PlayerLookCommand Look { get; private set; }
        public bool PrimaryPressed { get; private set; }
        public bool CancelPressed { get; private set; }
        public bool IsReady => input != null && isActiveAndEnabled;
        public event Action InputReset;
        public void Initialize(IPlayerPointerInput input, PlayerControlState controls)
        {
            if (this.input != null) throw new InvalidOperationException("포인터는 한 번만 초기화합니다.");
            this.input = input ?? throw new ArgumentNullException(nameof(input));
            Controls = controls ?? throw new ArgumentNullException(nameof(controls));
        }
        private void OnEnable()
        {
            if (input == null) return;
            savedLock = Cursor.lockState; savedVisible = Cursor.visible; ownsCursor = true;
            Controls.Changed += ApplyControl;
            Controls.SetFocused(Application.isFocused); Controls.SetPaused(pausedByApplication || Time.timeScale == 0);
            Controls.SetActive(true); Controls.CapturePointer(); ApplyControl();
        }
        private void OnDisable()
        {
            if (input == null) return;
            Controls.SetActive(false); Controls.Changed -= ApplyControl; input.SetEnabled(false);
            ClearFrame();
            if (ownsCursor) { Cursor.lockState = savedLock; Cursor.visible = savedVisible; ownsCursor = false; }
        }
        private void OnApplicationFocus(bool focused) => Controls?.SetFocused(focused);
        private void OnApplicationPause(bool paused) { pausedByApplication = paused; Controls?.SetPaused(paused || Time.timeScale == 0); }
        private void ClearFrame() { Look = default; PrimaryPressed = false; CancelPressed = false; }
        private void ApplyControl()
        {
            input.SetEnabled(isActiveAndEnabled && Application.isFocused && !pausedByApplication);
            input.SetLookEnabled(Controls.GameplayEnabled);
            ClearFrame(); skipFrame = true; InputReset?.Invoke();
            Cursor.lockState = Controls.GameplayEnabled ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !Controls.GameplayEnabled;
        }
        private void Update()
        {
            ClearFrame(); if (input == null) return;
            Controls.SetPaused(pausedByApplication || Time.timeScale == 0);
            if (Controls.PointerCaptured && Cursor.lockState != CursorLockMode.Locked) Controls.ReleasePointer();
            if (input.ReleasePressed) { Controls.ReleasePointer(); CancelPressed = true; return; }
            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (!Controls.PointerCaptured && input.CapturePressed && !overUi)
            { Controls.CapturePointer(); return; } // 커서를 다시 잡는 클릭으로 대상을 동시에 작동시키지 않습니다.
            if (!Controls.GameplayEnabled) return;
            if (skipFrame) { skipFrame = false; return; }
            Look = input.ReadLook(); PrimaryPressed = input.CapturePressed && !overUi;
        }
        private void OnDestroy() { input?.Dispose(); input = null; }
    }
}
