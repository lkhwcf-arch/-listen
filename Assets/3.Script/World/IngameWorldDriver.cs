using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using EscapeGame.Data.Tables;
using UnityEngine;
using UnityEngine.SceneManagement;

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
        public event Action<RoomData, RoomSceneView> RoomLoaded;
        public event Action<int, string> RoomLoadFailed;
        private IReadOnlyDictionary<int, RoomSceneView> publishedRooms;
        public IReadOnlyDictionary<int, RoomSceneView> LoadedRooms => publishedRooms ??
            (publishedRooms = new ReadOnlyDictionary<int, RoomSceneView>(loaded));

        private void Start()
        {
            owner = FindFirstObjectByType<GameFlowBootstrapper>();
            if (owner == null || owner.Service.Data == null)
            {
                Debug.LogWarning("Join 씬에서 데이터 소스를 선택한 뒤 입장하세요.");
                SceneManager.LoadScene("Join");
                return;
            }
            foreach (var room in sceneRooms) if (room != null) room.gameObject.SetActive(false);
            data = owner.Service.Data;
            plan = new RoomLoadPlan(data, owner.Service.Role);
            owner.Service.RoomsChanged += ApplyRooms;
            ApplyRooms(owner.Service.UnlockedRoomIds);
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
                    view.Load(row, plan, data, owner.Assets);
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
