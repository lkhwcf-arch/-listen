using EscapeGame.Networking;
using EscapeGame.Protocol.V1;
using EscapeGame.Session;
using EscapeGame.Session.Contracts;
using EscapeGame.World;
using Google.Protobuf;

static class RoomPermissionChecks
{
    internal static async Task Run()
    {
        int checks = 0;
        void Check(bool ok, string name)
        {
            if (!ok)
                throw new Exception("FAIL: " + name);
            checks++;
        }
        void Failure(Action action, SessionFailureCode expected, string name)
        {
            bool caught = false;
            try
            {
                action();
            }
            catch (SessionResultException error)
            {
                caught = error.Code == expected;
            }
            Check(caught, name);
        }
        int requests = 0;
        bool grant = false;
        var permissions = new RoomPermissionService((room, answers, token) =>
        {
            requests++;
            return Task.FromResult(new RoomDecision(room, grant, grant));
        });
        Check(!await permissions.CheckDoorAsync(1, default), "locked door is false");
        Check(!await permissions.CheckDoorAsync(1, default) && requests == 2, "false is not permanently cached");
        grant = true;
        Check(await permissions.CheckDoorAsync(1, default) && requests == 3, "later permission is cached");
        Check(await permissions.CheckDoorAsync(1, default) && await permissions.CheckDoorAsync(1, default) && requests == 3, "repeated open close does not send requests");
        grant = false;
        Check(!await permissions.CheckDoorAsync(2, default) && permissions.HasPermission(1), "permission scoped by room");
        permissions.Reset();
        Check(!permissions.HasPermission(1), "new entry clears permission");
        int submits = 0;
        var answersService = new RoomPermissionService((room, answers, token) =>
        {
            submits++;
            return Task.FromResult(new RoomDecision(room, true, true));
        });
        Check(await answersService.SubmitAnswerAsync(1, new[] { "3", "7", "2", "5" }, default), "correct answer is true");
        Check(await answersService.CheckDoorAsync(1, default) && submits == 1, "correct permitted answer eliminates initial door request");
        var both = new RoomPermissionService((room, answers, token) => Task.FromResult(new RoomDecision(room, true, false)));
        Check(await both.SubmitAnswerAsync(7, new[] { "Red" }, default) && !both.HasPermission(7), "BothSolved correct answer is separate from permission");
        var wrong = new RoomPermissionService((room, answers, token) => Task.FromResult(new RoomDecision(room, false, false)));
        Check(!await wrong.SubmitAnswerAsync(1, new[] { "wrong" }, default), "wrong answer remains false");
        var network = new RoomPermissionService((room, answers, token) => Task.FromException<RoomDecision>(new SessionResultException(SessionFailureCode.ConnectionFailed)));
        bool failed = false;
        try
        {
            await network.CheckDoorAsync(1, default);
        }
        catch (SessionResultException error)
        {
            failed = error.Code == SessionFailureCode.ConnectionFailed;
        }
        Check(failed && !network.HasPermission(1), "transport failure is not a locked or wrong answer");
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        bool stopped = false;
        try
        {
            await permissions.CheckDoorAsync(1, cancelled.Token);
        }
        catch (OperationCanceledException)
        {
            stopped = true;
        }
        Check(stopped, "cancelled request cannot grant permission");
        var completion = new TaskCompletionSource<RoomDecision>(TaskCreationOptions.RunContinuationsAsynchronously);
        var late = new RoomPermissionService((room, answers, token) => completion.Task);
        var pending = late.CheckDoorAsync(1, default);
        bool busy = false;
        try
        {
            await late.CheckDoorAsync(1, default);
        }
        catch (SessionResultException error)
        {
            busy = error.Code == SessionFailureCode.Busy;
        }
        Check(busy, "duplicate room request is controlled centrally");
        late.Reset();
        completion.SetResult(new RoomDecision(1, true, true));
        bool stale = false;
        try
        {
            await pending;
        }
        catch (OperationCanceledException)
        {
            stale = true;
        }
        Check(stale && !late.HasPermission(1), "old response cannot grant new entry permission");
        var mismatch = new RoomPermissionService((room, answers, token) => Task.FromResult(new RoomDecision(2, true, true)));
        failed = false;
        try
        {
            await mismatch.CheckDoorAsync(1, default);
        }
        catch (SessionResultException error)
        {
            failed = error.Code == SessionFailureCode.InvalidResponse;
        }
        Check(failed && !mismatch.HasPermission(1), "different room reply rejected");
        var inconsistent = new RoomPermissionService((room, answers, token) => Task.FromResult(new RoomDecision(room, false, true)));
        failed = false;
        try
        {
            await inconsistent.CheckDoorAsync(1, default);
        }
        catch (SessionResultException error)
        {
            failed = error.Code == SessionFailureCode.InvalidResponse;
        }
        Check(failed, "door boolean and permission cannot contradict");

        var operation = GameApiRoutes.CheckRoomDoor("session");
        var locked = new GameApiResponse { Room = new RoomDecisionReply { RoomNo = 1, Value = false, DoorAllowed = false, Snapshot = new SessionSnapshot { SessionId = "session", Phase = SessionPhase.Playing } } };
        var decoded = GameApiResponse.Parser.ParseFrom(locked.ToByteArray());
        ProtobufApiResponseValidator.Validate(200, "application/protobuf", decoded, operation);
        Check(decoded.OutcomeCase == GameApiResponse.OutcomeOneofCase.Room && !decoded.Room.Value, "explicit false survives Google Protobuf");
        Failure(() => ProtobufApiResponseValidator.Validate(200, "application/protobuf", new GameApiResponse(), operation), SessionFailureCode.InvalidResponse, "empty outcome is not false");
        Failure(() => ProtobufApiResponseValidator.Validate(200, "application/json", locked, operation), SessionFailureCode.InvalidResponse, "JSON is not accepted as protobuf");
        Failure(() => ProtobufApiResponseValidator.Validate(201, "application/protobuf", locked, operation), SessionFailureCode.InvalidResponse, "wrong HTTP success status rejected");
        Failure(() => ProtobufApiResponseValidator.Validate(200, "application/protobuf", new GameApiResponse { Completed = new OperationCompleted() }, operation), SessionFailureCode.InvalidResponse, "wrong response branch rejected");
        foreach (var pair in new[] { (SessionErrorCode.InvalidParticipant, 403, SessionFailureCode.InvalidParticipant), (SessionErrorCode.SubmissionNotAllowed, 409, SessionFailureCode.SubmissionNotAllowed), (SessionErrorCode.GameNotPlaying, 409, SessionFailureCode.GameNotPlaying), (SessionErrorCode.RoomNotFound, 404, SessionFailureCode.RoomNotFound) })
            Failure(() => ProtobufApiResponseValidator.Validate(pair.Item2, "application/protobuf", new GameApiResponse { Failure = new SessionFailure { Code = pair.Item1 } }, operation), pair.Item3, "failure code " + pair.Item1);
        Failure(() => ProtobufApiResponseValidator.Validate(200, "application/protobuf", new GameApiResponse { Failure = new SessionFailure { Code = SessionErrorCode.InvalidParticipant } }, operation), SessionFailureCode.InvalidResponse, "error code and status must match");
        Failure(() => ProtobufApiResponseValidator.Validate(500, "application/protobuf", new GameApiResponse { Failure = new SessionFailure { Code = (SessionErrorCode)999 } }, operation), SessionFailureCode.InvalidResponse, "unknown error enum rejected");
        Failure(() => ProtobufApiResponseValidator.Validate(500, "application/protobuf", new GameApiResponse { Failure = new SessionFailure { Code = (SessionErrorCode)0 } }, operation), SessionFailureCode.InvalidResponse, "default error enum rejected");

        using var session = new SessionClient(new TestFactory(), default);
        await session.JoinAsync("http://test", default);
        var decision = await session.CheckRoomDoorAsync(1, default);
        Check(!decision.Value && session.State.Phase == ClientSessionPhase.Playing, "session facade applies snapshot before returning false decision");
        decision = await session.SubmitRoomAnswerAsync(1, new[] { "3" }, default);
        Check(decision.Value && decision.DoorAllowed, "session facade returns correct room decision");
        Console.WriteLine($"{checks} room permission/protobuf checks passed.");
    }
    private sealed class TestFactory : ISessionGatewayFactory
    {
        public ISessionGateway Create(string address) => new TestGateway();
    }
    private sealed class TestGateway : ISessionGateway, ISessionRoomGateway
    {
        public Task<SessionJoinData> JoinAsync(CancellationToken token) => Task.FromResult(new SessionJoinData("session", new SessionPlayerData("b", "B", false), "token"));
        private SessionSnapshotData Snapshot => new SessionSnapshotData("session", ClientSessionPhase.Playing, new[] { new SessionPlayerData("a", "A", true), new SessionPlayerData("b", "B", true) }, new[] { 1001, 1002 });
        public Task<SessionRoomDecisionData> DecideRoomAsync(string session, string token, int room, string[] answers, CancellationToken ct) => Task.FromResult(new SessionRoomDecisionData(Snapshot, new RoomDecision(room, answers != null, answers != null)));
        public Task<SessionSnapshotData> QueryAsync(string s, string t, CancellationToken ct) => Task.FromResult(Snapshot);
        public Task<SessionSnapshotData> ReportSceneLoadedAsync(string s, string t, CancellationToken ct) => Task.FromResult(Snapshot);
        public Task LeaveAsync(string s, string t, CancellationToken ct) => Task.CompletedTask;
        public Task HeartbeatAsync(string s, string t, CancellationToken ct) => Task.CompletedTask;
    }
}
