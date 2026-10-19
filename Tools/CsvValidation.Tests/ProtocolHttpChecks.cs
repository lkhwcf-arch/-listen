using System.Net.Http.Headers;
using EscapeGame.Data.CsvValidation.Runtime;
using EscapeGame.Data.CsvValidation.Schema;
using EscapeGame.Networking;
using EscapeGame.Protocol.V1;
using EscapeGame.Session;
using EscapeGame.Session.Contracts;
using EscapeGame.World;
using Google.Protobuf;

static class ProtocolHttpChecks
{
    internal static async Task Run(string address,string root)
    {
        int checks=0;
        void Check(bool ok,string name){if(!ok)throw new Exception("FAIL: "+name);checks++;}
        var validator=new CsvValidator(CsvValidationCatalog.Read(File.ReadAllText(Path.Combine(root,"Assets/3.Script/Data/CsvValidation/Configuration/CsvValidationRules.xml"))));
        using var factoryA=new HttpFactory();using var factoryB=new HttpFactory();
        void Diagnostic(Exception error){if(error is not SessionResultException)Console.Error.WriteLine(error);}
        using var sessionA=new SessionClient(factoryA,default,Diagnostic);using var sessionB=new SessionClient(factoryB,default,Diagnostic);
        using var entryA=new GameEntryService(sessionA,sessionA,()=>throw new Exception("No local fallback"),validator,_=>Task.CompletedTask);
        using var entryB=new GameEntryService(sessionB,sessionB,()=>throw new Exception("No local fallback"),validator,_=>Task.CompletedTask);
        await entryA.EnterAsync(false,address,null,default);await entryB.EnterAsync(false,address,null,default);
        Check(entryA.Data!=null&&entryB.Data!=null,"both clients join and download binary CSV bundles");
        Check(entryA.Role=="A"&&entryB.Role=="B","roles mapped from generated enum");
        Check(entryA.Data.Source.Documents.Count==23,"all CSV tables survive protobuf HTTP roundtrip");
        await sessionA.ReportSceneLoadedAsync(default);await sessionB.ReportSceneLoadedAsync(default);await sessionA.QueryAsync(default);
        Check(sessionA.State.Phase==ClientSessionPhase.Playing&&sessionB.State.Phase==ClientSessionPhase.Playing,"ready/query protobuf maps both to Playing");
        Check(!await entryA.CheckRoomDoorAsync(1,default),"unsolved door returns false over HTTP");
        Check(!await entryB.SubmitRoomAnswerAsync(1,new[]{"wrong"},default),"wrong answer returns false over HTTP");
        var answers=entryB.Data.PuzzleSlots.Where(s=>s.PuzzleId==5001).OrderBy(s=>s.Slot).Select(s=>s.Answer).ToArray();
        bool rejected=false;try{await entryA.SubmitRoomAnswerAsync(1,answers,default);}catch(SessionResultException error){rejected=error.Code==SessionFailureCode.SubmissionNotAllowed;}
        Check(rejected,"clue submission is failure, not a wrong answer");
        Check(await entryB.SubmitRoomAnswerAsync(1,answers,default),"solver correct answer returns true");
        Check(entryB.RoomPermissions.HasPermission(1),"solver receives its door permission with correct answer");
        Check(entryB.UnlockedRoomIds.Contains(1004)&&!entryB.UnlockedRoomIds.Contains(1003),"only solver next is granted immediately");
        Check(await entryB.CheckRoomDoorAsync(1,default)&&await entryB.CheckRoomDoorAsync(1,default)&&factoryB.DoorRequests==0,"solver open close sends no door request");
        Check(await entryA.CheckRoomDoorAsync(1,default),"clue checks solved room once and receives permission");
        Check(entryA.UnlockedRoomIds.Contains(1003),"clue next becomes available after own door check");
        int doorRequests=factoryA.DoorRequests;
        Check(await entryA.CheckRoomDoorAsync(1,default)&&await entryA.CheckRoomDoorAsync(1,default)&&factoryA.DoorRequests==doorRequests,"clue repeat open close is local");
        await sessionA.HeartbeatAsync(default);Check(sessionA.State.IsJoined,"binary heartbeat keeps session");
        await sessionA.LeaveAsync(default);Check(!entryA.RoomPermissions.HasPermission(1),"leave clears cached permission");
        await entryA.EnterAsync(false,address,null,default);Check(entryA.Data!=null&&!entryA.RoomPermissions.HasPermission(1),"rejoin starts with empty local permission cache");
        sessionA.Disconnect(SessionFailureCode.ConnectionFailed);Check(!entryA.RoomPermissions.HasPermission(1),"disconnect clears permission cache");
        await sessionB.LeaveAsync(default);
        Console.WriteLine($"{checks} C# client/server Protobuf HTTP integration checks passed.");
    }
    private sealed class HttpFactory:ISessionGatewayFactory,IDisposable
    {
        private readonly List<TestTransport> transports=new();
        public int DoorRequests=>transports.Sum(t=>t.DoorRequests);
        public ISessionGateway Create(string address){var transport=new TestTransport(address);transports.Add(transport);return new HttpSessionGateway(new SessionApiClient(transport));}
        public void Dispose(){foreach(var transport in transports)transport.Dispose();}
    }
    // The production generated methods, mapper, validator, gateway and core are exercised with a .NET HTTP adapter.
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
