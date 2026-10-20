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
    // 에디터 미리보기의 생성물만 소유합니다. 기존 씬 모델은 빌려 쓰고 제거하지 않습니다.
    public sealed class RoomClearPreviewSession : IDisposable
    {
        private readonly GameContentAssets assets;
        private readonly RoomLoadPlan plan;
        private readonly Scene scene;
        private readonly Dictionary<int, GameObject> previews = new Dictionary<int, GameObject>();
        private GameObject ownedRoot;
        private bool disposed;
        public DeveloperRoomProgression Progression { get; }
        public string Role => plan.Role;

        public RoomClearPreviewSession(GameContentAssets assets, CsvGameData data, string role, Scene scene)
        {
            if (!Application.isEditor || !Application.isPlaying || scene.name != "Ingame")
                throw new InvalidOperationException("Ingame 씬이 활성화된 Play 모드에서 실행하세요.");
            this.assets = assets ?? throw new ArgumentNullException(nameof(assets));
            this.scene = scene;
            plan = new RoomLoadPlan(data, role);
            Progression = new DeveloperRoomProgression(plan);
        }

        public void Start() => EnsureRoom(Progression.First);
        public GameObject FindPreview(RoomData room) => previews.TryGetValue(room.Id, out var value) ? value : null;

        public bool Clear(int roomNo)
        {
            ValidateLifetime();
            var room = Progression.FindRoom(roomNo);
            if (Progression.IsCleared(room)) return false;
            var next = Progression.NextForClear(roomNo);
            // 누락된 프리팹 때문에 실패하면 클리어 상태를 확정하지 않습니다.
            if (next != null) EnsureRoom(next);
            return Progression.Complete(roomNo);
        }

        private void EnsureRoom(RoomData row)
        {
            ValidateLifetime();
            if (FindPreview(row) != null) return;
            var existing = Object.FindObjectsByType<RoomSceneView>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(view => view.gameObject.scene == scene && view.RoomNo == row.RoomNo).ToArray();
            if (existing.Length > 1) throw new InvalidOperationException("같은 방 번호의 RoomSceneView가 여러 개 있습니다: " + row.RoomNo);
            if (existing.Length == 1)
            {
                previews[row.Id] = existing[0].gameObject;
                return;
            }
            string path = plan.PrefabPath(row.RoomPrefabId);
            var prefab = assets.ResolvePrefab(path);
            if (ownedRoot == null)
            {
                ownedRoot = new GameObject("[Editor] Room Clear Preview") { hideFlags = HideFlags.DontSave };
                SceneManager.MoveGameObjectToScene(ownedRoot, scene);
            }
            // 방 외형만 생성합니다. RoomSceneView.Load와 내부 CSV 앵커 스폰은 호출하지 않습니다.
            var instance = Object.Instantiate(prefab, prefab.transform.position, prefab.transform.rotation, ownedRoot.transform);
            instance.name = $"R{row.RoomNo:00}_{Role}_Preview";
            previews[row.Id] = instance;
        }

        private void ValidateLifetime()
        {
            if (disposed) throw new ObjectDisposedException(nameof(RoomClearPreviewSession));
            if (!Application.isPlaying || !scene.IsValid() || !scene.isLoaded)
                throw new InvalidOperationException("미리보기를 종료하고 현재 Ingame 씬에서 다시 시작하세요.");
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (ownedRoot != null) Object.DestroyImmediate(ownedRoot);
            previews.Clear();
        }
    }
}
#endif
