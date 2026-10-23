using EscapeGame.Data;
using EscapeGame.Data.Csv;
using EscapeGame.Data.CsvValidation.Contracts;
using EscapeGame.Session;
using EscapeGame.Session.Contracts;
using EscapeGame.World;

static class RefactorChecks
{
    public static async Task Run(Dictionary<string, string> source, string xml)
    {
        int checks = 0;
        void Check(bool value, string reason)
        {
            if (!value)
                throw new Exception(reason);
            checks++;
        }
        foreach (var mutation in new[] { ("COMPONENT", "Script", "UnknownFeature"), ("COMPONENT", "Params", "bad"), ("PUZZLE", "Type", "UnknownPuzzle") })
        {
            var copy = new Dictionary<string, string>(source);
            var doc = new CsvParser().Read(copy[mutation.Item1]);
            string Quote(string text) => "\"" + text.Replace("\"", "\"\"") + "\"";
            copy[mutation.Item1] = string.Join(",", doc.Headers.Select(Quote)) + "\n" + string.Join("\n", doc.Rows.Select((row, index) =>
                string.Join(",", doc.Headers.Select(h => Quote(index == 0 && h == mutation.Item2 ? mutation.Item3 : row.Get(h))))));
            Check(new TestValidator(xml).Validate(copy).Any(issue => issue.Severity == CsvValidationSeverity.Error && issue.Sheet == mutation.Item1 && issue.Column == mutation.Item2), "실행 전 계약 오류 거부");
        }
        var state = new ClientSessionState();
        state.ApplyJoin(new SessionJoinData("test", new SessionPlayerData("a", "A", false), "token"));
        int changes = 0;
        state.RoomsChanged += _ => changes++;
        SessionSnapshotData Snapshot(int[] ids) => new SessionSnapshotData("test", ClientSessionPhase.Loading,
            new[] { new SessionPlayerData("a", "A", false), new SessionPlayerData("b", "B", false) }, ids);
        state.ApplySnapshot(Snapshot(new[] { 1001, 1002 }));
        state.ApplySnapshot(Snapshot(new[] { 1002, 1001 }));
        Check(changes == 1, "같은 방 집합은 순서가 달라도 재통지하지 않음");
        state.ApplySnapshot(Snapshot(new[] { 1001, 1002, 1004 }));
        Check(changes == 2, "실제 해금 변경은 통지");
        state.Clear();
        state.ApplyJoin(new SessionJoinData("test", new SessionPlayerData("a", "A", false), "token2"));
        state.ApplySnapshot(Snapshot(new[] { 1001, 1002 }));
        Check(changes == 3, "재입장 첫 스냅샷은 다시 통지");
        int calls = 0, solved = 0;
        using var owner = new CancellationTokenSource();
        using var service = new RoomInteractionService((room, answers, token) =>
        {
            calls++;
            return Task.FromResult(new RoomDecision(room, answers == null || answers[0] == "정답", true));
        }, owner.Token);
        service.SetAvailable(true);
        service.RoomSolved += room =>
        {
            Check(service.HasDoorPermission(room), "캐시 반영 후 정답 결과 이벤트");
            solved++;
        };
        await service.SubmitRoomAnswerAsync(1, new[] { "정답" }, default);
        Check(solved == 1 && await service.CheckRoomDoorAsync(1, default) && calls == 1, "정답 후 문은 캐시 사용");
        service.Reset();
        Check(!service.IsAvailable && !service.HasDoorPermission(1), "입장 수명 종료가 가용성과 허가를 함께 초기화");
        owner.Cancel();
        bool cancelled = false;
        try
        {
            await service.SubmitRoomAnswerAsync(1, new[] { "정답" }, default);
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
        }
        Check(cancelled && solved == 1, "소유자 취소 후 정답 이벤트 차단");
        Console.WriteLine($"공통 계약·변경 이벤트·방 명령 서비스 검사 {checks}개 통과");
    }
}
