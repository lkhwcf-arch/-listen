using System;
using System.Linq;
using EscapeGame.Data.Tables;
using EscapeGame.World;
using UnityEngine;

namespace EscapeGame.Player
{
    public sealed class PlayerSpawnDriver : MonoBehaviour
    {
        public FirstPersonPlayerRig Player { get; private set; }
        public event Action<FirstPersonPlayerRig> Spawned;
        public void Initialize(CsvGameData data, string role, RoomSceneView firstRoom = null)
        {
            if (Player != null)
            {
                if (Player.Role != role) throw new InvalidOperationException("역할을 바꾸려면 기존 플레이어를 먼저 정리하세요.");
                return;
            }
            var settings = Resources.Load<PlayerPresentationSettings>("PlayerPresentation")
                ?? throw new InvalidOperationException("PlayerPresentation 설정 에셋이 필요합니다.");
            var plan = new PlayerSpawnPlan(data, role, settings.SpawnPrefabId, settings.CharacterConfigId);
            var rooms = firstRoom != null ? new[] { firstRoom } : FindObjectsByType<RoomSceneView>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(room => room.gameObject.scene == gameObject.scene && room.RoomNo == plan.Room.RoomNo).ToArray();
            if (rooms.Length != 1 || rooms[0].gameObject.scene != gameObject.scene || rooms[0].RoomNo != plan.Room.RoomNo)
                throw new InvalidOperationException("첫 방의 RoomSceneView가 현재 씬에 정확히 하나 필요합니다.");
            Player = LocalPlayerFactory.Spawn(plan, rooms[0].transform, settings);
            Spawned?.Invoke(Player);
        }
        private void OnDestroy()
        {
            if (Player != null) { Player.gameObject.SetActive(false); Destroy(Player.gameObject); }
        }
    }
}
