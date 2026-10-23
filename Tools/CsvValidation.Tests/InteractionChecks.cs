using EscapeGame.Data;
using EscapeGame.Data.Csv;
using EscapeGame.Data.CsvValidation.Runtime;
using EscapeGame.Data.CsvValidation.Contracts;
using EscapeGame.Data.Tables;
using EscapeGame.Interaction;
using EscapeGame.World;

internal static class InteractionChecks
{
    public static void Run(Dictionary<string, string> original, string xml)
    {
        int count = 0;
        void Check(bool valid, string name)
        {
            if (!valid)
                throw new Exception("상호작용 검사 실패: " + name);
            count++;
        }
        void Reject(Action action, string name)
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
            throw new Exception("상호작용 거부 검사 실패: " + name);
        }
        void Cell(Dictionary<string, string> input, string sheet, string id, string column, string value)
        {
            var document = new CsvParser().Read(input[sheet]);
            string Quote(string text) => "\"" + text.Replace("\"", "\"\"") + "\"";
            input[sheet] = string.Join(",", document.Headers.Select(Quote)) + "\n" + string.Join("\n", document.Rows.Select(row =>
                string.Join(",", document.Headers.Select(header => Quote(row.Get("ID") == id && header == column ? value : row.Get(header))))));
        }
        CsvGameData Data(Action<Dictionary<string, string>> change = null)
        {
            var copy = new Dictionary<string, string>(original);
            change?.Invoke(copy);
            var fixture = new TestValidator(xml);
            var store = new CsvDataStore();
            if (!store.TryRegister(fixture.Input(copy), new CsvValidator(fixture.Catalog), out var report))
                throw new InvalidOperationException(string.Join("\n", report.Issues));
            return new CsvGameData(store);
        }
        foreach (string invalid in new[] { "99999", "14001|14001", "14001||14005", "0", "-1", "1.5", "2147483648" })
        {
            var copy = new Dictionary<string, string>(original);
            Cell(copy, "PREFAB", "13002", "Components", invalid);
            Check(new TestValidator(xml).Validate(copy).Any(issue => issue.Severity == CsvValidationSeverity.Error && issue.Column == "Components"), "기능 ID 검사 " + invalid);
        }
        var data = Data();
        var door = new PrefabFeaturePlan(data, 13002);
        Check(door.Has(FeatureScripts.Interactable) && door.Has(FeatureScripts.Door) && !door.Has(FeatureScripts.Closeup), "데이터 선언에 따른 문 기능 선택");
        Check(new PrefabFeaturePlan(data, 13003).Components.Count == 0, "빈 선언은 기능 없음");
        var closeup = new PrefabFeaturePlan(data, 13005);
        Check(closeup.CloseupCameraOffsetMm().SequenceEqual(new[] { 0, 300, -500 }), "미터를 정수 mm로 변환");
        Check(closeup.Has(FeatureScripts.Dial), "복수 기능 조합");
        var remap = Data(copy =>
        {
            Cell(copy, "COMPONENT", "14001", "ID", "14901");
            foreach (var row in new CsvParser().Read(copy["PREFAB"]).Rows.ToArray())
                Cell(copy, "PREFAB", row.Get("ID"), "Components", string.Join("|", row.Get("Components").Split('|').Select(id => id == "14001" ? "14901" : id)));
        });
        Check(new PrefabFeaturePlan(remap, 13002).Has(FeatureScripts.Interactable), "코드 변경 없이 기능 ID 교체");
        Reject(() => new PrefabFeaturePlan(Data(copy => Cell(copy, "COMPONENT", "14002", "Script", "MissingFeature")), 13005), "미등록 기능");
        Reject(() => new PrefabFeaturePlan(Data(copy => Cell(copy, "COMPONENT", "14002", "Script", "Interactable")), 13005), "기능 중복");
        Reject(() => new PrefabFeaturePlan(Data(copy => Cell(copy, "PREFAB", "13005", "Components", "14002|14003")), 13005), "Interactable 필수 조합");
        Reject(() => new PrefabFeaturePlan(Data(copy => Cell(copy, "PREFAB", "13005", "Components", "14001|14003")), 13005), "CloseUpUI 필수 조합");
        foreach (string text in new[] { "CamOffset=0,0,0", "CamOffset=1,2", "CamOffset=NaN,1,2", "CamOffset=101,0,0", "Wrong=1", "CamOffset=1,2,3;CamOffset=3,2,1" })
            Reject(() => new PrefabFeaturePlan(Data(copy => Cell(copy, "COMPONENT", "14002", "Params", text)), 13005).CloseupCameraOffsetMm(), text);
        foreach (string invalid in new[] { "1e100", "3000000" })
        {
            var copy = new Dictionary<string, string>(original);
            Cell(copy, "CHARACTER_STAT", "17001", "InteractDistance", invalid);
            Check(new TestValidator(xml).Validate(copy).Any(issue => issue.Severity == CsvValidationSeverity.Error && issue.Column == "InteractDistance"), "거리 int32 경계");
        }
        int leases = 0, releases = 0, events = 0;
        var controller = new CloseupController(() =>
        {
            leases++;
            return new Lease(() => releases++);
        });
        controller.Changed += () => events++;
        var target = new Target();
        Check(!controller.Open(null) && !controller.Open(new Target { SupportsCloseup = false }), "잘못된 대상은 잠금 획득 없음");
        Check(controller.Open(target) && controller.Target == target && leases == 1 && events == 1, "열기 상태·잠금 반영 후 이벤트 통지");
        Check(!controller.Open(target) && leases == 1 && events == 1, "중복 열기 무시");
        controller.Refresh();
        Check(controller.IsOpen && releases == 0, "가용 대상은 패널 유지");
        target.IsAvailable = false;
        controller.Refresh();
        Check(!controller.IsOpen && releases == 1 && events == 2, "비활성 대상 닫기와 잠금 1회 해제");
        controller.Close();
        Check(releases == 1 && events == 2, "중복 닫기 무시");
        Check(!controller.Open(target), "비활성 대상 재열기 거부");
        target.IsAvailable = true;
        controller.Open(target);
        controller.Dispose();
        controller.Dispose();
        Check(releases == 2 && events == 4 && !controller.IsOpen, "제거 시 1회 정리");
        Reject(() => controller.Open(target), "제거 이후 열기 거부");
        Console.WriteLine($"데이터 기반 상호작용·클로즈업 검사 {count}개 통과.");
    }
    private sealed class Target : ICloseupTarget
    {
        public bool IsAvailable
        {
            get;
            set;
        } = true;
        public bool SupportsCloseup
        {
            get;
            set;
        } = true;
    }
    private sealed class Lease : IDisposable
    {
        private Action release;
        public Lease(Action release) => this.release = release;
        public void Dispose()
        {
            var owned = release;
            release = null;
            owned?.Invoke();
        }
    }
}
