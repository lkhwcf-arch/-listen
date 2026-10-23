using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using EscapeGame.Data;
using EscapeGame.Data.Tables;
using EscapeGame.Session;
using EscapeGame.World;
using EscapeGame.Protocol.V1;
using Google.Protobuf;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Networking;

[InitializeOnLoad]
public static class WorldVerification
{
    static WorldVerification()
    {
        EditorApplication.playModeStateChanged += OnPlay;
    }
    public static void Run()
    {
        try
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("GameSession");
            root.SetActive(false);
            var flow = root.AddComponent<GameFlowBootstrapper>();
            var driver = root.GetComponent<SessionSceneLoadedDriver>();
            var serialized = new SerializedObject(driver);
            serialized.FindProperty("requiredComponents").arraySize = 1;
            serialized.FindProperty("requiredComponents").GetArrayElementAtIndex(0).objectReferenceValue = root.GetComponent<SessionBootstrapper>();
            serialized.ApplyModifiedPropertiesWithoutUndo();
            root.SetActive(true);
            EditorSceneManager.SaveScene(scene, "Assets/1.Scene/Join.unity");
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/1.Scene/Join.unity", true), new EditorBuildSettingsScene("Assets/1.Scene/Ingame.unity", true) };
            SessionState.SetBool("VerifyWorld", true);
            EditorApplication.isPlaying = true;
        }
        catch (Exception error)
        {
            Fail(error);
        }
    }
    private static async void OnPlay(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("VerifyWorld", false))
            return;
        SessionState.SetBool("VerifyWorld", false);
        try
        {
            int checks = 0;
            void Check(bool ok, string message)
            {
                if (!ok)
                    throw new Exception(message);
                checks++;
                Debug.Log("VERIFIED: " + message);
            }
            var owner = UnityEngine.Object.FindFirstObjectByType<GameFlowBootstrapper>();
            Check(owner != null && owner.Service != null, "persistent composition initialized");
            await owner.Service.EnterAsync(true, null, "A", default);
            await WaitReady(owner);
            var world = UnityEngine.Object.FindFirstObjectByType<IngameWorldDriver>();
            Check(world.LoadedRooms.Count == 1, "A initial room loaded");
            var roomA = world.LoadedRooms[1001];
            var settings = Resources.Load<EscapeGame.Player.PlayerPresentationSettings>("PlayerPresentation");
            var planA = new RoomLoadPlan(owner.Service.Data, "A");
            var expectedA = planA.Anchors(planA.First).Where(row => row.PrefabId != settings.SpawnPrefabId).Select(row => row.Id).OrderBy(id => id);
            Check(roomA.GetComponentsInChildren<AnchorInstance>(true).Select(item => item.Definition.Id).OrderBy(id => id).SequenceEqual(expectedA), "A R01 binds every selected non-player CSV row exactly once");
            Check(owner.PlayerReady && owner.IsGameplayReady, "room readiness includes initialized local player");
            Check(roomA.transform.Find("Room_01_Final") != null, "authored prefab root resolves");
            Check(owner.GetComponent<SessionBootstrapper>().State.IsJoined == false, "local mode has no server participant");

            await owner.Service.EnterAsync(true, null, "B", default);
            // 현재 B 전용 콘텐츠가 미완성이면 거짓 준비 완료 대신 명시적인 입장 실패를 검사합니다.
            if (owner.Service.Data == null)
            {
                Check(!owner.IsGameplayReady && !owner.PlayerReady, "missing B content cannot report gameplay readiness");
                Check(owner.Service.Message.Contains("R01_LEDPanelPoint"), "unfinished B content reports the missing authored anchor");
                Debug.LogWarning("B 역할 정상 플레이 검증 보류: " + owner.Service.Message);
                File.WriteAllText("verification.txt", checks + " Unity Play checks passed; INCOMPLETE: B gameplay and optional online scenario blocked by missing authored content.");
                EditorApplication.Exit(2);
                return;
            }
            await WaitReady(owner);
            world = UnityEngine.Object.FindFirstObjectByType<IngameWorldDriver>();
            var roomB = world.LoadedRooms[1002];
            var planB = new RoomLoadPlan(owner.Service.Data, "B");
            var expectedB = planB.Anchors(planB.First).Where(row => row.PrefabId != settings.SpawnPrefabId).Select(row => row.Id).OrderBy(id => id);
            Check(roomB.GetComponentsInChildren<AnchorInstance>(true).Select(item => item.Definition.Id).OrderBy(id => id).SequenceEqual(expectedB), "B R01 bindings follow the current CSV");
            Check(!roomB.GetComponentsInChildren<AnchorInstance>(true).Any(item => item.Definition.PlayerType == "A"), "B does not spawn A-only CSV props");
            foreach (var binding in roomB.GetComponentsInChildren<AnchorInstance>())
                Check(object.ReferenceEquals(binding.Data, owner.Service.Data), "spawn receives registered data snapshot " + binding.Definition.Id);
            var apply = typeof(IngameWorldDriver).GetMethod("ApplyRooms", BindingFlags.Instance | BindingFlags.NonPublic);
            var before = roomB.GetComponentsInChildren<AnchorInstance>().Select(item => item.GetInstanceID()).OrderBy(id => id).ToArray();
            apply.Invoke(world, new object[] { owner.Service.UnlockedRoomIds });
            Check(before.SequenceEqual(roomB.GetComponentsInChildren<AnchorInstance>().Select(item => item.GetInstanceID()).OrderBy(id => id)), "repeated snapshot does not duplicate room or objects");
            var answers = owner.Service.Data.PuzzleSlots.Where(row => row.PuzzleId == 5001).OrderBy(row => row.Slot).Select(row => row.Answer).ToArray();
            await owner.Service.SubmitPuzzleAsync(5001, answers, default);
            Check(world.LoadedRooms.Count == 1 && roomB != null && roomB.IsLoaded, "unfinished R02 retains R01 and readiness");

            // 실제 콘텐츠를 변경하지 않고 미래 방/프리팹을 메모리에 준비하여 공통 로더를 확인합니다.
            var plan = new RoomLoadPlan(owner.Service.Data, "B");
            var row2 = plan.Find(1004);
            var template = new GameObject("R02_Surface");
            template.SetActive(false);
            foreach (var anchor in plan.Anchors(row2))
            {
                var node = new GameObject(anchor.AnchorId);
                node.transform.SetParent(template.transform, false);
            }
            var dummy = new GameObject("FutureObject");
            dummy.SetActive(true);
            var field = typeof(GameContentAssets).GetField("prefabs", BindingFlags.Instance | BindingFlags.NonPublic);
            var originalBindings = (GameContentAssets.PrefabBinding[])field.GetValue(owner.Assets);
            string Normalize(string path) => path.Replace('\\', '/').Replace(".prefab", "");
            var overriddenPaths = plan.Anchors(row2).Select(anchor => Normalize(plan.PrefabPath(anchor.PrefabId)))
                .Append(Normalize(plan.PrefabPath(row2.RoomPrefabId))).ToHashSet(StringComparer.Ordinal);
            var bindings = originalBindings.Where(item => !overriddenPaths.Contains(Normalize(item.path))).ToList();
            foreach (int prefabId in plan.Anchors(row2).Select(anchor => anchor.PrefabId).Distinct())
                bindings.Add(new GameContentAssets.PrefabBinding { path = plan.PrefabPath(prefabId), prefab = dummy });
            bindings.Add(new GameContentAssets.PrefabBinding { path = plan.PrefabPath(row2.RoomPrefabId), prefab = template });
            try
            {
                field.SetValue(owner.Assets, bindings.ToArray());
                apply.Invoke(world, new object[] { owner.Service.UnlockedRoomIds });
                Check(world.LoadedRooms.Count == 2 && world.LoadedRooms[1002] == roomB, "future room loads additively from table without replacing R01");
                Check(world.LoadedRooms[1004].GetComponentsInChildren<AnchorInstance>().Length == plan.Anchors(row2).Count(anchor => anchor.PrefabId != settings.SpawnPrefabId), "future room uses same anchor injection");
            }
            finally
            {
                field.SetValue(owner.Assets, originalBindings);
                UnityEngine.Object.Destroy(template);
                UnityEngine.Object.Destroy(dummy);
            }
            if (Environment.GetEnvironmentVariable("WORLD_VERIFICATION_SERVER") == "1")
            {
                await owner.Service.EnterAsync(false, "http://127.0.0.1:3099", null, default);
                await WaitReady(owner);
                Check(!owner.Service.IsLocal && owner.Service.Role == "A", "Unity downloads and injects server CSV after authenticated join");
                Check(owner.Service.Data.Source.Documents.Count == 23, "Unity Protobuf/CSV roundtrip registers 23 server tables");
                var second = (await Send("/sessions/join", null, null)).Joined;
                await Send("/sessions/" + second.SessionId + "/ready", second.ParticipantToken, null);
                var clock = new EscapeGame.Timing.StopwatchMillisecondClock();
                long deadline = clock.ElapsedMilliseconds + 10000;
                while (owner.GetComponent<SessionBootstrapper>().State.Phase != EscapeGame.Session.Contracts.ClientSessionPhase.Playing && clock.ElapsedMilliseconds < deadline)
                    await Task.Yield();
                Check(owner.GetComponent<SessionBootstrapper>().State.Phase == EscapeGame.Session.Contracts.ClientSessionPhase.Playing, "ready follows world load and both clients reach Playing");
                Check((await owner.Service.SubmitPuzzleAsync(5001, answers, default)).Outcome == EscapeGame.Session.Contracts.PuzzleSubmissionOutcome.Rejected, "server rejects clue role without treating it as incorrect");
                var command = new RoomCommandRequest { RoomNo = 1 };
                command.Answers.Add(answers);
                await Send("/sessions/" + second.SessionId + "/rooms/answer", second.ParticipantToken, command);
                if (!await owner.Service.CheckRoomDoorAsync(1, default))
                    throw new Exception("Solved room door was denied.");
                deadline = clock.ElapsedMilliseconds + 10000;
                while (!owner.Service.UnlockedRoomIds.Contains(1003) && clock.ElapsedMilliseconds < deadline)
                    await Task.Yield();
                Check(owner.Service.UnlockedRoomIds.Contains(1003), "clue door check grants next room through state event");
                world = UnityEngine.Object.FindFirstObjectByType<IngameWorldDriver>();
                Check(world.LoadedRooms.ContainsKey(1001) && owner.WorldReady, "online unlock retains authored R01 while R02 is unfinished");
            }
            File.WriteAllText("verification.txt", checks + " Unity Play checks passed.");
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            Fail(error);
        }
    }
    private static async Task WaitReady(GameFlowBootstrapper owner)
    {
        var clock = new EscapeGame.Timing.StopwatchMillisecondClock();
        long deadline = clock.ElapsedMilliseconds + 20000;
        while (!owner.IsGameplayReady && clock.ElapsedMilliseconds < deadline)
            await Task.Yield();
        if (!owner.IsGameplayReady)
            throw new Exception("Room/player did not become ready: " + owner.Service.Message);
    }
    private static void Fail(Exception error)
    {
        Debug.LogException(error);
        File.WriteAllText("verification.txt", error.ToString());
        EditorApplication.Exit(1);
    }
    private static async Task<GameApiResponse> Send(string route, string token, IMessage body)
    {
        using var request = new UnityWebRequest("http://127.0.0.1:3099" + route, "POST");
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Accept", "application/protobuf");
        request.timeout = 10;
        if (token != null)
            request.SetRequestHeader("x-participant-token", token);
        if (body != null)
        {
            request.uploadHandler = new UploadHandlerRaw(body.ToByteArray());
            request.SetRequestHeader("Content-Type", "application/protobuf");
        }
        var operation = request.SendWebRequest();
        while (!operation.isDone)
            await Task.Yield();
        if (request.responseCode < 200 || request.responseCode >= 300)
            throw new Exception("HTTP verification request failed " + request.responseCode + " " + route);
        return GameApiResponse.Parser.ParseFrom(request.downloadHandler.data);
    }
}
