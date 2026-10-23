using System;
using UnityEngine.InputSystem;

namespace EscapeGame.Player
{
    // 입력 기능마다 원본 액션을 켜고 끄지 않도록 복제·바인딩·해제를 공통화합니다.
    public sealed class InputActionLease : IDisposable
    {
        private bool disposed;
        public InputAction Action
        {
            get;
        }
        public InputActionLease(InputActionAsset asset, string path)
        {
            if (asset == null)
                throw new ArgumentNullException(nameof(asset));
            Action = asset.FindAction(path, true).Clone();
            Action.bindingMask = InputBinding.MaskByGroup("Keyboard&Mouse");
        }
        public void SetEnabled(bool enabled)
        {
            if (disposed)
                return;
            if (enabled)
                Action.Enable();
            else
                Action.Disable();
        }
        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            Action.Disable();
            Action.Dispose();
        }
    }
}
