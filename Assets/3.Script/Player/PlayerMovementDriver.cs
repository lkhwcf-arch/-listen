using System;
using EscapeGame.Timing;
using UnityEngine;

namespace EscapeGame.Player
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMovementDriver : MonoBehaviour
    {
        private CharacterController controller;
        private PlayerMovementMotor motor;
        private IPlayerMoveInput input;
        private IMillisecondClock clock;
        private long lastMs;
        private int maximumFrameMs, groundContactMm;
        private bool movementEnabled = true, focused = true;
        private PlayerControlState controls;
        private bool CanMove => movementEnabled && focused && (controls == null || controls.GameplayEnabled);
        public PlayerMovementMotor Motor => motor;
        public bool IsReady => motor != null && input != null && isActiveAndEnabled && controller != null && controller.enabled;

        // Factory가 비활성 오브젝트에 의존성을 주입한 뒤 활성화합니다. input의 수명은 이 Driver가 소유합니다.
        public void Initialize(PlayerCharacterSettings settings, IPlayerMoveInput input, IMillisecondClock clock,
            int maximumFrameMs, int maximumFallSpeedMmPerSecond, int groundContactMm, PlayerControlState controls = null)
        {
            if (this.input != null) throw new InvalidOperationException("이동 Driver는 한 번만 초기화합니다.");
            if (maximumFrameMs < 1 || maximumFrameMs > 100 || groundContactMm < 1 || groundContactMm > 100)
                throw new ArgumentOutOfRangeException("프레임 제한·접지 보정 범위를 확인하세요.");
            if (Physics.gravity.x != 0 || Physics.gravity.z != 0)
                throw new InvalidOperationException("현재 이동은 월드 Y축 중력을 사용합니다.");
            controller = GetComponent<CharacterController>();
            motor = new PlayerMovementMotor(settings, PlayerCharacterSettings.ToMilliUnit(Physics.gravity.y, "Physics.gravity.y"), maximumFallSpeedMmPerSecond);
            this.input = input ?? throw new ArgumentNullException(nameof(input));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.maximumFrameMs = maximumFrameMs; this.groundContactMm = groundContactMm;
            this.controls = controls;
            focused = Application.isFocused;
        }

        public void SetMovementEnabled(bool enabled)
        {
            movementEnabled = enabled;
            ResetInput(); // 이동 단독 검사/차단 명령. UI 전체 잠금은 공유 Controls.BlockForUi를 사용합니다.
        }

        private void OnEnable() { if (controls != null) controls.Changed += ResetInput; ResetInput(); }
        private void OnDisable() { if (controls != null) controls.Changed -= ResetInput; input?.SetEnabled(false); motor?.Reset(); }
        private void OnApplicationFocus(bool hasFocus) { focused = hasFocus; ResetInput(); }
        private void OnApplicationPause(bool paused) { focused = !paused && Application.isFocused; ResetInput(); }
        private void ResetInput()
        {
            if (input == null) return;
            input.SetEnabled(isActiveAndEnabled && CanMove);
            motor.Reset(); lastMs = clock.ElapsedMilliseconds;
        }

        private void Update()
        {
            if (input == null) return;
            long now = clock.ElapsedMilliseconds;
            long elapsed = Math.Max(0, now - lastMs); lastMs = now;
            if (!CanMove || Time.timeScale == 0 || !controller.enabled)
            { motor.Reset(); return; }
            int elapsedMs = (int)Math.Min(elapsed, maximumFrameMs);
            if (elapsedMs == 0) return;
            bool grounded = controller.isGrounded;
            if (grounded) motor.OnGrounded();
            var step = motor.Tick(input.Read(), elapsedMs);
            // Unity Transform/충돌 API 경계에서만 m 및 float로 바꿉니다. 이동 방향은 플레이어의 Y 회전 기준입니다.
            var horizontal = Quaternion.Euler(0, transform.eulerAngles.y, 0) * new Vector3(step.RightMm, 0, step.ForwardMm);
            int upMm = grounded ? Math.Min(step.UpMm, -groundContactMm) : step.UpMm;
            var flags = controller.Move((horizontal + Vector3.up * upMm) / 1000f);
            if ((flags & CollisionFlags.Below) != 0) motor.OnGrounded();
        }

        private void OnDestroy() { input?.Dispose(); input = null; }
    }
}
