#if UNITY_EDITOR
using System;
using System.Linq;
using System.IO;
using EscapeGame.Data.Csv;
using EscapeGame.Data.Tables;
using EscapeGame.Data.CsvValidation.Runtime;
using EscapeGame.Data.CsvValidation.Schema;
using EscapeGame.Data;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace EscapeGame.Editor
{
    // 새 CSV/프리팹 추가와 빌드 시 참조 목록을 갱신합니다. 런타임에는 이 에디터 코드를 사용하지 않습니다.
    public sealed class GameContentBuilder : AssetPostprocessor, IPreprocessBuildWithReport
    {
        private const string AssetPath = "Assets/Resources/GameContent.asset";
        private static bool queued;
        [InitializeOnLoadMethod] private static void OnLoaded() => QueueRebuild();
        public int callbackOrder => 0;
        public void OnPreprocessBuild(BuildReport report) => Rebuild();
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] oldPaths)
        {
            if (imported.Concat(deleted).Concat(moved).Concat(oldPaths).Any(path =>
                path.StartsWith("Assets/11.CSV/", StringComparison.Ordinal) ||
                path.StartsWith("Assets/2.Model/", StringComparison.Ordinal) && path.EndsWith(".prefab", StringComparison.Ordinal) ||
                path.EndsWith("CsvValidationRules.xml", StringComparison.Ordinal)))
                QueueRebuild();
        }
        private static void QueueRebuild()
        {
            if (queued)
                return;
            queued = true;
            EditorApplication.delayCall += Rebuild;
        }
        [MenuItem("Tools/Game/Rebuild Content References")]
        public static void Rebuild()
        {
            queued = false;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            var asset = AssetDatabase.LoadAssetAtPath<GameContentAssets>(AssetPath);
            if (asset == null)
            {
                asset = UnityEngine.ScriptableObject.CreateInstance<GameContentAssets>();
                AssetDatabase.CreateAsset(asset, AssetPath);
            }
            var serialized = new SerializedObject(asset);
            var csv = AssetDatabase.FindAssets("t:TextAsset", new[] { "Assets/11.CSV" }).Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)).OrderBy(path => path, StringComparer.Ordinal).ToArray();
            var parser = new CsvParser();
            var source = new CsvDataSet(csv.ToDictionary(path => Path.GetFileNameWithoutExtension(path), path => parser.Read(File.ReadAllText(path), path), StringComparer.Ordinal));
            var rulesPath = "Assets/3.Script/Data/CsvValidation/Configuration/CsvValidationRules.xml";
            var validator = new CsvValidator(CsvValidationCatalog.Read(File.ReadAllText(rulesPath)));
            var store = new CsvDataStore();
            if (!store.TryRegister(source, validator, out var validation))
                throw new BuildFailedException("콘텐츠 검증 실패: " + string.Join("\n", validation.Issues.Where(issue => issue.Severity == EscapeGame.Data.CsvValidation.Contracts.CsvValidationSeverity.Error)));
            var data = new CsvGameData(store);
            var published = new System.Collections.Generic.HashSet<string>(data.Registry.Where(row => row.Active).Select(row => row.Sheet), StringComparer.Ordinal);
            csv = csv.Where(path => published.Contains(Path.GetFileNameWithoutExtension(path))).ToArray();
            var settings = UnityEngine.Resources.Load<EscapeGame.Player.PlayerPresentationSettings>("PlayerPresentation");
            var paths = data.Prefabs.Where(row => settings == null || row.Id != settings.SpawnPrefabId)
                .Select(row => row.Path.Replace('\\', '/')).Select(path => path.EndsWith(".prefab", StringComparison.Ordinal) ? path : path + ".prefab")
                .Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal).ToArray();
            foreach (var path in paths)
                if (AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(path) == null)
                    throw new BuildFailedException("등록 프리팹이 없습니다: " + path);
            var files = serialized.FindProperty("csvFiles");
            files.arraySize = csv.Length;
            for (int i = 0; i < csv.Length; i++)
                files.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<UnityEngine.TextAsset>(csv[i]);
            serialized.FindProperty("validationRules").objectReferenceValue = AssetDatabase.LoadAssetAtPath<UnityEngine.TextAsset>(rulesPath);
            var prefabs = serialized.FindProperty("prefabs");
            prefabs.arraySize = paths.Length;
            for (int i = 0; i < paths.Length; i++)
            {
                var entry = prefabs.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("path").stringValue = paths[i];
                entry.FindPropertyRelative("prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(paths[i]);
            }
            if (serialized.ApplyModifiedPropertiesWithoutUndo())
            {
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssetIfDirty(asset);
            }
        }
    }
}
#endif
