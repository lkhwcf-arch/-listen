using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EscapeGame.Player
{
    public interface IPlayerPointerInput : IDisposable
    {
        PlayerLookCommand ReadLook();
        bool CapturePressed { get; }
        bool ReleasePressed { get; }
        void SetEnabled(bool enabled);
        void SetLookEnabled(bool enabled);
    }
    public sealed class InputSystemPointerInput : IPlayerPointerInput
    {
        private readonly InputActionLease look, capture, release;
        public InputSystemPointerInput(InputActionAsset asset)
        {
            // 먼저 전부 확인하여 부분 생성 실패로 액션 수명이 남지 않게 합니다.
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            foreach (string path in new[] { "Player/Look", "Player/Attack", "UI/Cancel" }) asset.FindAction(path, true);
            look = new InputActionLease(asset, "Player/Look");
            capture = new InputActionLease(asset, "Player/Attack");
            release = new InputActionLease(asset, "UI/Cancel");
        }
        public bool CapturePressed => capture.Action.WasPressedThisFrame();
        public bool ReleasePressed => release.Action.WasPressedThisFrame();
        public PlayerLookCommand ReadLook()
        {
            Vector2 delta = look.Action.enabled ? look.Action.ReadValue<Vector2>() : Vector2.zero;
            int Normalize(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0 :
                Mathf.RoundToInt(Mathf.Clamp(value, -10000, 10000) * 1000);
            return new PlayerLookCommand(Normalize(delta.x), Normalize(delta.y));
        }
        public void SetEnabled(bool enabled)
        { capture.SetEnabled(enabled); release.SetEnabled(enabled); if (!enabled) look.SetEnabled(false); }
        public void SetLookEnabled(bool enabled) => look.SetEnabled(enabled);
        public void Dispose() { look.Dispose(); capture.Dispose(); release.Dispose(); }
    }
}
