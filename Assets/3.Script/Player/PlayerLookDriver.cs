using System;
using UnityEngine;

namespace EscapeGame.Player
{
    [DefaultExecutionOrder(-100)]
    public sealed class PlayerLookDriver : MonoBehaviour
    {
        private Transform view;
        private PlayerPointerDriver pointer;
        private PlayerLookMotor motor;
        public PlayerControlState Controls => pointer?.Controls;
        public PlayerLookMotor Motor => motor;
        public bool IsReady => pointer != null && pointer.IsReady && view != null && motor != null && isActiveAndEnabled;
        public void Initialize(Transform view, PlayerCharacterSettings settings, int unitMilliDegreesPerPixel, PlayerPointerDriver pointer)
        {
            if (this.pointer != null)
                throw new InvalidOperationException("시점은 한 번만 초기화합니다.");
            this.view = view != null ? view : throw new ArgumentNullException(nameof(view));
            motor = new PlayerLookMotor(settings, unitMilliDegreesPerPixel, PlayerCharacterSettings.ToMilliUnit(transform.eulerAngles.y, "InitialYaw"));
            this.pointer = pointer != null ? pointer : throw new ArgumentNullException(nameof(pointer));
        }
        private void OnEnable()
        {
            if (pointer != null)
                pointer.InputReset += ResetInput;
        }
        private void OnDisable()
        {
            if (pointer != null)
                pointer.InputReset -= ResetInput;
            ResetInput();
        }
        private void ResetInput() => motor?.ClearPendingInput();
        private void Update()
        {
            if (!IsReady || !Controls.GameplayEnabled)
                return;
            motor.Apply(pointer.Look);
            transform.rotation = Quaternion.Euler(0, motor.YawMilliDegrees / 1000f, 0);
            view.localRotation = Quaternion.Euler(motor.PitchMilliDegrees / 1000f, 0, 0);
        }
    }
}
