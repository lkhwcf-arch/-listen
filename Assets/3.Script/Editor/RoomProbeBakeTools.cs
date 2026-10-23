#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using EscapeGame.Data;
using EscapeGame.Data.Csv;
using EscapeGame.Data.Tables;
using EscapeGame.Player;
using EscapeGame.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace EscapeGame.Editor
{
    // 실제 CSV 조립을 편집 모드에서 재현합니다. 임시 편집 씬은 게임의 빌드 씬 목록에 넣지 않습니다.
    [InitializeOnLoad]
    public static class RoomProbeBakeTools
    {
        private static BakeSession current;
        public static string Status => current == null ? "프로브 배치 없음" : current.Role + " 역할 / " + (Lightmapping.isRunning ? "베이크 중" : "배치 준비 완료");
        static RoomProbeBakeTools()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Cancel;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingEditMode)
                    Cancel();
            };
        }
        [MenuItem("Tools/Game/조명 프로브/A 배치 준비")]
        public static void PrepareA() => Prepare("A");
        [MenuItem("Tools/Game/조명 프로브/B 배치 준비")]
        public static void PrepareB() => Prepare("B");
        public static void Prepare(string role)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning || current != null)
                throw new InvalidOperationException("실행·베이크·기존 프로브 배치를 종료한 뒤 준비하세요.");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isDirty || string.IsNullOrEmpty(scene.path))
                    throw new InvalidOperationException("현재 열린 씬을 먼저 저장하세요. 기존 씬 작업을 임의로 저장하거나 버리지 않습니다.");
            }
            var assets = Resources.Load<GameContentAssets>("GameContent") ?? throw new InvalidOperationException("게임 콘텐츠 설정이 없습니다.");
            var store = new CsvDataStore();
            if (!store.TryRegister(assets.ReadLocalData(), assets.CreateValidator(), out var report))
                throw new InvalidOperationException("프로브 준비 전 CSV 검증 실패: " + string.Join(" / ", report.Issues.Where(issue => issue.Severity == EscapeGame.Data.CsvValidation.Contracts.CsvValidationSeverity.Error).Take(3)));
            var next = new BakeSession(role, setup);
            try
            {
                next.Prepare(assets, new CsvGameData(store));
                current = next;
            }
            catch
            {
                next.Dispose();
                throw;
            }
            Debug.Log("[방 조명] " + role + " 역할 배치 준비 완료. 현재 앵커 위치를 유지했습니다. 베이크·저장 메뉴를 실행하세요.");
        }
        [MenuItem("Tools/Game/조명 프로브/현재 배치 베이크·저장")]
        public static void BakeAndSave()
        {
            if (current == null || Lightmapping.isRunning)
                throw new InvalidOperationException("먼저 역할별 배치를 준비하세요.");
            Lightmapping.bakeCompleted += CaptureAndClose;
            try
            {
                if (!Lightmapping.BakeAsync())
                    throw new InvalidOperationException("Unity가 베이크를 시작하지 못했습니다.");
            }
            catch
            {
                Lightmapping.bakeCompleted -= CaptureAndClose;
                throw;
            }
            Debug.Log("[방 조명] 프로브 베이크 시작. 완료되면 역할별 결과를 저장하고 원래 씬으로 돌아갑니다.");
        }
        private static void CaptureAndClose()
        {
            Lightmapping.bakeCompleted -= CaptureAndClose;
            var session = current;
            current = null;
            if (session == null)
                return;
            try
            {
                session.Capture();
            }
            catch (Exception error)
            {
                Debug.LogError("[방 조명] 저장 실패: " + error.Message);
            }
            finally
            {
                session.Dispose();
            }
        }
        [MenuItem("Tools/Game/조명 프로브/임시 배치 종료")]
        public static void Cancel()
        {
            Lightmapping.bakeCompleted -= CaptureAndClose;
            var session = current;
            current = null;
            if (session == null)
                return;
            if (Lightmapping.isRunning)
                Lightmapping.Cancel();
            session.Dispose();
        }
        [MenuItem("Tools/Game/조명 프로브/저장된 결과 검사")]
        public static void CheckSaved()
        {
            var settings = Resources.Load<RoomProbeLightingSettings>("RoomProbeLighting") ?? throw new InvalidOperationException("저장된 방 프로브 설정이 없습니다.");
            foreach (string role in new[] { "A", "B" })
            {
                var probes = settings.ForRole(role);
                if (probes == null || probes.count < 4 || probes.bakedProbes.Length != probes.positions.Length)
                    throw new InvalidOperationException(role + " 역할의 프로브 베이크·저장을 완료하세요.");
                Debug.Log($"[방 조명] {role} 역할 저장 프로브 {probes.count}개 확인");
            }
        }
        private sealed class BakeSession : IDisposable
        {
            private readonly SceneSetup[] previous;
            private readonly string folder;
            private Scene scene;
            private LightingSettings lighting;
            private bool opened, disposed;
            public string Role
            {
                get;
            }
            public BakeSession(string role, SceneSetup[] previous)
            {
                Role = role;
                this.previous = previous;
                folder = "Assets/Editor/RoomProbeBake_" + Guid.NewGuid().ToString("N");
            }
            public void Prepare(GameContentAssets assets, CsvGameData data)
            {
                if (!AssetDatabase.IsValidFolder("Assets/Editor"))
                    AssetDatabase.CreateFolder("Assets", "Editor");
                AssetDatabase.CreateFolder("Assets/Editor", folder.Substring("Assets/Editor/".Length));
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                opened = true;
                var plan = new RoomLoadPlan(data, Role);
                var spawn = Resources.Load<PlayerPresentationSettings>("PlayerPresentation");
                var external = spawn == null ? Array.Empty<int>() : new[] { spawn.SpawnPrefabId };
                var positions = new HashSet<Vector3>();
                RoomSceneView previousRoom = null;
                foreach (var row in plan.OrderedRooms)
                {
                    var prefab = assets.ResolvePrefab(plan.PrefabPath(row.RoomPrefabId));
                    var instance = Object.Instantiate(prefab, prefab.transform.position, prefab.transform.rotation);
                    instance.name = $"베이크용 {row.RoomNo}번 방 {Role}";
                    var view = instance.GetComponent<RoomSceneView>() ?? instance.AddComponent<RoomSceneView>();
                    view.Configure(row.RoomNo);
                    RoomSceneAssembler.Prepare(view, row, plan, data, assets, external, previousRoom);
                    previousRoom = view;
                    var renderers = instance.GetComponentsInChildren<MeshRenderer>(true);
                    if (renderers.Length == 0)
                        throw new InvalidOperationException(row.RoomNo + "번 방에 베이크할 메시가 없습니다.");
                    Bounds bounds = renderers[0].bounds;
                    foreach (var renderer in renderers)
                    {
                        bounds.Encapsulate(renderer.bounds);
                        GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, StaticEditorFlags.ContributeGI);
                        renderer.receiveGI = ReceiveGI.LightProbes;
                    }
                    AddRoomSamples(bounds, positions);
                    foreach (var light in instance.GetComponentsInChildren<Light>(true))
                    {
                        // 앵커 자체에 남아 있는 제작용 조명과 CSV로 스폰한 조명의 이중 베이크를 막습니다.
                        if (light.GetComponentInParent<AnchorInstance>() == null)
                        {
                            light.enabled = false;
                            continue;
                        }
                        light.lightmapBakeType = LightmapBakeType.Baked;
                        float radius = Mathf.Clamp(light.range * .1f, .25f, 1);
                        for (int i = 0; i < 8; i++)
                            positions.Add(light.transform.position + new Vector3((i & 1) == 0 ? -radius : radius, (i & 2) == 0 ? -radius : radius, (i & 4) == 0 ? -radius : radius));
                    }
                }
                var group = new GameObject("방 전체 조명 프로브").AddComponent<LightProbeGroup>();
                group.probePositions = positions.ToArray();
                lighting = new LightingSettings
                {
                    name = "방 프로브 베이크 설정",
                    bakedGI = true,
                    realtimeGI = false,
                    lightmapper = LightingSettings.Lightmapper.ProgressiveCPU,
                    directSampleCount = 16,
                    indirectSampleCount = 32,
                    environmentSampleCount = 32,
                    maxBounces = 2
                };
                Lightmapping.lightingSettings = lighting;
                EditorSceneManager.SaveScene(scene, folder + "/RoomProbeBake.unity");
            }
            private static void AddRoomSamples(Bounds bounds, HashSet<Vector3> points)
            {
                int xCount = Mathf.Clamp(Mathf.CeilToInt(bounds.size.x / 3), 2, 12);
                int zCount = Mathf.Clamp(Mathf.CeilToInt(bounds.size.z / 3), 2, 12);
                for (int x = 0; x < xCount; x++)
                    for (int z = 0; z < zCount; z++)
                        for (int y = 0; y < 3; y++)
                            points.Add(new Vector3(Mathf.Lerp(bounds.min.x + .35f, bounds.max.x - .35f, x / (float)(xCount - 1)),
                                Mathf.Lerp(bounds.min.y + .35f, bounds.max.y - .35f, y / 2f),
                                Mathf.Lerp(bounds.min.z + .35f, bounds.max.z - .35f, z / (float)(zCount - 1))));
            }
            public void Capture()
            {
                var source = LightmapSettings.lightProbes;
                if (source == null || source.count < 4 || source.positions.Length != source.bakedProbes.Length)
                    throw new InvalidOperationException("유효한 베이크 프로브가 생성되지 않았습니다. Unity의 베이크 오류를 확인하세요.");
                const string configPath = "Assets/Resources/RoomProbeLighting.asset";
                string probesPath = "Assets/Resources/RoomProbes_" + Role + ".asset";
                if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                    AssetDatabase.CreateFolder("Assets", "Resources");
                var copy = Object.Instantiate(source);
                copy.name = "방 조명 프로브 " + Role;
                var saved = AssetDatabase.LoadAssetAtPath<LightProbes>(probesPath);
                if (saved == null)
                {
                    AssetDatabase.CreateAsset(copy, probesPath);
                    saved = copy;
                }
                else
                {
                    EditorUtility.CopySerialized(copy, saved);
                    Object.DestroyImmediate(copy);
                    EditorUtility.SetDirty(saved);
                }
                var settings = AssetDatabase.LoadAssetAtPath<RoomProbeLightingSettings>(configPath);
                if (settings == null)
                {
                    settings = ScriptableObject.CreateInstance<RoomProbeLightingSettings>();
                    AssetDatabase.CreateAsset(settings, configPath);
                }
                var serialized = new SerializedObject(settings);
                serialized.FindProperty(Role == "A" ? "probesA" : "probesB").objectReferenceValue = saved;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
                Debug.Log($"[방 조명] {Role} 역할 프로브 {saved.count}개 저장 완료: {probesPath}");
            }
            public void Dispose()
            {
                if (disposed)
                    return;
                disposed = true;
                if (opened)
                {
                    if (scene.IsValid() && scene.isLoaded && scene.isDirty)
                        EditorSceneManager.SaveScene(scene);
                    EditorSceneManager.RestoreSceneManagerSetup(previous);
                }
                if (lighting != null)
                    Object.DestroyImmediate(lighting);
                if (AssetDatabase.IsValidFolder(folder))
                    AssetDatabase.DeleteAsset(folder);
            }
        }
    }
}
#endif
