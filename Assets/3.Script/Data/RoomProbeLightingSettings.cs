using System;
using UnityEngine;

namespace EscapeGame.Data
{
    [CreateAssetMenu(menuName = "EscapeGame/방 조명 프로브 설정")]
    public sealed class RoomProbeLightingSettings : ScriptableObject
    {
        [SerializeField] private LightProbes probesA, probesB;
        [SerializeField] private bool disableSceneDirectionalLights = true;
        public bool DisableSceneDirectionalLights => disableSceneDirectionalLights;
        public LightProbes ForRole(string role) => role == "A" ? probesA : role == "B" ? probesB
            : throw new ArgumentException("플레이어 역할은 A 또는 B입니다.", nameof(role));
    }
}
