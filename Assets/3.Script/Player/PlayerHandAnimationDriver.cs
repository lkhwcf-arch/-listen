using System;
using UnityEngine;

namespace EscapeGame.Player
{
    // 충돌 처리 후의 이동 결과를 손 모델의 애니메이터에 전달하는 표현 어댑터입니다.
    [DefaultExecutionOrder(90)]
    public sealed class PlayerHandAnimationDriver : MonoBehaviour
    {
        private Animator[] animators;
        private PlayerMovementDriver movement;
        private PlayerControlState controls;
        private int speedParameter;
        private string speedParameterName;
        private bool bindingsValidated;
        private float distanceMeters;
        private float lastAppliedSpeed = float.NaN;
        private bool subscribed;
        public float SpeedMetersPerSecond
        {
            get;
            private set;
        }

        public void Initialize(Animator[] animators, PlayerMovementDriver movement,
            PlayerControlState controls, string speedParameterName)
        {
            if (this.movement != null)
                throw new InvalidOperationException("손 애니메이션은 한 번만 연결합니다.");
            if (animators == null || animators.Length == 0 || movement == null || controls == null)
                throw new ArgumentException("손 애니메이터·이동 결과·조작 상태가 필요합니다.");
            if (string.IsNullOrWhiteSpace(speedParameterName))
                throw new ArgumentException("손 애니메이션의 이동 속도 파라미터 이름이 필요합니다.");
            foreach (var animator in animators)
                if (animator == null || animator.runtimeAnimatorController == null)
                    throw new InvalidOperationException("손 모델의 애니메이션 제어기를 연결하세요.");
            this.animators = animators;
            this.movement = movement;
            this.controls = controls;
            this.speedParameterName = speedParameterName;
            speedParameter = Animator.StringToHash(speedParameterName);
            if (isActiveAndEnabled)
            {
                ValidateRuntimeBinding();
                Subscribe();
            }
        }

        // 비활성 조립 중에는 플레이어용 Animator의 파라미터가 아직 생성되지 않을 수 있습니다.
        // Factory가 루트를 활성화한 뒤 같은 생성 실패 처리 범위 안에서 명시적으로 확인합니다.
        public void ValidateRuntimeBinding()
        {
            if (bindingsValidated)
                return;
            if (animators == null || movement == null || !isActiveAndEnabled)
                throw new InvalidOperationException("손 애니메이션은 연결·활성화 후 검증하세요.");
            foreach (var animator in animators)
            {
                if (animator == null || !animator.isActiveAndEnabled)
                    throw new InvalidOperationException("손 애니메이터와 모델 계층을 활성화하세요.");
                if (!animator.isInitialized)
                    animator.Rebind();
                if (!animator.isInitialized)
                    throw new InvalidOperationException($"손 애니메이터 {animator.name}의 초기화를 완료하지 못했습니다.");
                bool found = false;
                foreach (var parameter in animator.parameters)
                    if (parameter.nameHash == speedParameter && parameter.type == AnimatorControllerParameterType.Float)
                        found = true;
                if (!found)
                    throw new InvalidOperationException($"손 애니메이터 {animator.name}에 실수형 {speedParameterName} 파라미터가 필요합니다.");
            }
            bindingsValidated = true;
            ResetMotion();
        }

        private void OnEnable()
        {
            if (movement == null)
                return;
            Subscribe();
            ResetMotion();
        }

        private void Subscribe()
        {
            if (subscribed)
                return;
            movement.MotionApplied += OnMotion;
            controls.Changed += OnControlsChanged;
            subscribed = true;
        }

        private void OnMotion(float meters, bool isGrounded)
        {
            if (isGrounded)
                distanceMeters += meters;
        }

        private void OnControlsChanged()
        {
            if (!controls.GameplayEnabled)
                ResetMotion();
        }

        private void LateUpdate()
        {
            if (movement == null)
            {
                distanceMeters = 0;
                ApplySpeed(0);
                return;
            }
            float speed = controls.GameplayEnabled && Time.timeScale > 0 && Time.deltaTime > 0
                ? distanceMeters / Time.deltaTime : 0;
            ApplySpeed(speed);
            distanceMeters = 0;
        }

        private void ApplySpeed(float speed)
        {
            SpeedMetersPerSecond = speed;
            if (!bindingsValidated || animators == null || speed == lastAppliedSpeed)
                return;
            bool allReady = true;
            foreach (var animator in animators)
            {
                if (animator == null || !animator.isActiveAndEnabled || !animator.isInitialized)
                {
                    allReady = false;
                    continue;
                }
                animator.SetFloat(speedParameter, speed);
            }
            // 비활성 모델이 초기화된 뒤에도 첫 값을 전달할 수 있도록 미적용 값을 캐시하지 않습니다.
            lastAppliedSpeed = allReady ? speed : float.NaN;
        }

        private void ResetMotion()
        {
            distanceMeters = 0;
            lastAppliedSpeed = float.NaN;
            ApplySpeed(0);
        }

        private void OnDisable()
        {
            if (subscribed)
            {
                if (movement != null)
                    movement.MotionApplied -= OnMotion;
                controls.Changed -= OnControlsChanged;
            }
            subscribed = false;
            ResetMotion();
        }
    }
}
