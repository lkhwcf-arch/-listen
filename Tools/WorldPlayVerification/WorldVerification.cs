using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using EscapeGame.Data;
using EscapeGame.Data.Tables;
using EscapeGame.Session;
using EscapeGame.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Networking;

[InitializeOnLoad]
public static class WorldVerification
{
    static WorldVerification() { EditorApplication.playModeStateChanged += OnPlay; }
    public static void Run()
    {
        try
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("GameSession"); root.SetActive(false);
            var flow = root.AddComponent<GameFlowBootstrapper>();
            var driver = root.GetComponent<SessionSceneLoadedDriver>();
            var serialized = new SerializedObject(driver);
            serialized.FindProperty("requiredComponents").arraySize = 1;
            serialized.FindProperty("requiredComponents").GetArrayElementAtIndex(0).objectReferenceValue = root.GetComponent<SessionBootstrapper>();
            serialized.ApplyModifiedPropertiesWithoutUndo();
            root.SetActive(true);
            EditorSceneManager.SaveScene(scene,"Assets/1.Scene/Join.unity");
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/1.Scene/Join.unity",true),new EditorBuildSettingsScene("Assets/1.Scene/Ingame.unity",true) };
            SessionState.SetBool("VerifyWorld",true);
            EditorApplication.isPlaying = true;
        }
        catch(Exception error) { Fail(error); }
    }
    private static async void OnPlay(PlayModeStateChange state)
    {
        if(state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("VerifyWorld",false)) return;
        SessionState.SetBool("VerifyWorld",false);
        try
        {
            int checks = 0;
            void Check(bool ok,string message) { if(!ok)throw new Exception(message); checks++; Debug.Log("VERIFIED: "+message); }
            var owner = UnityEngine.Object.FindFirstObjectByType<GameFlowBootstrapper>();
            Check(owner != null && owner.Service != null,"persistent composition initialized");
            await owner.Service.EnterAsync(true,null,"A",default);
            await WaitReady(owner);
            var world = UnityEngine.Object.FindFirstObjectByType<IngameWorldDriver>();
            Check(world.LoadedRooms.Count == 1,"A initial room loaded");
            var roomA = world.LoadedRooms[1001];
            Check(roomA.GetComponentsInChildren<AnchorInstance>().Length == 8,"A R01 has exactly 8 CSV bindings");
            Check(roomA.transform.Find("R01_모델_Anchor/R01_FloorCard_Anchor").childCount == 1,"A floor card spawned at authored anchor");
            Check(roomA.transform.Find("R01_모델_Anchor/R01_Dial_table_Anchor").childCount == 0,"A does not spawn B dial table");
            Check(owner.GetComponent<SessionBootstrapper>().State.IsJoined == false,"local mode has no server participant");

            await owner.Service.EnterAsync(true,null,"B",default);
            await WaitReady(owner);
            world = UnityEngine.Object.FindFirstObjectByType<IngameWorldDriver>();
            var roomB = world.LoadedRooms[1002];
            Check(roomB.GetComponentsInChildren<AnchorInstance>().Length == 14,"B R01 has exactly 14 CSV bindings");
            Check(roomB.transform.Find("R01_모델_Anchor/R01_Dial_table_Anchor").childCount == 1,"B dial table spawned at authored anchor");
            Check(roomB.transform.Find("R01_모델_Anchor/PZ_R1_B_Dial_0_Anchor").childCount == 1,"B dial spawned at authored anchor");
            foreach(var binding in roomB.GetComponentsInChildren<AnchorInstance>())
                Check(object.ReferenceEquals(binding.Data,owner.Service.Data),"spawn receives registered data snapshot "+binding.Definition.Id);
            var apply = typeof(IngameWorldDriver).GetMethod("ApplyRooms",BindingFlags.Instance|BindingFlags.NonPublic);
            var before = roomB.GetComponentsInChildren<AnchorInstance>().Select(item=>item.GetInstanceID()).OrderBy(id=>id).ToArray();
            apply.Invoke(world,new object[]{owner.Service.UnlockedRoomIds});
            Check(before.SequenceEqual(roomB.GetComponentsInChildren<AnchorInstance>().Select(item=>item.GetInstanceID()).OrderBy(id=>id)),"repeated snapshot does not duplicate room or objects");
            var answers = owner.Service.Data.PuzzleSlots.Where(row=>row.PuzzleId==5001).OrderBy(row=>row.Slot).Select(row=>row.Answer).ToArray();
            await owner.Service.SubmitPuzzleAsync(5001,answers,default);
            Check(world.LoadedRooms.Count==1 && roomB!=null && roomB.IsLoaded,"unfinished R02 retains R01 and readiness");

            // 실제 콘텐츠를 변경하지 않고 미래 방/프리팹을 메모리에 준비하여 공통 로더를 확인합니다.
            var plan = new RoomLoadPlan(owner.Service.Data,"B");
            var row2 = plan.Find(1004);
            var template = new GameObject("R02_Surface"); template.SetActive(false);
            foreach(var anchor in plan.Anchors(row2)) { var node=new GameObject(anchor.AnchorId);node.transform.SetParent(template.transform,false); }
            var dummy = new GameObject("FutureObject"); dummy.SetActive(true);
            var field = typeof(GameContentAssets).GetField("prefabs",BindingFlags.Instance|BindingFlags.NonPublic);
            var bindings = ((GameContentAssets.PrefabBinding[])field.GetValue(owner.Assets)).ToList();
            foreach(int prefabId in plan.Anchors(row2).Select(anchor=>anchor.PrefabId).Distinct())
                bindings.Add(new GameContentAssets.PrefabBinding{path=plan.PrefabPath(prefabId),prefab=dummy});
            bindings.Add(new GameContentAssets.PrefabBinding{path=plan.PrefabPath(row2.RoomPrefabId),prefab=template});
            field.SetValue(owner.Assets,bindings.ToArray());
            apply.Invoke(world,new object[]{owner.Service.UnlockedRoomIds});
            Check(world.LoadedRooms.Count==2 && world.LoadedRooms[1002]==roomB,"future room loads additively from table without replacing R01");
            Check(world.LoadedRooms[1004].GetComponentsInChildren<AnchorInstance>().Length==plan.Anchors(row2).Count,"future room uses same anchor injection");
            if (Environment.GetEnvironmentVariable("WORLD_VERIFICATION_SERVER") == "1")
            {
                await owner.Service.EnterAsync(false,"http://127.0.0.1:3099",null,default);
                await WaitReady(owner);
                Check(!owner.Service.IsLocal && owner.Service.Role=="A","Unity downloads and injects server CSV after authenticated join");
                Check(owner.Service.Data.Source.Documents.Count==23,"Unity JSON/CSV roundtrip registers 23 server tables");
                var second=JsonUtility.FromJson<Join>(await Send("/sessions/join",null,""));
                await Send("/sessions/"+second.sessionId+"/ready",second.participantToken,"");
                var deadline=DateTime.UtcNow.AddSeconds(10);
                while(owner.GetComponent<SessionBootstrapper>().State.Phase!=EscapeGame.Session.Contracts.ClientSessionPhase.Playing && DateTime.UtcNow<deadline)await Task.Yield();
                Check(owner.GetComponent<SessionBootstrapper>().State.Phase==EscapeGame.Session.Contracts.ClientSessionPhase.Playing,"ready follows world load and both clients reach Playing");
                Check((await owner.Service.SubmitPuzzleAsync(5001,answers,default)).Outcome == EscapeGame.Session.Contracts.PuzzleSubmissionOutcome.Rejected,"server rejects clue role without treating it as incorrect");
                await Send("/sessions/"+second.sessionId+"/puzzles/5001/submit",second.participantToken,JsonUtility.ToJson(new Answers{answers=answers}));
                deadline=DateTime.UtcNow.AddSeconds(10);
                while(!owner.Service.UnlockedRoomIds.Contains(1003) && DateTime.UtcNow<deadline)await Task.Yield();
                Check(owner.Service.UnlockedRoomIds.Contains(1003),"solver result reaches other Unity client through polling event");
                world=UnityEngine.Object.FindFirstObjectByType<IngameWorldDriver>();
                Check(world.LoadedRooms.ContainsKey(1001) && owner.WorldReady,"online unlock retains authored R01 while R02 is unfinished");
            }
            File.WriteAllText("verification.txt",checks+" Unity Play checks passed.");
            EditorApplication.Exit(0);
        }
        catch(Exception error) { Fail(error); }
    }
    private static async Task WaitReady(GameFlowBootstrapper owner)
    {
        var deadline=DateTime.UtcNow.AddSeconds(20);
        while(!owner.WorldReady && DateTime.UtcNow<deadline)await Task.Yield();
        if(!owner.WorldReady)throw new Exception("World did not become ready: "+owner.Service.Message);
    }
    private static void Fail(Exception error) { Debug.LogException(error);File.WriteAllText("verification.txt",error.ToString());EditorApplication.Exit(1); }
    [Serializable] private class Join { public string sessionId;public string participantToken; }
    [Serializable] private class Answers { public string[] answers; }
    private static async Task<string> Send(string route,string token,string body)
    {
        using var request=new UnityWebRequest("http://127.0.0.1:3099"+route,"POST");
        request.downloadHandler=new DownloadHandlerBuffer();
        request.timeout=10;
        if(token!=null)request.SetRequestHeader("x-participant-token",token);
        if(body.Length>0){request.uploadHandler=new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(body));request.SetRequestHeader("Content-Type","application/json");}
        var operation=request.SendWebRequest();while(!operation.isDone)await Task.Yield();
        if(request.responseCode<200 || request.responseCode>=300)throw new Exception("HTTP verification request failed "+request.responseCode+" "+route);
        return request.downloadHandler.text;
    }
}
