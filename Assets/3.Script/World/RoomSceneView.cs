using System;
using System.Collections.Generic;
using System.Linq;
using EscapeGame.Data;
using EscapeGame.Data.Tables;
using UnityEngine;

namespace EscapeGame.World
{
    public sealed class RoomSceneView : MonoBehaviour
    {
        [Serializable] public sealed class EmbeddedBinding { public string anchorId; public string objectPath; }
        [SerializeField, Min(1)] private int roomNo = 1;
        [SerializeField] private Transform anchorsRoot;
        [SerializeField] private EmbeddedBinding[] embedded = Array.Empty<EmbeddedBinding>();
        public int RoomNo => roomNo;
        public bool IsLoaded { get; private set; }
        public void Configure(int number) { roomNo = number; anchorsRoot = transform; }

        public void Load(RoomData room, RoomLoadPlan plan, CsvGameData data, GameContentAssets assets, IReadOnlyCollection<int> externallyManagedPrefabIds = null)
        {
            if (IsLoaded) return;
            if (anchorsRoot == null) anchorsRoot = transform;
            var rows = plan.Anchors(room).Where(row => externallyManagedPrefabIds == null || !externallyManagedPrefabIds.Contains(row.PrefabId)).ToArray();
            var requiredNames = new HashSet<string>(rows.Select(row => row.AnchorId), StringComparer.Ordinal);
            var anchors = anchorsRoot.GetComponentsInChildren<Transform>(true)
                .Where(item => requiredNames.Contains(item.name)).ToDictionary(item => item.name, StringComparer.Ordinal);
            var operations = new List<(AnchorData row, Transform anchor, GameObject prefab, GameObject existing)>();
            // 먼저 모든 참조를 확인합니다. 일부 프리팹만 생성된 방을 준비 완료로 보고하지 않습니다.
            foreach (var row in rows)
            {
                var binding = embedded.SingleOrDefault(item => item.anchorId == row.AnchorId);
                if (binding != null)
                {
                    var existing = string.IsNullOrEmpty(binding.objectPath) ? transform : transform.Find(binding.objectPath);
                    if (existing == null) throw new InvalidOperationException($"R{roomNo:00}: 포함된 모델을 찾을 수 없습니다: {binding.objectPath}");
                    operations.Add((row, existing, null, existing.gameObject));
                }
                else
                {
                    if (!anchors.TryGetValue(row.AnchorId, out var anchor))
                        throw new InvalidOperationException($"R{roomNo:00}: CSV {row.Id}의 앵커가 없습니다: {row.AnchorId}");
                    operations.Add((row, anchor, assets.ResolvePrefab(plan.PrefabPath(row.PrefabId)), null));
                }
            }
            var created = new List<GameObject>();
            gameObject.SetActive(false);
            try
            {
                foreach (var operation in operations)
                {
                    var instance = operation.existing;
                    if (instance == null)
                    {
                        instance = Instantiate(operation.prefab, operation.anchor, false);
                        created.Add(instance);
                        // 모델 파일에 저장된 절대 위치 대신 앵커의 위치와 회전을 사용합니다.
                        instance.transform.localPosition = Vector3.zero;
                        instance.transform.localRotation = Quaternion.identity;
                    }
                    var binding = instance.GetComponent<AnchorInstance>() ?? instance.AddComponent<AnchorInstance>();
                    binding.Initialize(operation.row, data);
                }
                IsLoaded = true;
                gameObject.SetActive(true);
            }
            catch
            {
                foreach (var instance in created) Destroy(instance);
                throw;
            }
        }
    }
}
