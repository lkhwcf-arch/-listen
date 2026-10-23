#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EscapeGame.Data;
using EscapeGame.Data.Csv;
using EscapeGame.Data.Tables;
using EscapeGame.Interaction;
using EscapeGame.Player;
using EscapeGame.Session;
using EscapeGame.Session.Contracts;
using EscapeGame.World;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace EscapeGame.Editor
{
    // 원본 CSV와 장면을 수정하지 않는 검사 도구입니다. 로컬 B 판정과 실제 서버 통신 검사를 구분합니다.
    public static class AnswerInputChecks
    {
        private static LocalSession previewSession;
        private static GameEntryService previewEntry;
        private static CloseupPanelView previewPanel;
        private static PuzzleAnswerController previewController;
        private static PuzzleAnswerView previewView;
        private static IDisposable previewLock;

        private static CsvGameData ReadData(string directory)
        {
            var assets = Resources.Load<GameContentAssets>("GameContent");
            var source = string.IsNullOrEmpty(directory) ? assets.ReadLocalData() : new CsvDataSet(Directory.GetFiles(directory,"*.csv")
                .ToDictionary(Path.GetFileNameWithoutExtension, path => new CsvParser().Read(File.ReadAllText(path),Path.GetFileNameWithoutExtension(path)), StringComparer.Ordinal));
            var store = new CsvDataStore();
            if (!store.TryRegister(source,assets.CreateValidator(),out var report))
                throw new InvalidOperationException("검사 데이터가 유효하지 않습니다.\n"+string.Join("\n",report.Issues.Where(issue=>issue.Severity==EscapeGame.Data.CsvValidation.Contracts.CsvValidationSeverity.Error)));
            return new CsvGameData(store);
        }
        private static AnchorInteractData FixtureAnchor(CsvGameData data)
        {
            var room = new RoomLoadPlan(data,"B").First;
            var puzzle = data.Puzzles.Single(row=>row.Id==room.PuzzleId);
            var anchor = data.InteractAnchors.Single(row=>row.Id==puzzle.InputAnchor);
            var prototype = data.Prefabs.First(row=>new PrefabFeaturePlan(data,row.Id).Has(FeatureScripts.Dial));
            string Quote(string text)=>"\""+text.Replace("\"","\"\"")+"\"";
            return new AnchorInteractData(new CsvParser().Read("ID,RoomNo,PlayerType,AnchorID,PrefabID,Active\n"+
                string.Join(",",new[]{anchor.Id.ToString(),anchor.RoomNo.ToString(),anchor.PlayerType,anchor.AnchorId,prototype.Id.ToString(),"Y"}.Select(Quote))).Rows[0]);
        }
        private static GameEntryService LocalEntry(CsvGameData data, out LocalSession session)
        {
            session = new LocalSession();
            var entry = new GameEntryService(session,session,()=>data.Source,Resources.Load<GameContentAssets>("GameContent").CreateValidator(),_=>Task.CompletedTask);
            entry.EnterAsync(true,null,"B",default).GetAwaiter().GetResult();
            if(entry.Data==null){entry.Dispose();session.Dispose();throw new InvalidOperationException(entry.Message);}
            return entry;
        }
        [MenuItem("Tools/Game/정답 입력 UI 로컬 검사")]
        public static void OpenPreview() => OpenPreviewFromDirectory(null);
        [MenuItem("Tools/Game/정답 UI 검사 데이터 폴더 선택")]
        public static void PickPreviewData()
        {
            if(!Application.isPlaying){Debug.LogWarning("Play 상태에서 실행하세요.");return;}
            var directory = EditorUtility.OpenFolderPanel("검증할 CSV 폴더 선택","","");
            if(!string.IsNullOrEmpty(directory))OpenPreviewFromDirectory(directory);
        }
        public static void OpenPreviewFromDirectory(string directory)
        {
            if(!Application.isPlaying)throw new InvalidOperationException("Play 상태에서 실행하세요.");
            StopPreview();
            try
            {
                var data=ReadData(directory);var anchor=FixtureAnchor(data);
                var plan=PuzzleInputPlan.Create(data,"B",anchor,new PrefabFeaturePlan(data,anchor.PrefabId));
                previewEntry=LocalEntry(data,out previewSession);
                var pointer=Object.FindFirstObjectByType<PlayerPointerDriver>();previewLock=pointer?.Controls?.BlockForUi();
                var settings=Resources.Load<InteractionPresentationSettings>("InteractionPresentation");
                previewPanel=new CloseupPanelView(SceneManager.GetActiveScene(),settings);previewPanel.Show(plan.RoomNo);
                InteractionUiFactory.Text("LocalTestNotice",previewPanel.Preview.transform,settings.Font,
                    "개발자 로컬 검사\nB의 첫 방 입력을 독립적으로 확인합니다.\n실제 서버 통신·원본 데이터 변경은 없습니다.",500,220,0,0,24);
                previewController=new PuzzleAnswerController(plan,previewEntry,default,error=>Debug.LogException(error));
                previewView=new PuzzleAnswerView(previewPanel.ContentRoot,settings.Font,previewController);
                previewPanel.CloseRequested+=StopPreview;EditorApplication.playModeStateChanged+=OnPlayChanged;
                Debug.Log("[정답 입력 검사] 로컬 B 입력 UI를 열었습니다. 정답은 선택한 데이터의 PUZZLE_SLOT에서 확인하세요.");
            }
            catch{StopPreview();throw;}
        }
        private static void OnPlayChanged(PlayModeStateChange state){if(state==PlayModeStateChange.ExitingPlayMode)StopPreview();}
        public static void StopPreview()
        {
            EditorApplication.playModeStateChanged-=OnPlayChanged;
            if(previewPanel!=null)previewPanel.CloseRequested-=StopPreview;
            previewView?.Dispose();previewView=null;previewController?.Dispose();previewController=null;
            previewPanel?.Dispose();previewPanel=null;previewEntry?.Dispose();previewEntry=null;previewSession?.Dispose();previewSession=null;
            previewLock?.Dispose();previewLock=null;
        }
        [MenuItem("Tools/Game/정답 입력 UI 자동 검사")]
        public static void VerifyLocal() => VerifyFromDirectory(null);
        public static void VerifyFromDirectory(string directory)
        {
            if(Application.isPlaying)throw new InvalidOperationException("Play를 종료하고 검사하세요.");
            var data=ReadData(directory);var previous=SceneManager.GetActiveScene();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            int checks=0;GameEntryService entry=null;LocalSession session=null;
            void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);checks++;}
            try
            {
                entry=LocalEntry(data,out session);var tracking=new TrackingService{Handler=((IRoomAnswerService)entry).SubmitAnswerAsync};
                var anchor=FixtureAnchor(data);var targetObject=GameObject.CreatePrimitive(PrimitiveType.Cube);targetObject.transform.position=new Vector3(-2000,100,-2000);
                AnchorFeatureFactory.Compose(targetObject,anchor,data);targetObject.AddComponent<AnchorInstance>().Initialize(anchor,data);
                var player=new GameObject("TemporaryAnswerPlayer");player.SetActive(false);var camera=player.AddComponent<Camera>();camera.enabled=false;
                var controls=new PlayerControlState();var pointer=player.AddComponent<PlayerPointerDriver>();pointer.Initialize(new IdlePointer(),controls);
                var driver=player.AddComponent<PlayerInteractionDriver>();driver.Initialize(camera,pointer,2000,Resources.Load<InteractionPresentationSettings>("InteractionPresentation"),tracking,"B");player.SetActive(true);
                var target=targetObject.GetComponent<WorldInteractionTarget>();driver.Closeup.Open(target);
                var canvas=scene.GetRootGameObjects().Single(item=>item.name=="InteractionCanvas");
                Func<Button> submit=()=>canvas.GetComponentsInChildren<Button>(true).Single(button=>button.name=="Submit");
                Func<TMP_Text> status=()=>canvas.GetComponentsInChildren<TMP_Text>(true).Single(label=>label.name=="AnswerStatus");
                Check(canvas.GetComponentsInChildren<Transform>(true).Count(node=>node.name.StartsWith("Slot_"))==data.Puzzles.Single(row=>row.Id==entry.Data.RoomsB.First().PuzzleId).SlotCount,"표의 슬롯 수로 UI 자동 생성");
                submit().onClick.Invoke();Check(status().text.Contains("오답")&&tracking.Room==anchor.RoomNo,"UI 버튼에서 공통 서비스까지 false 전달");
                tracking.Handler=(room,values,token)=>Task.FromResult(RoomAnswerResult.Failed(SessionFailureCode.GameNotPlaying));
                submit().onClick.Invoke();Check(status().text.Contains("준비")&&!status().text.Contains("오답"),"요청 실패와 오답 표시 구분");
                var completion=new TaskCompletionSource<RoomAnswerResult>();tracking.Handler=(room,values,token)=>completion.Task;
                int calls=tracking.Calls;submit().onClick.Invoke();submit().onClick.Invoke();
                Check(tracking.Calls==calls+1&&!submit().interactable,"중복 클릭과 요청 중 제출 차단");
                driver.Closeup.Close();Check(tracking.Token.IsCancellationRequested,"패널 종료 시 요청 취소");completion.SetResult(RoomAnswerResult.Decision(true));
                Check(!canvas.GetComponentsInChildren<Transform>(true).Any(node=>node.name=="PuzzleAnswer"),"이전 입력 View 정리");
                tracking.Handler=((IRoomAnswerService)entry).SubmitAnswerAsync;driver.Closeup.Open(target);
                var plan=PuzzleInputPlan.Create(data,"B",anchor,new PrefabFeaturePlan(data,anchor.PrefabId));
                var answers=data.PuzzleSlots.Where(row=>row.PuzzleId==plan.PuzzleId).OrderBy(row=>row.Slot).Select(row=>row.Answer).ToArray();
                for(int i=0;i<answers.Length;i++)
                {
                    var row=canvas.GetComponentsInChildren<Transform>(true).Single(node=>node.name=="Slot_"+i);
                    int index=plan.Choices.ToList().IndexOf(answers[i]);if(index<0)throw new InvalidOperationException("검사 정답이 입력 선택지에 없습니다.");
                    var next=row.GetComponentsInChildren<Button>().Single(button=>button.name=="Next");for(int step=0;step<index;step++)next.onClick.Invoke();
                }
                submit().onClick.Invoke();Check(status().text.Contains("정답입니다")&&entry.RoomPermissions.HasPermission(plan.RoomNo),"실제 로컬 Facade 판정과 문 허가 캐시");
                Check(tracking.Answers.SequenceEqual(answers)&&!submit().interactable,"문자열 슬롯 순서 보존과 정답 후 재제출 차단");
                driver.Closeup.Close();Check(!controls.CanCapture||!controls.GameplayEnabled,"닫은 뒤 커서 재획득 대기");
                driver.Closeup.Open(target);
                var lateAnswer = new TaskCompletionSource<RoomAnswerResult>(); tracking.Handler = (room, values, token) => lateAnswer.Task;
                submit().onClick.Invoke();
                Object.DestroyImmediate(canvas);
                Check(!driver.IsReady, "Canvas 선파괴 시 상호작용 준비 해제");
                lateAnswer.SetResult(RoomAnswerResult.Decision(false));
                driver.enabled = false;
                Check(!driver.Closeup.IsOpen && tracking.Token.IsCancellationRequested, "Canvas 선파괴 후 Disable에서 UI 잠금·정답 요청 정리");
                Object.DestroyImmediate(player);
                Check(driver == null && !scene.GetRootGameObjects().Any(item => item.name == "CloseupVisualPreview"), "Canvas 선파괴 후 플레이어 Destroy에서 미리보기 정리");
                Debug.Log($"[정답 입력 검사] Unity UI·서비스 연결 {checks}개 통과. 데이터: {directory??"클라이언트 로컬"}. 실제 HTTP 검사는 별도입니다.");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene,true);entry?.Dispose();session?.Dispose();
                if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);
            }
        }
        private sealed class LocalSession : ISessionConnectionService, ISessionContentService, IDisposable
        {
            public SessionInfo State { get; } = new SessionInfo(null,null,null,null,0,false);
            public bool IsBusy => false; public string ServerBaseUrl => "";
            public event Action<SessionInfo> StateChanged { add {} remove {} }
            public event Action<ClientSessionPhase?,ClientSessionPhase> PhaseChanged { add {} remove {} }
            public event Action<SessionFailureCode> BackgroundFailed { add {} remove {} }
            public event Action<SessionFailureCode> Disconnected { add {} remove {} }
            public event Action<System.Collections.Generic.IReadOnlyList<int>> RoomsChanged { add {} remove {} }
            private static Exception NetworkError() => new InvalidOperationException("로컬 UI 검사는 네트워크를 사용하지 않습니다.");
            public Task<SessionCommandResult> JoinAsync(string address,CancellationToken token)=>throw NetworkError();
            public Task<SessionCommandResult> QueryAsync(CancellationToken token)=>throw NetworkError();
            public Task<SessionCommandResult> ReportSceneLoadedAsync(CancellationToken token)=>throw NetworkError();
            public Task<SessionCommandResult> HeartbeatAsync(CancellationToken token)=>throw NetworkError();
            public Task<SessionCommandResult> LeaveAsync(CancellationToken token)=>throw NetworkError();
            public Task<CsvDataSet> DownloadDataAsync(CancellationToken token)=>throw NetworkError();
            public Task<PuzzleSubmissionResult> SubmitPuzzleAsync(int id,string[] answers,CancellationToken token)=>throw NetworkError();
            public void Disconnect(SessionFailureCode reason) {} public void Dispose() {}
        }
        private sealed class TrackingService:IRoomAnswerService
        {
            public int Calls,Room;public string[] Answers;public CancellationToken Token;
            public Func<int,string[],CancellationToken,Task<RoomAnswerResult>> Handler;
            public Task<RoomAnswerResult> SubmitAnswerAsync(int room,string[] answers,CancellationToken token){Calls++;Room=room;Answers=answers;Token=token;return Handler(room,answers,token);}
        }
        private sealed class IdlePointer:IPlayerPointerInput
        {
            public PlayerLookCommand ReadLook()=>default;public bool CapturePressed=>false;public bool ReleasePressed=>false;
            public void SetEnabled(bool enabled){}public void SetLookEnabled(bool enabled){}public void Dispose(){}
        }
    }
}
#endif
