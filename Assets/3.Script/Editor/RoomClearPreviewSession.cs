#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using EscapeGame.Data;
using EscapeGame.Data.Tables;
using EscapeGame.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace EscapeGame.Editor
{
    // 원본 씬의 방은 숨겨 보존하고, CSV를 주입한 복제본과 플레이어만 소유합니다.
    public sealed class RoomClearPreviewSession : IDisposable
    {
        private readonly GameContentAssets assets;
        private readonly RoomLoadPlan plan;
        private readonly Scene scene;
        private readonly Dictionary<int, GameObject> previews = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, (GameObject source, bool active)> hiddenTemplates = new Dictionary<int, (GameObject, bool)>();
        private readonly int[] externallyManagedPrefabIds;
        private readonly CsvGameData data;
        private readonly DeveloperRoomDoorService doors;
        private EscapeGame.Player.FirstPersonPlayerRig player;
        private GameObject ownedRoot;
        private bool disposed;
        public DeveloperRoomProgression Progression
        {
            get;
        }
        public string Role => plan.Role;

        public RoomClearPreviewSession(GameContentAssets assets, CsvGameData data, string role, Scene scene)
        {
            if (!Application.isEditor || !Application.isPlaying || SceneManager.GetActiveScene().name != "Ingame" || !scene.IsValid() || !scene.isLoaded)
                throw new InvalidOperationException("Ingame 씬이 활성화된 Play 모드에서 실행하세요.");
            this.assets = assets ?? throw new ArgumentNullException(nameof(assets));
            this.scene = scene;
            this.data = data;
            plan = new RoomLoadPlan(data, role);
            Progression = new DeveloperRoomProgression(plan);
            doors = new DeveloperRoomDoorService(Progression);
            var settings = Resources.Load<EscapeGame.Player.PlayerPresentationSettings>("PlayerPresentation")
                ?? throw new InvalidOperationException("PlayerPresentation 에셋이 필요합니다.");
            externallyManagedPrefabIds = new[] { settings.SpawnPrefabId };
        }

        public void Start()
        {
            var flow = Object.FindFirstObjectByType<GameFlowBootstrapper>();
            if (flow != null && flow.Service != null && flow.Service.Data != null)
                throw new InvalidOperationException("실제 입장 세션과 미리보기를 함께 실행할 수 없습니다. Play를 종료하고 Ingame 씬에서 직접 Play하세요.");
            EnsureRoom(Progression.First);
        }
        public bool HasPlayer => player != null;
        public void SpawnPlayer()
        {
            ValidateLifetime();
            if (player != null)
                return;
            var room = FindPreview(Progression.First);
            if (room == null)
                throw new InvalidOperationException("첫 방 미리보기가 없습니다.");
            var host = new GameObject("미리보기 플레이어 생성기");
            SceneManager.MoveGameObjectToScene(host, scene);
            host.transform.SetParent(ownedRoot.transform, false);
            try
            {
                var driver = host.AddComponent<EscapeGame.Player.PlayerSpawnDriver>();
                driver.Initialize(data, Role, room.GetComponent<RoomSceneView>(), doors: doors);
                player = driver.Player;
                foreach (var preview in previews.Values)
                {
                    var view = preview.GetComponent<RoomSceneView>();
                    driver.Feedback?.RegisterRoom(view);
                    driver.Lighting?.RegisterRoom(view);
                }
            }
            catch
            {
                Object.DestroyImmediate(host);
                throw;
            }
        }
        public GameObject FindPreview(RoomData room) => previews.TryGetValue(room.Id, out var value) ? value : null;

        public bool Clear(int roomNo)
        {
            ValidateLifetime();
            var room = Progression.FindRoom(roomNo);
            if (Progression.IsCleared(room))
                return false;
            var next = Progression.NextForClear(roomNo);
            // 누락된 프리팹 때문에 실패하면 클리어 상태를 확정하지 않습니다.
            if (next != null)
                ConnectNext(room, next);
            return Progression.Complete(roomNo);
        }

        private void ConnectNext(RoomData room, RoomData next)
        {
            if (FindPreview(room) == null)
                throw new InvalidOperationException("현재 방 미리보기가 없습니다.");
            EnsureRoom(next); // 실제 게임과 같은 RoomSceneAssembler에서 배치·조립합니다.
        }

        private GameObject EnsureRoom(RoomData row)
        {
            ValidateLifetime();
            if (FindPreview(row) != null)
                return FindPreview(row);
            var existing = Object.FindObjectsByType<RoomSceneView>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(view => view.gameObject.scene == scene && view.RoomNo == row.RoomNo &&
                    (ownedRoot == null || !view.transform.IsChildOf(ownedRoot.transform))).ToArray();
            if (existing.Length > 1)
                throw new InvalidOperationException("같은 방 번호의 RoomSceneView가 여러 개 있습니다: " + row.RoomNo);
            if (existing.Length == 1 && existing[0].IsLoaded)
                throw new InvalidOperationException("게임 로더가 사용 중인 방입니다. Ingame 씬에서 직접 Play하고 미리보기를 시작하세요.");
            if (ownedRoot == null)
            {
                ownedRoot = new GameObject("[Editor] Room Clear Preview") { hideFlags = HideFlags.DontSave };
                SceneManager.MoveGameObjectToScene(ownedRoot, scene);
            }
            GameObject template = existing.Length == 1 ? existing[0].gameObject : assets.ResolvePrefab(plan.PrefabPath(row.RoomPrefabId));
            GameObject instance = null;
            if (existing.Length == 1)
            {
                hiddenTemplates.Add(row.Id, (template, template.activeSelf));
                template.SetActive(false);
            }
            try
            {
                instance = Object.Instantiate(template, template.transform.position, template.transform.rotation, ownedRoot.transform);
                instance.name = $"R{row.RoomNo:00}_{Role}_Preview";
                var view = instance.GetComponent<RoomSceneView>();
                if (view == null)
                    view = instance.AddComponent<RoomSceneView>();
                if (existing.Length == 0)
                    view.Configure(row.RoomNo);
                // 실제 게임 로더를 재사용하여 역할별 CSV 선택·앵커 배치·기능 주입을 동일하게 처리합니다.
                var previousRow = plan.Rooms.FirstOrDefault(candidate => candidate.Next == row.Id);
                var previous = previousRow == null ? null : FindPreview(previousRow)?.GetComponent<RoomSceneView>();
                RoomSceneAssembler.Prepare(view, row, plan, data, assets, externallyManagedPrefabIds, previous);
                previews.Add(row.Id, instance);
                if (player != null)
                    player.GetComponent<EscapeGame.Audio.GameFeedbackDriver>()?.RegisterRoom(view);
                if (player != null)
                    player.GetComponent<RoomProbeLightingDriver>()?.RegisterRoom(view);
                return instance;
            }
            catch
            {
                if (instance != null)
                    Object.DestroyImmediate(instance);
                RestoreTemplate(row.Id);
                throw;
            }
        }
        private void RestoreTemplate(int id)
        {
            if (!hiddenTemplates.TryGetValue(id, out var original))
                return;
            if (original.source != null)
                original.source.SetActive(original.active);
            hiddenTemplates.Remove(id);
        }
        private void ReleaseRoom(int id)
        {
            if (previews.TryGetValue(id, out var instance) && instance != null)
                Object.DestroyImmediate(instance);
            previews.Remove(id);
            RestoreTemplate(id);
        }

        private void ValidateLifetime()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(RoomClearPreviewSession));
            if (!Application.isPlaying || !scene.IsValid() || !scene.isLoaded)
                throw new InvalidOperationException("미리보기를 종료하고 현재 Ingame 씬에서 다시 시작하세요.");
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            doors.Dispose();
            if (player != null)
            {
                player.gameObject.SetActive(false);
                Object.DestroyImmediate(player.gameObject);
                player = null;
            }
            if (ownedRoot != null)
                Object.DestroyImmediate(ownedRoot);
            foreach (int id in hiddenTemplates.Keys.ToArray())
                RestoreTemplate(id);
            previews.Clear();
        }
    }
}
#endif
