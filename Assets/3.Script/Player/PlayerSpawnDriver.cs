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
        public EscapeGame.Audio.GameFeedbackDriver Feedback { get; private set; }
        public EscapeRunClock RunClock { get; private set; }
        public void Initialize(CsvGameData data, string role, RoomSceneView firstRoom = null, IRoomAnswerService answers = null, IRoomDoorService doors = null, long? enteredAtMs = null)
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
            var interactionSettings = Resources.Load<EscapeGame.Interaction.InteractionPresentationSettings>("InteractionPresentation")
                ?? throw new InvalidOperationException("InteractionPresentation 설정 에셋이 필요합니다.");
            interactionSettings.Validate();
            RunClock = new EscapeRunClock(enteredAtMs ?? (long)(Time.realtimeSinceStartupAsDouble * 1000));
            var spawned = LocalPlayerFactory.Spawn(plan, rooms[0].transform, settings);
            try
            {
                spawned.gameObject.AddComponent<EscapeGame.Interaction.PlayerInteractionDriver>().Initialize(
                    spawned.PlayerCamera, spawned.GetComponent<PlayerPointerDriver>(), plan.Character.InteractDistanceMm, interactionSettings, answers, role, doors ?? answers as IRoomDoorService);
                Player = spawned;
                var feedbackAssets = Resources.Load<EscapeGame.Data.GameFeedbackAssets>("GameFeedback");
                if (feedbackAssets != null)
                {
                    Feedback = spawned.gameObject.AddComponent<EscapeGame.Audio.GameFeedbackDriver>();
                    Feedback.Initialize(spawned, data, role, feedbackAssets, rooms[0]);
                }
                else Debug.LogWarning("[게임 표현] GameFeedback 설정이 없어 보행 연출·게임 효과음을 생략합니다.");
            }
            catch { spawned.gameObject.SetActive(false); Destroy(spawned.gameObject); throw; }
            Spawned?.Invoke(Player);
        }
        private void OnDestroy()
        {
            if (Player != null) { Player.gameObject.SetActive(false); Destroy(Player.gameObject); }
        }
    }
}
