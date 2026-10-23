using System.Collections.Generic;
using UnityEngine;

namespace EscapeGame.Player
{
    // 입력·통신을 소유하지 않습니다. 카메라/오디오 소유권과 모델 계층의 수명만 관리합니다.
    public sealed class FirstPersonPlayerRig : MonoBehaviour
    {
        private readonly List<Camera> previousCameras = new List<Camera>();
        private readonly List<AudioListener> previousListeners = new List<AudioListener>();
        public string Role
        {
            get;
            private set;
        }
        public Camera PlayerCamera
        {
            get;
            private set;
        }
        public Transform VisualRoot
        {
            get;
            private set;
        }
        public void Initialize(string role, Camera camera, Transform visualRoot)
        {
            Role = role;
            PlayerCamera = camera;
            VisualRoot = visualRoot;
        }
        private void OnEnable()
        {
            if (PlayerCamera == null)
                return;
            foreach (var camera in FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (camera != PlayerCamera && camera.gameObject.scene == gameObject.scene && camera.enabled &&
                    camera.cameraType == CameraType.Game && camera.targetTexture == null && camera.targetDisplay == PlayerCamera.targetDisplay)
                {
                    previousCameras.Add(camera);
                    camera.enabled = false;
                }
            foreach (var listener in FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (!listener.transform.IsChildOf(transform) && listener.gameObject.scene == gameObject.scene && listener.enabled)
                {
                    previousListeners.Add(listener);
                    listener.enabled = false;
                }
        }
        private void OnDisable()
        {
            foreach (var camera in previousCameras)
                if (camera != null)
                    camera.enabled = true;
            foreach (var listener in previousListeners)
                if (listener != null)
                    listener.enabled = true;
            previousCameras.Clear();
            previousListeners.Clear();
        }
    }
}
