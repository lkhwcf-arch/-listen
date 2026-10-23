using EscapeGame.Data;
using EscapeGame.Data.Csv;
using EscapeGame.Data.CsvValidation.Runtime;
using EscapeGame.Data.Tables;
using EscapeGame.Interaction;
using EscapeGame.Session;
using EscapeGame.Session.Contracts;
using EscapeGame.World;

internal static class PuzzleAnswerChecks
{
    internal static AnchorInteractData FixtureAnchor(CsvGameData data, int puzzleId, string feature)
    {
        var puzzle = data.Puzzles.Single(row => row.Id == puzzleId);
        var original = data.InteractAnchors.Single(row => row.Id == puzzle.InputAnchor);
        var prefab = data.Prefabs.First(row => new PrefabFeaturePlan(data, row.Id).Has(feature));
        string Quote(string text) => "\"" + text.Replace("\"", "\"\"") + "\"";
        return new AnchorInteractData(new CsvParser().Read("ID,RoomNo,PlayerType,AnchorID,PrefabID,Active\n" +
            string.Join(",", new[] { original.Id.ToString(), original.RoomNo.ToString(), original.PlayerType, original.AnchorId, prefab.Id.ToString(), "Y" }.Select(Quote))).Rows[0]);
    }
    internal static PuzzleInputPlan Plan(CsvGameData data, int puzzleId, string role, string feature)
    {
        var anchor = FixtureAnchor(data, puzzleId, feature);
        return PuzzleInputPlan.Create(data, role, anchor, new PrefabFeaturePlan(data, anchor.PrefabId));
    }
    internal static async Task Run(Dictionary<string, string> original, string xml)
    {
        int count = 0;
        void Check(bool condition, string message)
        {
            if (!condition)
                throw new Exception("정답 입력 검사 실패: " + message);
            count++;
        }
        void Reject(Action action, string message)
        {
            try
            {
                action();
            }
            catch (InvalidOperationException)
            {
                count++;
                return;
            }
            throw new Exception("거부 검사 실패: " + message);
        }
        var fixture = new TestValidator(xml);
        var source = fixture.Input(original);
        var store = new CsvDataStore();
        store.TryRegister(source, new CsvValidator(fixture.Catalog), out _);
        var data = new CsvGameData(store);
        var plan = Plan(data, 5001, "B", FeatureScripts.Dial);
        Check(plan.RoomNo == 1 && plan.PuzzleId == 5001 && plan.AnchorId == 2030 && plan.SlotCount == 4, "입력 앵커·퍼즐·방 자동 연결");
        Check(plan.Choices.SequenceEqual(Enumerable.Range(1, 9).Select(i => i.ToString())) && plan.SlotLabels.SequenceEqual(new[] { "A", "K", "R", "W" }), "다이얼 선택지와 표시 데이터");
        Check(plan.GetType().GetProperties().All(p => !p.Name.Contains("Answer")), "입력 계획은 정답 값을 노출하지 않음");
        Reject(() => Plan(data, 5001, "A", FeatureScripts.Dial), "다른 역할의 입력 계획 거부");
        var letters = Plan(data, 5006, "A", FeatureScripts.Dial);
        Check(letters.Choices.SequenceEqual(data.Locks.Single(r => r.AnchorRef == letters.AnchorId).DialLetters), "문자 다이얼은 시트 순서");
        var zero = Plan(data, 5005, "B", FeatureScripts.Dial);
        Check(zero.Choices.First() == "0" && zero.Choices.Count == 10, "0을 포함한 숫자 다이얼");
        var color = Plan(data, 5007, "A", FeatureScripts.Dial);
        Check(color.Choices.SequenceEqual(data.ColorLetters.OrderBy(r => r.Id).Select(r => r.Color).Distinct()), "색 목록은 테이블에서 구성");
        var sequence = Plan(data, 5003, "B", FeatureScripts.Console);
        Check(sequence.Kind == PuzzleInputKind.Sequence && sequence.SlotCount == 10 && sequence.Choices.SequenceEqual(data.Consoles.Single().Buttons), "버튼 순서 입력 구성");
        var anchor = FixtureAnchor(data, 5001, FeatureScripts.Dial);
        var inactive = new AnchorInteractData(new CsvParser().Read("ID,RoomNo,PlayerType,AnchorID,PrefabID,Active\n2030,1,B,test," + anchor.PrefabId + ",N").Rows[0]);
        Reject(() => PuzzleInputPlan.Create(data, "B", inactive, new PrefabFeaturePlan(data, anchor.PrefabId)), "비활성 입력 거부");
        var wrongAnchor = new AnchorInteractData(new CsvParser().Read("ID,RoomNo,PlayerType,AnchorID,PrefabID,Active\n99001,1,B,test," + anchor.PrefabId + ",Y").Rows[0]);
        Reject(() => PuzzleInputPlan.Create(data, "B", wrongAnchor, new PrefabFeaturePlan(data, anchor.PrefabId)), "없는 PUZZLE 연결 거부");
        var fake = new Service();
        using (var input = new PuzzleAnswerController(plan, fake))
        {
            Check(input.CanSubmit && input.Values.All(v => v == "1"), "다이얼 초기 상태");
            input.Cycle(0, -1);
            Check(input.Values[0] == "9", "이전 값 순환");
            input.Cycle(0, 1);
            Check(input.Values[0] == "1", "다음 값 순환");
            input.Choose("3");
            input.SelectSlot(1);
            input.Choose("7");
            Check(input.Values.Take(2).SequenceEqual(new[] { "3", "7" }), "선택 슬롯만 변경");
            fake.Reply = RoomAnswerResult.Decision(false);
            await input.SubmitAsync();
            Check(input.Phase == AnswerInputPhase.Incorrect && input.Failure == SessionFailureCode.None, "false는 오답으로 반영");
            Check(fake.Room == 1 && fake.Answers.SequenceEqual(input.Values), "방 번호와 문자열 배열 전송");
            fake.Reply = RoomAnswerResult.Failed(SessionFailureCode.ConnectionFailed);
            await input.SubmitAsync();
            Check(input.Phase == AnswerInputPhase.Failed && input.Failure == SessionFailureCode.ConnectionFailed && input.CanSubmit, "통신 실패를 오답과 구분하고 재시도 허용");
            Check(AnswerInputMessages.Describe(input).Contains("연결"), "실패 코드의 한글 표시");
            fake.Reply = RoomAnswerResult.Decision(true);
            await input.SubmitAsync();
            Check(input.Phase == AnswerInputPhase.Correct && !input.CanEdit && !input.CanSubmit, "정답 후 중복 제출 차단");
            int calls = fake.Calls;
            await input.SubmitAsync();
            input.Reset();
            Check(fake.Calls == calls && input.Phase == AnswerInputPhase.Correct, "정답 상태는 UI 초기화로 해제하지 않음");
        }
        using (var input = new PuzzleAnswerController(sequence, fake))
        {
            Check(!input.CanSubmit && input.Values.All(v => v == ""), "버튼 입력 초기 미입력");
            int calls = fake.Calls;
            await input.SubmitAsync();
            Check(fake.Calls == calls, "미완성 입력 제출 금지");
            input.Choose("Right");
            Check(input.Values[0] == "Right" && input.SelectedSlot == 1, "입력 후 다음 슬롯으로 이동");
            input.SelectSlot(0);
            input.Choose("Up");
            Check(input.Values[0] == "Up", "기존 슬롯 재선택");
            input.Reset();
            Check(!input.CanSubmit && input.SelectedSlot == 0, "입력 초기화");
            for (int i = 0; i < input.Plan.SlotCount; i++)
                input.Choose("Down");
            Check(input.CanSubmit, "모든 슬롯 입력 후 제출 허용");
            await input.SubmitAsync();
            Check(fake.Room == 3 && fake.Answers.Length == 10, "고정된 1번 방 대신 테이블의 3번 방 전송");
        }
        var completion = new TaskCompletionSource<RoomAnswerResult>();
        fake.Pending = completion;
        using (var input = new PuzzleAnswerController(plan, fake))
        {
            int calls = fake.Calls;
            var task = input.SubmitAsync();
            var snapshot = input.Values.ToArray();
            Check(input.Phase == AnswerInputPhase.Submitting && !input.CanEdit, "요청 중 편집 잠금");
            input.Cycle(0, 1);
            input.Reset();
            await input.SubmitAsync();
            Check(fake.Calls == calls + 1 && input.Values.SequenceEqual(snapshot), "중복 제출과 요청 중 값 변경 차단");
            completion.SetResult(RoomAnswerResult.Decision(false));
            await task;
            Check(input.Phase == AnswerInputPhase.Incorrect && input.CanSubmit, "응답 후 재입력 허용");
        }
        completion = new TaskCompletionSource<RoomAnswerResult>();
        fake.Pending = completion;
        var removed = new PuzzleAnswerController(plan, fake);
        int events = 0;
        removed.Changed += () => events++;
        var late = removed.SubmitAsync();
        removed.Dispose();
        int before = events;
        Check(fake.Token.IsCancellationRequested, "닫기 시 진행 중 요청 취소");
        completion.SetResult(RoomAnswerResult.Decision(true));
        await late;
        Check(events == before, "이전 패널의 늦은 응답 반영 금지");
        removed.Dispose();
        completion = new TaskCompletionSource<RoomAnswerResult>();
        fake.Pending = completion;
        using (var owner = new CancellationTokenSource())
        using (var input = new PuzzleAnswerController(plan, fake, owner.Token))
        {
            var pending = input.SubmitAsync();
            owner.Cancel();
            completion.SetResult(RoomAnswerResult.Decision(true));
            await pending;
            Check(input.Phase == AnswerInputPhase.Cancelled, "게임 수명 취소를 정답과 구분");
        }
        fake.Pending = null;
        fake.Reply = null;
        using (var input = new PuzzleAnswerController(plan, fake))
        {
            await input.SubmitAsync();
            Check(input.Failure == SessionFailureCode.InvalidResponse, "빈 응답 거부");
        }
        fake.Error = new InvalidOperationException("검사용 예외");
        int diagnostics = 0;
        using (var input = new PuzzleAnswerController(plan, fake, default, _ => diagnostics++))
        {
            await input.SubmitAsync();
            Check(input.Failure == SessionFailureCode.UnexpectedFailure && diagnostics == 1, "예상 밖 오류의 안전한 표시·진단 분리");
        }
        fake.Error = null;
        using var session = new SessionClient(new NoNetworkFactory(), default);
        using var entry = new GameEntryService(session, session, () => source, new CsvValidator(fixture.Catalog), _ => Task.CompletedTask);
        await entry.EnterAsync(true, null, "B", default);
        var service = (IRoomAnswerService)entry;
        var wrong = await service.SubmitAnswerAsync(1, new[] { "wrong" }, default);
        Check(wrong.IsSuccess && !wrong.Correct, "게임 진입 Facade의 로컬 false 계약");
        var answer = data.PuzzleSlots.Where(r => r.PuzzleId == 5001).OrderBy(r => r.Slot).Select(r => r.Answer).ToArray();
        var correct = await service.SubmitAnswerAsync(1, answer, default);
        Check(correct.IsSuccess && correct.Correct && entry.RoomPermissions.HasPermission(1), "공통 경로로 판정 후 문 허가 캐시 연결");
        var forbidden = await service.SubmitAnswerAsync(2, answer, default);
        Check(!forbidden.IsSuccess && forbidden.Failure == SessionFailureCode.SubmissionNotAllowed, "내부 예외를 공개 오류 결과로 변환");
        Check(AnswerInputMessages.Value("Right") == "오른쪽" && AnswerInputMessages.Value("L") == "L", "표시 한글화와 원본 입력 토큰 보존");
        Console.WriteLine($"정답 입력·API 연결 검사 {count}개 통과.");
    }
    private sealed class Service : IRoomAnswerService
    {
        public int Calls, Room;
        public string[] Answers;
        public CancellationToken Token;
        public Exception Error;
        public RoomAnswerResult Reply = RoomAnswerResult.Decision(false);
        public TaskCompletionSource<RoomAnswerResult> Pending;
        public Task<RoomAnswerResult> SubmitAnswerAsync(int roomNo, string[] answers, CancellationToken token)
        {
            Calls++;
            Room = roomNo;
            Answers = answers;
            Token = token;
            return Error != null ? Task.FromException<RoomAnswerResult>(Error) : Pending?.Task ?? Task.FromResult(Reply);
        }
    }
    private sealed class NoNetworkFactory : ISessionGatewayFactory
    {
        public ISessionGateway Create(string address) => throw new InvalidOperationException("로컬 검사는 네트워크에 접근하지 않습니다.");
    }
}
