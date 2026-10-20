using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace EscapeGame.Player
{
    [DefaultExecutionOrder(-100)]
    public sealed class PlayerLookDriver : MonoBehaviour
    {
        private Transform view;
        private IPlayerPointerInput input;
        private PlayerLookMotor motor;
        private bool skipLookFrame, pausedByApplication, ownsCursor;
        private CursorLockMode savedLock;
        private bool savedVisible;
        public PlayerControlState Controls { get; private set; }
        public PlayerLookMotor Motor => motor;
        public bool IsReady => input != null && view != null && motor != null && isActiveAndEnabled;
        public void Initialize(Transform view, PlayerCharacterSettings settings, int unitMilliDegreesPerPixel,
            IPlayerPointerInput input, PlayerControlState controls)
        {
            if (this.input != null) throw new InvalidOperationException("시점 Driver는 한 번만 초기화합니다.");
            this.view = view != null ? view : throw new ArgumentNullException(nameof(view));
            motor = new PlayerLookMotor(settings, unitMilliDegreesPerPixel,
                PlayerCharacterSettings.ToMilliUnit(transform.eulerAngles.y, "InitialYaw"));
            this.input = input ?? throw new ArgumentNullException(nameof(input));
            Controls = controls ?? throw new ArgumentNullException(nameof(controls));
        }
        private void OnEnable()
        {
            if (input == null) return;
            savedLock = Cursor.lockState; savedVisible = Cursor.visible; ownsCursor = true;
            Controls.Changed += ApplyControl;
            Controls.SetFocused(Application.isFocused);
            Controls.SetPaused(pausedByApplication || Time.timeScale == 0);
            Controls.SetActive(true);
            // 첫 입장만 자동 포획. Alt+Tab/ESC 후에는 좌클릭으로 명시적으로 복귀합니다.
            Controls.CapturePointer();
            ApplyControl();
        }
        private void OnDisable()
        {
            if (input == null) return;
            Controls.SetActive(false);
            Controls.Changed -= ApplyControl;
            input.SetEnabled(false);
            if (ownsCursor) { Cursor.lockState = savedLock; Cursor.visible = savedVisible; ownsCursor = false; }
        }
        private void OnApplicationFocus(bool focused) { Controls?.SetFocused(focused); }
        private void OnApplicationPause(bool paused)
        { pausedByApplication = paused; Controls?.SetPaused(paused || Time.timeScale == 0); }
        private void ApplyControl()
        {
            if (input == null) return;
            input.SetEnabled(isActiveAndEnabled && Application.isFocused && !pausedByApplication);
            input.SetLookEnabled(Controls.GameplayEnabled);
            motor.ClearPendingInput(); skipLookFrame = true;
            Cursor.lockState = Controls.GameplayEnabled ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !Controls.GameplayEnabled;
        }
        private void Update()
        {
            if (input == null) return;
            Controls.SetPaused(pausedByApplication || Time.timeScale == 0);
            if (Controls.PointerCaptured && Cursor.lockState != CursorLockMode.Locked) Controls.ReleasePointer();
            if (input.ReleasePressed) Controls.ReleasePointer();
            else if (!Controls.PointerCaptured && input.CapturePressed &&
                (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject())) Controls.CapturePointer();
            if (!Controls.GameplayEnabled) return;
            if (skipLookFrame) { skipLookFrame = false; return; }
            motor.Apply(input.ReadLook());
            transform.rotation = Quaternion.Euler(0, motor.YawMilliDegrees / 1000f, 0);
            view.localRotation = Quaternion.Euler(motor.PitchMilliDegrees / 1000f, 0, 0);
        }
        private void OnDestroy() { input?.Dispose(); input = null; }
    }
}
