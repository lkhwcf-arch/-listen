using System;
using EscapeGame.Data;
using UnityEngine;

namespace EscapeGame.Player
{
    [DefaultExecutionOrder(100)]
    public sealed class PlayerGaitDriver : MonoBehaviour
    {
        private PlayerMovementDriver movement;
        private PlayerPointerDriver pointer;
        private Transform cameraTransform;
        private Vector3 rest;
        private PlayerGait gait;
        private float distance;
        private bool grounded, subscribed;
        public event Action Stepped;
        public void Initialize(Transform cameraTransform, PlayerMovementDriver movement, PlayerPointerDriver pointer,
            PlayerCharacterSettings character, GameFeedbackAssets settings)
        {
            this.cameraTransform = cameraTransform;
            this.movement = movement;
            this.pointer = pointer;
            rest = cameraTransform.localPosition;
            gait = new PlayerGait(settings.StepDistanceMeters, character.HeadBobMeters, settings.HeadBobReturnSeconds);
            gait.Stepped += OnStep;
            if (isActiveAndEnabled)
                Subscribe();
        }
        private void OnEnable()
        {
            if (gait != null)
                Subscribe();
        }
        private void Subscribe()
        {
            if (subscribed)
                return;
            movement.MotionApplied += OnMotion;
            subscribed = true;
        }
        private void OnMotion(float meters, bool isGrounded)
        {
            distance += meters;
            grounded = isGrounded;
        }
        private void OnStep() => Stepped?.Invoke();
        private void LateUpdate()
        {
            if (gait == null)
                return;
            gait.Tick(distance, grounded, pointer.Controls.GameplayEnabled && Time.timeScale > 0, Time.unscaledDeltaTime);
            cameraTransform.localPosition = rest + Vector3.up * (float)gait.OffsetMeters;
            distance = 0;
        }
        private void OnDisable()
        {
            if (subscribed && movement != null)
                movement.MotionApplied -= OnMotion;
            subscribed = false;
            distance = 0;
            gait?.Reset();
            if (cameraTransform != null)
                cameraTransform.localPosition = rest;
        }
        private void OnDestroy()
        {
            if (gait != null)
                gait.Stepped -= OnStep;
            Stepped = null;
        }
    }
}
