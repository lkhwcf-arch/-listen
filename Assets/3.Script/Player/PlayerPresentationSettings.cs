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
        [SerializeField] private GameObject modelA;
        [SerializeField] private GameObject modelB;
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private int maximumMovementFrameMs = 100;
        [SerializeField] private int maximumFallSpeedMmPerSecond = 50000;
        [SerializeField] private int groundContactMm = 2;
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
            if (inputActions == null || inputActions.FindAction("Player/Move", false) == null ||
                maximumMovementFrameMs < 1 || maximumMovementFrameMs > 100 || maximumFallSpeedMmPerSecond <= 0 ||
                maximumFallSpeedMmPerSecond > 100000 || groundContactMm < 1 || groundContactMm > 100)
                throw new InvalidOperationException("PlayerPresentation의 Player/Move 입력·정수 이동 어댑터 설정을 확인하세요.");
        }
    }
}
