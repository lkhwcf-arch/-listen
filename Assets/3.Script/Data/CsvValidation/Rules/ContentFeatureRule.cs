using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using EscapeGame.Data.CsvValidation.Contracts;
using EscapeGame.Data.CsvValidation.Runtime;

namespace EscapeGame.Data.CsvValidation.Rules
{
    // 서버와 동일한 생성 계약을 사용합니다. Unity 리소스·Collider 검사는 에디터 어댑터에 남깁니다.
    public sealed class ContentFeatureRule : ICsvValidationRule
    {
        public void Validate(CsvValidationContext context)
        {
            var components = context.Find("COMPONENT");
            if (components != null) foreach (var row in components.Rows)
            {
                string script = row.Get("Script"), raw = row.Get("Params");
                void Fail(string column, string reason) => context.Add(CsvValidationCode.InvalidValue, components.Name, row.SourceLine, column, reason);
                if (!ContentContract.Features.Contains(script)) { Fail("Script", "미지원 기능: " + script); continue; }
                string required = ContentContract.RequiredParameter(script);
                if (required == null) { if (raw != "-" && raw != "") Fail("Params", "매개변수가 없는 기능은 -를 사용합니다."); continue; }
                var parts = raw.Split('=');
                if (parts.Length != 2 || parts[0].Trim() != required) { Fail("Params", required + "=값 형식이 필요합니다."); continue; }
                string value = parts[1].Trim();
                if (required == "HighlightColor")
                {
                    if (!Regex.IsMatch(value, "^#[0-9a-fA-F]{6}([0-9a-fA-F]{2})?$") && !ContentContract.Colors.Contains(value.ToLowerInvariant()))
                        Fail("Params", "지원하는 색 이름 또는 #RRGGBB[AA]가 필요합니다.");
                }
                else
                {
                    var values = value.Split(',');
                    var numbers = values.Select(item => double.TryParse(item, NumberStyles.Float, CultureInfo.InvariantCulture, out double number) ? number : double.NaN).ToArray();
                    if (numbers.Length != 3 || numbers.Any(number => double.IsNaN(number) || double.IsInfinity(number) || Math.Abs(number) > 100) ||
                        numbers.All(number => Math.Round(Math.Abs(number) * 1000, MidpointRounding.AwayFromZero) == 0))
                        Fail("Params", "CamOffset은 ±100m 이내의 0이 아닌 x,y,z입니다.");
                }
            }
            var puzzles = context.Find("PUZZLE");
            if (puzzles != null) foreach (var row in puzzles.Rows)
                if (!ContentContract.PuzzleTypes.Contains(row.Get("Type")))
                    context.Add(CsvValidationCode.InvalidValue, puzzles.Name, row.SourceLine, "Type", row.Get("Type"), "미지원 퍼즐 종류");
            var prefabs = context.Find("PREFAB");
            if (prefabs == null || components == null) return;
            foreach (var row in prefabs.Rows)
            {
                var names = row.Get("Components").Split('|').Select(id => components.Find(id.Trim())?.Get("Script")).Where(name => name != null).ToArray();
                void Fail(string reason) => context.Add(CsvValidationCode.InvalidValue, prefabs.Name, row.SourceLine, "Components", reason);
                if (names.Distinct(StringComparer.Ordinal).Count() != names.Length) Fail("같은 기능을 중복 선언했습니다.");
                if (names.Any(name => new[] { "CloseUpUI", "DialLock", "ButtonConsole", "Door" }.Contains(name)) && !names.Contains("Interactable")) Fail("Interactable이 필요합니다.");
                if (names.Any(name => name == "DialLock" || name == "ButtonConsole") && !names.Contains("CloseUpUI")) Fail("CloseUpUI가 필요합니다.");
                if (names.Contains("DialLock") && names.Contains("ButtonConsole")) Fail("DialLock과 ButtonConsole은 함께 사용할 수 없습니다.");
            }
        }
    }
}
