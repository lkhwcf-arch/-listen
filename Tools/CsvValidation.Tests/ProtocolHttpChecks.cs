using System.Net.Http.Headers;
using EscapeGame.Data.CsvValidation.Runtime;
using EscapeGame.Data.CsvValidation.Schema;
using EscapeGame.Networking;
using EscapeGame.Protocol.V1;
using EscapeGame.Session;
using EscapeGame.Session.Contracts;
using EscapeGame.World;
using EscapeGame.Interaction;
using Google.Protobuf;

static class ProtocolHttpChecks
{
    internal static async Task Run(string address,string root)
    {
        int checks=0;
        void Check(bool ok,string name){if(!ok)throw new Exception("실패: "+name);checks++;}
        var validator=new CsvValidator(CsvValidationCatalog.Read(File.ReadAllText(Path.Combine(root,"Assets/3.Script/Data/CsvValidation/Configuration/CsvValidationRules.xml"))));
        using var factoryA=new HttpFactory();using var factoryB=new HttpFactory();
        void Diagnostic(Exception error){if(error is not SessionResultException)Console.Error.WriteLine(error);}
        using var sessionA=new SessionClient(factoryA,default,Diagnostic);using var sessionB=new SessionClient(factoryB,default,Diagnostic);
        using var entryA=new GameEntryService(sessionA,sessionA,()=>throw new Exception("온라인 검사에서 로컬 대체 금지"),validator,_=>Task.CompletedTask);
        using var entryB=new GameEntryService(sessionB,sessionB,()=>throw new Exception("온라인 검사에서 로컬 대체 금지"),validator,_=>Task.CompletedTask);
        using var entryTimeout = new CancellationTokenSource(10000);
        var enteringA = entryA.EnterAsync(false,address,null,entryTimeout.Token);
        while (!sessionA.State.IsJoined) await Task.Delay(10, entryTimeout.Token);
        await entryB.EnterAsync(false,address,null,entryTimeout.Token);
        await sessionA.QueryAsync(entryTimeout.Token);
        await enteringA;
        Check(entryA.Data!=null&&entryB.Data!=null,"두 클라이언트 입장과 바이너리 CSV 수신");
        Check(entryA.Role=="A"&&entryB.Role=="B","생성 열거형 역할 변환");
        Check(entryA.Data.Source.Documents.Count==entryA.Data.Registry.Count(row=>row.Active),"CSV 테이블 Protobuf HTTP 왕복");
        var inputPlan=PuzzleAnswerChecks.Plan(entryB.Data,5001,"B",FeatureScripts.Dial);
        using var input=new PuzzleAnswerController(inputPlan,(IRoomAnswerService)entryB);
        await input.SubmitAsync();
        Check(input.Phase==AnswerInputPhase.Failed&&input.Failure==SessionFailureCode.GameNotPlaying,"입력 Controller에서 실제 HTTP 준비 전 오류 구분");
        await sessionA.ReportSceneLoadedAsync(default);await sessionB.ReportSceneLoadedAsync(default);await sessionA.QueryAsync(default);
        Check(sessionA.State.Phase==ClientSessionPhase.Playing&&sessionB.State.Phase==ClientSessionPhase.Playing,"준비 보고·조회 후 양쪽 Playing 반영");
        Check(!await entryA.CheckRoomDoorAsync(1,default),"미해결 문은 HTTP에서 false");
        Check(!await entryB.SubmitRoomAnswerAsync(1,new[]{"wrong"},default),"오답은 HTTP에서 false");
        await input.SubmitAsync();Check(input.Phase==AnswerInputPhase.Incorrect&&input.Failure==SessionFailureCode.None,"입력 Controller의 오답이 Protobuf false로 복귀");
        var answers=entryB.Data.PuzzleSlots.Where(s=>s.PuzzleId==5001).OrderBy(s=>s.Slot).Select(s=>s.Answer).ToArray();
        bool rejected=false;try{await entryA.SubmitRoomAnswerAsync(1,answers,default);}catch(SessionResultException error){rejected=error.Code==SessionFailureCode.SubmissionNotAllowed;}
        Check(rejected,"단서 역할의 요청은 오답이 아닌 실패");
        for(int i=0;i<answers.Length;i++){input.SelectSlot(i);input.Choose(answers[i]);}
        await input.SubmitAsync();
        Check(input.Phase==AnswerInputPhase.Correct,"입력 Controller → 공개 서비스 → 실제 HTTP Protobuf 정답 true");
        Check(input.Values.SequenceEqual(answers)&&!input.CanSubmit,"요청 슬롯 순서 보존과 정답 후 재제출 차단");
        Check(await entryB.SubmitRoomAnswerAsync(1,answers,default),"정답 역할 제출은 true");
        Check(entryB.RoomPermissions.HasPermission(1),"정답 응답과 함께 입력 측 문 허가 수신");
        Check(entryB.UnlockedRoomIds.Contains(1004)&&!entryB.UnlockedRoomIds.Contains(1003),"정답 입력 측의 다음 방만 우선 해금");
        Check(await entryB.CheckRoomDoorAsync(1,default)&&await entryB.CheckRoomDoorAsync(1,default)&&factoryB.DoorRequests==0,"허가된 입력 측은 반복 문 요청 없음");
        Check(await entryA.CheckRoomDoorAsync(1,default),"단서 측도 문 확인 후 허가 수신");
        Check(entryA.UnlockedRoomIds.Contains(1003),"단서 측 문 확인 후 다음 방 해금");
        int doorRequests=factoryA.DoorRequests;
        Check(await entryA.CheckRoomDoorAsync(1,default)&&await entryA.CheckRoomDoorAsync(1,default)&&factoryA.DoorRequests==doorRequests,"단서 측 반복 개폐는 로컬 처리");
        await sessionA.HeartbeatAsync(default);Check(sessionA.State.IsJoined,"바이너리 생존 확인으로 세션 유지");
        await sessionA.LeaveAsync(default);Check(!entryA.RoomPermissions.HasPermission(1),"퇴장 시 허가 캐시 해제");
        await entryA.EnterAsync(false,address,null,default);Check(entryA.Data!=null&&!entryA.RoomPermissions.HasPermission(1),"재입장은 새 허가 캐시 사용");
        sessionA.Disconnect(SessionFailureCode.ConnectionFailed);Check(!entryA.RoomPermissions.HasPermission(1),"연결 종료 시 허가 캐시 해제");
        await sessionB.LeaveAsync(default);
        Console.WriteLine($"{checks} 개 C#·서버 Protobuf HTTP 통합 검사 통과.");
    }
    private sealed class HttpFactory:ISessionGatewayFactory,IDisposable
    {
        private readonly List<TestTransport> transports=new();
        public int DoorRequests=>transports.Sum(t=>t.DoorRequests);
        public ISessionGateway Create(string address){var transport=new TestTransport(address);transports.Add(transport);return new HttpSessionGateway(new SessionApiClient(transport));}
        public void Dispose(){foreach(var transport in transports)transport.Dispose();}
    }
    // 실제 생성 API·매핑·검증·Gateway·코어를 .NET HTTP 어댑터로 검사합니다.
    private sealed class TestTransport:GameApiClientBase,IDisposable
    {
        private readonly string address;private readonly HttpClient http=new(){Timeout=TimeSpan.FromMilliseconds(10000)};
        public int DoorRequests;
        internal TestTransport(string address){this.address=address;}
        protected override async Task<GameApiResponse> SendAsync(GameApiOperation operation,IMessage body,string participantToken,CancellationToken token)
        {
            if(operation.Path.EndsWith("/rooms/door"))DoorRequests++;
            using var request=new HttpRequestMessage(new HttpMethod(operation.Method),address+operation.Path);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/protobuf"));
            if(participantToken!=null)request.Headers.Add("x-participant-token",participantToken);
            if(body!=null){request.Content=new ByteArrayContent(body.ToByteArray());request.Content.Headers.ContentType=new MediaTypeHeaderValue("application/protobuf");}
            using var response=await http.SendAsync(request,token);
            var reply=GameApiResponse.Parser.ParseFrom(await response.Content.ReadAsByteArrayAsync(token));
            ProtobufApiResponseValidator.Validate((int)response.StatusCode,response.Content.Headers.ContentType?.ToString(),reply,operation);
            return reply;
        }
        public void Dispose()=>http.Dispose();
    }
}
