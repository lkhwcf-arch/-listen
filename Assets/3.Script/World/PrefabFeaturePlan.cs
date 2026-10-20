using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using EscapeGame.Data.Tables;

namespace EscapeGame.World
{
    // 기능 이름은 콘텐츠 ID/프리팹 이름과 독립적입니다. Unity Reflection으로 임의 클래스를 생성하지 않습니다.
    public static class FeatureScripts
    {
        public const string Interactable = "Interactable", Closeup = "CloseUpUI", Dial = "DialLock", Console = "ButtonConsole",
            Door = "Door", Sign = "LetterSign", Screen = "ScreenText", Spawn = "Spawn";
        public static bool IsKnown(string script) => new[] { Interactable, Closeup, Dial, Console, Door, Sign, Screen, Spawn }.Contains(script);
    }
    public sealed class PrefabFeaturePlan
    {
        public IReadOnlyList<ComponentData> Components { get; }
        public int PrefabId { get; }
        public bool Has(string script) => Components.Any(row => row.Script == script);
        public PrefabFeaturePlan(CsvGameData data, int prefabId)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            PrefabId = prefabId;
            var prefab = data.Prefabs.Single(row => row.Id == prefabId);
            var modules = prefab.Components.Select(id => data.Components.Single(row => row.Id == id)).ToArray();
            if (modules.Any(row => !FeatureScripts.IsKnown(row.Script)))
                throw new InvalidOperationException("등록되지 않은 기능입니다: " + string.Join(", ", modules.Where(row => !FeatureScripts.IsKnown(row.Script)).Select(row => row.Script)));
            if (modules.Select(row => row.Script).Distinct(StringComparer.Ordinal).Count() != modules.Length)
                throw new InvalidOperationException("같은 기능을 한 프리팹에 중복 선언할 수 없습니다: " + prefabId);
            Components = Array.AsReadOnly(modules);
            foreach (string dependent in new[] { FeatureScripts.Closeup, FeatureScripts.Door, FeatureScripts.Dial, FeatureScripts.Console })
                if (Has(dependent) && !Has(FeatureScripts.Interactable)) throw new InvalidOperationException(dependent + "에는 Interactable이 필요합니다.");
            if ((Has(FeatureScripts.Dial) || Has(FeatureScripts.Console)) && !Has(FeatureScripts.Closeup))
                throw new InvalidOperationException("입력 기능에는 CloseUpUI가 필요합니다.");
        }
        public int[] CloseupCameraOffsetMm()
        {
            var component = Components.SingleOrDefault(row => row.Script == FeatureScripts.Closeup);
            if (component == null) throw new InvalidOperationException("CloseUpUI 선언이 없습니다.");
            string raw = ComponentParameters.Read(component.Params, "CamOffset");
            string[] values = raw.Split(',');
            if (values.Length != 3) throw new InvalidOperationException("CloseUpUI CamOffset은 x,y,z 미터 값 3개여야 합니다.");
            var result = values.Select(value =>
            {
                if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double number) || double.IsNaN(number) || double.IsInfinity(number))
                    throw new InvalidOperationException("CamOffset은 유한한 숫자여야 합니다.");
                double mm = Math.Round(number * 1000, MidpointRounding.AwayFromZero);
                if (Math.Abs(mm) > 100000) throw new InvalidOperationException("CamOffset은 ±100000mm 이하여야 합니다.");
                return (int)mm;
            }).ToArray();
            if (result.All(value => value == 0)) throw new InvalidOperationException("CamOffset을 0,0,0으로 지정할 수 없습니다.");
            return result;
        }
    }
    public static class ComponentParameters
    {
        public static string Read(string text, string key)
        {
            var entries = text.Split(';').Select(item => item.Split(new[] { '=' }, 2)).ToArray();
            if (entries.Any(item => item.Length != 2 || string.IsNullOrWhiteSpace(item[0])) || entries.Select(item => item[0].Trim()).Distinct(StringComparer.Ordinal).Count() != entries.Length)
                throw new InvalidOperationException("기능 Params는 중복 없는 Key=Value;Key=Value 형식이어야 합니다.");
            return entries.SingleOrDefault(item => item[0].Trim() == key)?[1].Trim() ?? throw new InvalidOperationException("기능 Params에 " + key + "가 없습니다.");
        }
    }
}
