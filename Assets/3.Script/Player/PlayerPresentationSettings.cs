using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EscapeGame.Player
{
    [CreateAssetMenu(menuName = "EscapeGame/Player Presentation")]
    public sealed class PlayerPresentationSettings : ScriptableObject
    {
        [SerializeField] private int spawnPrefabId = 13001;
        [SerializeField] private int characterConfigId = 16010;
        [SerializeField] private int nearClipMm = 30;
        [SerializeField] private int farClipMm = 200000;
        [SerializeField] private Vector3Int modelPositionMm = new Vector3Int(180, -200, 500);
        [SerializeField] private Vector3Int modelSizeMm = new Vector3Int(180, 180, 180);
        [SerializeField] private Vector3Int modelEulerMilliDegrees;
        [SerializeField] private Material placeholderMaterial;
        public Material PlaceholderMaterial => placeholderMaterial;
        [SerializeField] private GameObject modelA;
        [SerializeField] private GameObject modelB;
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private int maximumMovementFrameMs = 100;
        [SerializeField] private int maximumFallSpeedMmPerSecond = 50000;
        [SerializeField] private int groundContactMm = 2;
        [SerializeField] private int skinWidthMm = 30;
        [SerializeField] private int lookMilliDegreesPerPixelAtUnitSensitivity = 100;
        [SerializeField] private int modelCollisionMarginMm = 20;
        public int SkinWidthMm => skinWidthMm;
        public int LookMilliDegreesPerPixelAtUnitSensitivity => lookMilliDegreesPerPixelAtUnitSensitivity;
        public int ModelCollisionMarginMm => modelCollisionMarginMm;
        public InputActionAsset InputActions => inputActions;
        public int MaximumMovementFrameMs => maximumMovementFrameMs;
        public int MaximumFallSpeedMmPerSecond => maximumFallSpeedMmPerSecond;
        public int GroundContactMm => groundContactMm;
        public int SpawnPrefabId => spawnPrefabId;
        public int CharacterConfigId => characterConfigId;
        public int NearClipMm => nearClipMm;
        public int FarClipMm => farClipMm;
        public Vector3Int ModelPositionMm => modelPositionMm;
        public Vector3Int ModelSizeMm => modelSizeMm;
        public Vector3Int ModelEulerMilliDegrees => modelEulerMilliDegrees;
        public GameObject ModelFor(string role) => role == "A" ? modelA : role == "B" ? modelB : throw new ArgumentException("역할은 A 또는 B입니다.");
        public void Validate()
        {
            if (spawnPrefabId <= 0 || characterConfigId <= 0 || nearClipMm <= 0 || farClipMm <= nearClipMm ||
                modelPositionMm.z <= nearClipMm || modelSizeMm.x <= 0 || modelSizeMm.y <= 0 || modelSizeMm.z <= 0)
                throw new InvalidOperationException("PlayerPresentation의 ID·클리핑·모델 크기 설정을 확인하세요.");
            if ((modelA == null || modelB == null) && placeholderMaterial == null)
                throw new InvalidOperationException("PlayerPresentation에 빌드용 임시 모델 머티리얼을 연결하세요.");
            if (inputActions == null || inputActions.FindAction("Player/Move", false) == null ||
                inputActions.FindAction("Player/Look", false) == null || inputActions.FindAction("Player/Attack", false) == null ||
                inputActions.FindAction("UI/Cancel", false) == null || skinWidthMm < 1 || skinWidthMm > 100 ||
                lookMilliDegreesPerPixelAtUnitSensitivity < 1 || lookMilliDegreesPerPixelAtUnitSensitivity > 1000 ||
                modelCollisionMarginMm < 1 || modelCollisionMarginMm > 100 ||
                maximumMovementFrameMs < 1 || maximumMovementFrameMs > 100 || maximumFallSpeedMmPerSecond <= 0 ||
                maximumFallSpeedMmPerSecond > 100000 || groundContactMm < 1 || groundContactMm > 100)
                throw new InvalidOperationException("PlayerPresentation의 Player/Move·Look·Attack·UI/Cancel 및 이동·시점·충돌 어댑터 설정을 확인하세요.");
        }
    }
}
