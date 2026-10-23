using System;
using EscapeGame.Interaction;
using EscapeGame.Player;
using TMPro;
using UnityEngine;

namespace EscapeGame.World
{
    // 상태 관찰과 Unity 조작·표현을 연결하는 수명 어댑터입니다.
    public sealed class SessionGameplayDriver : MonoBehaviour
    {
        private SessionStartGate gate;
        private PlayerPointerDriver pointer;
        private PlayerInteractionDriver interaction;
        private SessionWaitingView view;
        private bool subscribed;
        public void Initialize(SessionStartGate gate, PlayerPointerDriver pointer, PlayerInteractionDriver interaction, TMP_FontAsset font)
        {
            if (this.gate != null)
                throw new InvalidOperationException("게임 시작 연결은 한 번만 초기화합니다.");
            this.gate = gate ?? throw new ArgumentNullException(nameof(gate));
            this.pointer = pointer ?? throw new ArgumentNullException(nameof(pointer));
            this.interaction = interaction ?? throw new ArgumentNullException(nameof(interaction));
            view = new SessionWaitingView(gameObject.scene, font);
            if (isActiveAndEnabled)
                StartObserving();
        }
        private void OnEnable()
        {
            if (gate != null)
                StartObserving();
        }
        private void StartObserving()
        {
            if (subscribed)
                return;
            gate.Changed += Apply;
            subscribed = true;
            Apply();
        }
        private void Apply()
        {
            if (pointer == null)
                return;
            pointer.Controls.SetGameplayAllowed(gate.CanPlay);
            if (!gate.CanPlay && interaction != null)
                interaction.Closeup?.Close();
            view.Render(gate.CanPlay, gate.Message);
        }
        private void OnDisable()
        {
            if (subscribed)
            {
                gate.Changed -= Apply;
                subscribed = false;
            }
            if (pointer != null)
                pointer.Controls.SetGameplayAllowed(false);
        }
        private void OnDestroy()
        {
            view?.Dispose();
        }
    }
}
