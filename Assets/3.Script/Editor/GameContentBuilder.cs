#if UNITY_EDITOR
using System;
using System.Linq;
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
        public int callbackOrder => 0;
        public void OnPreprocessBuild(BuildReport report) => Rebuild();
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] oldPaths)
        {
            if (imported.Concat(deleted).Concat(moved).Concat(oldPaths).Any(path =>
                path.StartsWith("Assets/11.CSV/", StringComparison.Ordinal) ||
                path.StartsWith("Assets/2.Model/", StringComparison.Ordinal) && path.EndsWith(".prefab", StringComparison.Ordinal) ||
                path.EndsWith("CsvValidationRules.xml", StringComparison.Ordinal)))
                EditorApplication.delayCall += Rebuild;
        }
        [MenuItem("Tools/Game/Rebuild Content References")]
        public static void Rebuild()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            var asset = AssetDatabase.LoadAssetAtPath<GameContentAssets>(AssetPath);
            if (asset == null)
            {
                asset = UnityEngine.ScriptableObject.CreateInstance<GameContentAssets>();
                AssetDatabase.CreateAsset(asset, AssetPath);
            }
            var serialized = new SerializedObject(asset);
            var csv = AssetDatabase.FindAssets("t:TextAsset", new[] { "Assets/11.CSV" }).Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)).OrderBy(path => path, StringComparer.Ordinal).ToArray();
            var files = serialized.FindProperty("csvFiles");
            files.arraySize = csv.Length;
            for (int i = 0; i < csv.Length; i++) files.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<UnityEngine.TextAsset>(csv[i]);
            serialized.FindProperty("validationRules").objectReferenceValue = AssetDatabase.LoadAssetAtPath<UnityEngine.TextAsset>(
                "Assets/3.Script/Data/CsvValidation/Configuration/CsvValidationRules.xml");
            var paths = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/2.Model" }).Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path, StringComparer.Ordinal).ToArray();
            var prefabs = serialized.FindProperty("prefabs");
            prefabs.arraySize = paths.Length;
            for (int i = 0; i < paths.Length; i++)
            {
                var entry = prefabs.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("path").stringValue = paths[i];
                entry.FindPropertyRelative("prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(paths[i]);
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
        }
    }
}
#endif
