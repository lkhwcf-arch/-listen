#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using EscapeGame.Data;
using EscapeGame.Data.Csv;
using EscapeGame.Data.Tables;
using EscapeGame.Player;
using EscapeGame.Session.Contracts;
using EscapeGame.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace EscapeGame.Editor
{
    // 모델 대신 임시 큐브를 써서 실제 CSV 선택·Unity 생성·연결 실패·정리를 검사합니다.
    public static class RoomPreviewChecks
    {
        [MenuItem("Tools/Game/방 스폰 미리보기 자동 검사")]
        public static void PickData()
        {
            if (!Application.isPlaying) { Debug.LogWarning("Ingame 씬에서 직접 Play한 뒤 검사하세요."); return; }
            var directory = EditorUtility.OpenFolderPanel("검사할 CSV 폴더 선택", Application.dataPath + "/11.CSV", "");
            if (!string.IsNullOrEmpty(directory)) RunFromDirectory(directory);
        }
        public static int RunFromDirectory(string directory)
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Play 모드에서 실행하세요.");
            var content = Resources.Load<GameContentAssets>("GameContent");
            var source = new CsvDataSet(Directory.GetFiles(directory, "*.csv").ToDictionary(Path.GetFileNameWithoutExtension,
                file => new CsvParser().Read(File.ReadAllText(file), Path.GetFileNameWithoutExtension(file)), StringComparer.Ordinal));
            var store = new CsvDataStore();
            if (!store.TryRegister(source, content.CreateValidator(), out var report))
                throw new InvalidOperationException("검사 CSV 오류: " + string.Join("\n", report.Issues));
            var data = new CsvGameData(store);
            int checks = 0;
            foreach (string role in new[] { "A", "B" }) checks += RunRole(data, role);
            Debug.Log($"[방 스폰 미리보기] Unity 검사 {checks}개 통과. 임시 큐브 검사이며 실제 방 모델 배치는 별도 확인하세요.");
            return checks;
        }
        private static int RunRole(CsvGameData data, string role)
        {
            int checks = 0;
            void Check(bool value, string label) { if (!value) throw new InvalidOperationException(role + ": " + label); checks++; }
            var scene = SceneManager.CreateScene("RoomPreviewChecks_" + role + Guid.NewGuid().ToString("N"));
            var fixture = new GameObject("검사용 원본"); SceneManager.MoveGameObjectToScene(fixture, scene);
            fixture.SetActive(false);
            var assets = ScriptableObject.CreateInstance<GameContentAssets>();
            typeof(GameContentAssets).GetField("placementMode", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(assets, RoomPlacementMode.ConnectionPoints);
            RoomClearPreviewSession session = null;
            try
            {
                var plan = new RoomLoadPlan(data, role);
                var first = plan.First;
                var next = plan.Find(first.Next) ?? throw new InvalidOperationException("검사에는 연결된 방이 두 개 필요합니다.");
                int spawnPrefab = Resources.Load<PlayerPresentationSettings>("PlayerPresentation").SpawnPrefabId;
                var bindings = new Dictionary<string, GameObject>(StringComparer.Ordinal);
                GameObject Make(string name)
                {
                    var go = new GameObject(name); go.transform.SetParent(fixture.transform, false); return go;
                }
                GameObject Room(RoomData row, bool sceneTemplate)
                {
                    var go = Make("원본_" + row.RoomNo);
                    if (sceneTemplate) go.AddComponent<RoomSceneView>().Configure(row.RoomNo);
                    foreach (string name in plan.Anchors(row).Select(anchor => anchor.AnchorId).Distinct())
                    { var anchor = new GameObject(name); anchor.transform.SetParent(go.transform, false); anchor.transform.localPosition = new Vector3(1, 0, 1); }
                    string entryName = $"R{row.RoomNo:00}_EntryPoint";
                    var entry = go.transform.Find(entryName)?.gameObject ?? new GameObject(entryName); entry.transform.SetParent(go.transform, false);
                    entry.transform.localRotation = Quaternion.Euler(0, 180, 0);
                    string exitName = $"R{row.RoomNo:00}_DoorPoint";
                    var exit = go.transform.Find(exitName)?.gameObject ?? new GameObject(exitName); exit.transform.SetParent(go.transform, false);
                    exit.transform.localPosition = new Vector3(0, 0, 5);
                    return go;
                }
                var firstTemplate = Room(first, true);
                var nextTemplate = Room(next, false);
                bindings[plan.PrefabPath(first.RoomPrefabId)] = firstTemplate;
                bindings[plan.PrefabPath(next.RoomPrefabId)] = nextTemplate;
                foreach (var anchor in plan.Anchors(first).Concat(plan.Anchors(next)).Where(anchor => anchor.PrefabId != spawnPrefab))
                {
                    string path = plan.PrefabPath(anchor.PrefabId);
                    if (bindings.ContainsKey(path)) continue;
                    var prop = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    prop.name = "검사용_소품_" + anchor.PrefabId;
                    prop.transform.SetParent(fixture.transform, false);
                    if (new PrefabFeaturePlan(data, anchor.PrefabId).Has(FeatureScripts.Door))
                    {
                        var hinge = new GameObject(EscapeGame.Interaction.WorldDoorView.HingeName).transform;
                        hinge.SetParent(prop.transform, false); hinge.localPosition = new Vector3(-.5f, 0, 0);
                    }
                    bindings.Add(path, prop);
                }
                typeof(GameContentAssets).GetField("prefabs", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(assets,
                    bindings.Select(pair => new GameContentAssets.PrefabBinding { path = pair.Key, prefab = pair.Value }).ToArray());
                session = new RoomClearPreviewSession(assets, data, role, scene);
                session.Start();
                var preview = session.FindPreview(first);
                Check(preview != firstTemplate && preview.GetComponent<RoomSceneView>().IsLoaded, "원본 복제와 실제 게임 로더 호출");
                Check(!firstTemplate.activeSelf && firstTemplate.GetComponentsInChildren<AnchorInstance>(true).Length == 0, "원본에는 스폰하지 않음");
                int firstCount = plan.Anchors(first).Count(anchor => anchor.PrefabId != spawnPrefab);
                Check(preview.GetComponentsInChildren<AnchorInstance>(true).Length == firstCount, "역할별 활성 CSV 앵커 수 일치 및 플레이어 행 제외");
                Check(preview.GetComponentsInChildren<AnchorInstance>(true).All(instance => instance.transform.localPosition == Vector3.zero), "앵커 로컬 원점 배치");
                session.Start();
                Check(preview.GetComponentsInChildren<AnchorInstance>(true).Length == firstCount, "반복 시작으로 소품 중복 생성 방지");
                session.SpawnPlayer();
                var player = Object.FindObjectsByType<FirstPersonPlayerRig>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(item => item.gameObject.scene == scene);
                Check(player.GetComponent<PlayerMovementDriver>().IsReady && player.GetComponent<EscapeGame.Interaction.PlayerInteractionDriver>().IsReady, "공통 플레이어 조립 경로 재사용");
                checks += CheckGameplayGate(player, scene);
                var pointer = player.GetComponent<PlayerPointerDriver>();
                pointer.Controls.SetGameplayAllowed(true); pointer.Controls.SetActive(true); pointer.Controls.SetFocused(true); pointer.Controls.CapturePointer();
                var interaction = player.GetComponent<EscapeGame.Interaction.PlayerInteractionDriver>();
                var door = preview.GetComponentsInChildren<EscapeGame.Interaction.WorldDoorView>(true).Single();
                var doorTarget = door.GetComponent<EscapeGame.Interaction.WorldInteractionTarget>();
                var closedRotation = door.transform.localRotation;
                var closedPosition = door.transform.localPosition;
                var hingePoint = door.transform.Find(EscapeGame.Interaction.WorldDoorView.HingeName);
                var hingeWorld = hingePoint.position;
                Check(interaction.InteractAsync(doorTarget).GetAwaiter().GetResult() && door.IsOpen, "생성된 방은 다음 방 준비와 독립적으로 문 열기 허가");
                Check(Vector3.Distance(hingePoint.position, hingeWorld) < .0001f, "열린 뒤 경첩 월드 위치 유지");
                Check(Vector3.Distance(door.transform.localPosition, closedPosition) > .1f, "문 중심이 경첩을 따라 이동");
                Check(interaction.InteractAsync(doorTarget).GetAwaiter().GetResult() && !door.IsOpen, "클리어 전에도 테스트 문 닫기");
                Check(Vector3.Distance(door.transform.localPosition, closedPosition) < .0001f, "닫힌 문 위치 복원");
                var entryPoint = nextTemplate.transform.Find($"R{next.RoomNo:00}_EntryPoint");
                entryPoint.name = "누락_입구";
                bool rejected = false;
                try { session.Clear(first.RoomNo); } catch (InvalidOperationException) { rejected = true; }
                Check(rejected && session.FindPreview(next) == null && !session.Progression.IsCleared(first), "연결점 누락 시 소품 포함 후보 방 제거 및 클리어 미확정");
                entryPoint.name = $"R{next.RoomNo:00}_EntryPoint";
                var required = plan.Anchors(next).First(anchor => anchor.PrefabId != spawnPrefab);
                var missing = nextTemplate.transform.Find(required.AnchorId);
                missing.name = "누락_앵커";
                rejected = false;
                try { session.Clear(first.RoomNo); } catch (InvalidOperationException) { rejected = true; }
                Check(rejected && session.FindPreview(next) == null && !session.Progression.IsCleared(first), "CSV 앵커 누락도 클리어 확정을 차단");
                missing.name = required.AnchorId;
                Check(session.Clear(first.RoomNo), "수정 후 재시도 성공");
                Check(interaction.InteractAsync(doorTarget).GetAwaiter().GetResult() && door.IsOpen, "개발자 클리어 후 실제 플레이어 명령으로 문 열기");
                Check(Quaternion.Angle(closedRotation, door.transform.localRotation) > 1, "문과 충돌체를 가진 실제 Transform 회전");
                Check(interaction.InteractAsync(doorTarget).GetAwaiter().GetResult() && !door.IsOpen && Quaternion.Angle(closedRotation, door.transform.localRotation) < .01f, "같은 문 재클릭으로 닫기와 원래 회전 복원");
                var nextPreview = session.FindPreview(next);
                Check(nextPreview.GetComponent<RoomSceneView>().IsLoaded &&
                    nextPreview.GetComponentsInChildren<AnchorInstance>(true).Length == plan.Anchors(next).Count(anchor => anchor.PrefabId != spawnPrefab), "다음 방 내부 소품 생성");
                Check(session.FindPreview(first) == preview && !session.Clear(first.RoomNo), "이전 방 유지와 중복 클리어 방지");
                Check(Vector3.Distance(RoomConnectionAlignment.FindExit(preview.transform, first.RoomNo).position,
                    RoomConnectionAlignment.FindEntry(nextPreview.transform, next.RoomNo).position) < .0001f, "소품을 포함한 다음 방 전체의 연결점 정렬");
                session.Dispose(); session = null;
                Check(firstTemplate.activeSelf && preview == null && nextPreview == null, "종료 시 원본 활성 상태 복원 및 생성물 제거");
            }
            finally
            {
                session?.Dispose();
                Object.DestroyImmediate(assets);
                foreach (var root in scene.GetRootGameObjects()) Object.DestroyImmediate(root);
                SceneManager.UnloadSceneAsync(scene);
            }
            return checks;
        }
        private static int CheckGameplayGate(FirstPersonPlayerRig player, Scene scene)
        {
            int checks = 0;
            void Check(bool value, string label) { if (!value) throw new InvalidOperationException(label); checks++; }
            var connection = new PreviewConnection();
            using var gate = new SessionStartGate(connection);
            var host = new GameObject("시작 조건 검사"); SceneManager.MoveGameObjectToScene(host, scene);
            try
            {
                var pointer = player.GetComponent<PlayerPointerDriver>();
                host.AddComponent<SessionGameplayDriver>().Initialize(gate, pointer,
                    player.GetComponent<EscapeGame.Interaction.PlayerInteractionDriver>(),
                    Resources.Load<EscapeGame.Interaction.InteractionPresentationSettings>("InteractionPresentation").Font);
                var canvas = scene.GetRootGameObjects().Single(item => item.name == "SessionWaitingCanvas");
                connection.Publish(ClientSessionPhase.Waiting, 1);
                Check(!pointer.Controls.CanCapture && canvas.activeSelf, "한 명이면 공통 조작 잠금과 대기 화면 표시");
                connection.Publish(ClientSessionPhase.Loading, 2);
                Check(!pointer.Controls.CanCapture && canvas.activeSelf, "씬 준비 중에는 조작과 월드 노출 차단");
                connection.Publish(ClientSessionPhase.Playing, 2);
                Check(pointer.Controls.CanCapture && !canvas.activeSelf, "양쪽 준비 후 조작 허용과 대기 화면 해제");
                connection.Disconnect(SessionFailureCode.ConnectionFailed);
                Check(!pointer.Controls.CanCapture && canvas.activeSelf, "연결 종료 시 다시 잠금");
                gate.SetLocalMode(true);
                Check(pointer.Controls.CanCapture && !canvas.activeSelf, "로컬 테스트에는 온라인 대기를 적용하지 않음");
                Object.DestroyImmediate(host);
                Check(!canvas.activeSelf, "시작 어댑터 종료 시 대기 UI 비활성 및 정리 예약");
            }
            finally { if (host != null) Object.DestroyImmediate(host); }
            return checks;
        }
        private sealed class PreviewConnection : ISessionConnectionService
        {
            public SessionInfo State { get; private set; } = new SessionInfo(null, null, null, null, 0, false);
            public bool IsBusy => false;
            public string ServerBaseUrl => "http://preview";
            public event Action<SessionInfo> StateChanged;
            public event Action<ClientSessionPhase?, ClientSessionPhase> PhaseChanged { add { } remove { } }
            public event Action<SessionFailureCode> BackgroundFailed { add { } remove { } }
            public event Action<SessionFailureCode> Disconnected { add { } remove { } }
            public void Publish(ClientSessionPhase phase, int count)
            { State = new SessionInfo("preview", "p", "A", phase, count, phase == ClientSessionPhase.Playing); StateChanged?.Invoke(State); }
            public void Disconnect(SessionFailureCode reason)
            { State = new SessionInfo("preview", "p", "A", null, 0, false, SessionConnectionStatus.Disconnected, reason); StateChanged?.Invoke(State); }
            public System.Threading.Tasks.Task<SessionCommandResult> LeaveAsync(System.Threading.CancellationToken token) => throw new NotSupportedException();
            public System.Threading.Tasks.Task<SessionCommandResult> HeartbeatAsync(System.Threading.CancellationToken token) => throw new NotSupportedException();
            public System.Threading.Tasks.Task<SessionCommandResult> JoinAsync(string address, System.Threading.CancellationToken token) => throw new NotSupportedException();
            public System.Threading.Tasks.Task<SessionCommandResult> ReportSceneLoadedAsync(System.Threading.CancellationToken token) => throw new NotSupportedException();
            public System.Threading.Tasks.Task<SessionCommandResult> QueryAsync(System.Threading.CancellationToken token) => throw new NotSupportedException();
        }
    }
}
#endif
