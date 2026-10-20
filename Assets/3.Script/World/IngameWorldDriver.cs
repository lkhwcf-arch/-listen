using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using EscapeGame.Data.Tables;
using UnityEngine;

namespace EscapeGame.World
{
    public sealed class IngameWorldDriver : MonoBehaviour
    {
        [SerializeField] private RoomSceneView[] sceneRooms = Array.Empty<RoomSceneView>();
        private readonly Dictionary<int, RoomSceneView> loaded = new Dictionary<int, RoomSceneView>();
        private readonly Dictionary<int, string> failures = new Dictionary<int, string>();
        private GameFlowBootstrapper owner;
        private RoomLoadPlan plan;
        private CsvGameData data;
        private int[] externallyManagedPrefabIds = Array.Empty<int>();
        public event Action<RoomData, RoomSceneView> RoomLoaded;
        public event Action<int, string> RoomLoadFailed;
        private IReadOnlyDictionary<int, RoomSceneView> publishedRooms;
        public IReadOnlyDictionary<int, RoomSceneView> LoadedRooms => publishedRooms ??
            (publishedRooms = new ReadOnlyDictionary<int, RoomSceneView>(loaded));

        private void Start()
        {
            if (owner != null) return;
            var flow = FindFirstObjectByType<GameFlowBootstrapper>();
            // Ingame 단독 Play에서는 개발자 방 미리보기를 허용합니다. 정상 입장은 Bootstrapper가 초기화합니다.
            if (flow != null && flow.Service.Data != null) Initialize(flow);
        }
        public void Initialize(GameFlowBootstrapper flow)
        {
            if (flow == null || flow.Service.Data == null) throw new InvalidOperationException("방 로드에 등록된 게임 데이터가 필요합니다.");
            if (owner != null)
            {
                if (owner != flow) throw new InvalidOperationException("방 로더의 게임 소유자를 변경할 수 없습니다.");
                return;
            }
            var numbers = new HashSet<int>();
            foreach (var room in sceneRooms)
                if (room != null && (room.gameObject.scene != gameObject.scene || !numbers.Add(room.RoomNo)))
                    throw new InvalidOperationException("씬 방 참조는 현재 씬에 속하며 방 번호가 중복되지 않아야 합니다.");
            owner = flow;
            foreach (var room in sceneRooms) if (room != null) room.gameObject.SetActive(false);
            data = owner.Service.Data;
            var playerSettings = Resources.Load<EscapeGame.Player.PlayerPresentationSettings>("PlayerPresentation");
            if (playerSettings != null) externallyManagedPrefabIds = new[] { playerSettings.SpawnPrefabId };
            plan = new RoomLoadPlan(data, owner.Service.Role);
            owner.Service.RoomsChanged += ApplyRooms;
            ApplyRooms(owner.Service.UnlockedRoomIds);
        }
        public RoomSceneView RequireFirstRoom()
        {
            if (plan == null) throw new InvalidOperationException("방 로더를 먼저 초기화하세요.");
            if (loaded.TryGetValue(plan.First.Id, out var room)) return room;
            throw new InvalidOperationException("첫 방 준비 실패: " +
                (failures.TryGetValue(plan.First.Id, out var reason) ? reason : "해금된 첫 방이 없습니다."));
        }
        private void ApplyRooms(IReadOnlyList<int> ids)
        {
            if (!ReferenceEquals(data, owner.Service.Data)) return;
            foreach (int id in ids)
            {
                var row = plan.Find(id);
                if (row == null || loaded.ContainsKey(id)) continue;
                RoomSceneView view = null;
                bool instantiated = false;
                bool added = false;
                try
                {
                    foreach (var candidate in sceneRooms)
                        if (candidate != null && candidate.RoomNo == row.RoomNo) { view = candidate; break; }
                    if (view == null)
                    {
                        var prefab = owner.Assets.ResolvePrefab(plan.PrefabPath(row.RoomPrefabId));
                        var instance = Instantiate(prefab, prefab.transform.position, prefab.transform.rotation, transform);
                        instantiated = true;
                        view = instance.GetComponent<RoomSceneView>() ?? instance.AddComponent<RoomSceneView>();
                        view.Configure(row.RoomNo);
                    }
                    view.Load(row, plan, data, owner.Assets, externallyManagedPrefabIds);
                    loaded.Add(id, view);
                    failures.Remove(id);
                    added = true;
                }
                catch (Exception error)
                {
                    if (instantiated && view != null) Destroy(view.gameObject);
                    if (!failures.TryGetValue(id, out string previous) || previous != error.Message)
                    {
                        failures[id] = error.Message;
                        Debug.LogWarning($"방 {row.RoomNo} 로드 대기: {error.Message}");
                        RoomLoadFailed?.Invoke(id, error.Message);
                    }
                }
                if (added) RoomLoaded?.Invoke(row, view);
            }
            // 후속 방이 아직 제작되지 않아도 이미 로드한 방은 유지합니다.
            owner.WorldReady = loaded.ContainsKey(plan.First.Id);
        }
        private void OnDestroy()
        {
            if (owner == null) return;
            owner.WorldReady = false;
            owner.Service.RoomsChanged -= ApplyRooms;
        }
    }
}
