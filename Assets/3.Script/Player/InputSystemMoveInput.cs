using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EscapeGame.Player
{
    public interface IPlayerMoveInput : IDisposable
    {
        PlayerMoveCommand Read();
        void SetEnabled(bool enabled);
    }

    // 기존 바인딩을 복제해 이 플레이어의 Move 액션만 소유합니다. 원본/다른 UI 액션은 건드리지 않습니다.
    public sealed class InputSystemMoveInput : IPlayerMoveInput
    {
        private readonly InputAction move;
        public InputSystemMoveInput(InputActionAsset asset)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            move = asset.FindAction("Player/Move", true).Clone();
            move.bindingMask = InputBinding.MaskByGroup("Keyboard&Mouse");
        }
        public PlayerMoveCommand Read()
        {
            Vector2 axes = move.enabled ? move.ReadValue<Vector2>() : Vector2.zero;
            return new PlayerMoveCommand(Mathf.Clamp(Mathf.RoundToInt(axes.x * 1000), -1000, 1000),
                Mathf.Clamp(Mathf.RoundToInt(axes.y * 1000), -1000, 1000));
        }
        public void SetEnabled(bool enabled) { if (enabled) move.Enable(); else move.Disable(); }
        public void Dispose() { move.Disable(); move.Dispose(); }
    }
}
