using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EscapeGame.Data.Csv;
using EscapeGame.Data.CsvValidation.Runtime;
using EscapeGame.Data.CsvValidation.Schema;
using UnityEngine;

namespace EscapeGame.Data
{
    // 에디터가 원본 CSV와 프리팹 참조를 수집합니다. 플레이어 빌드는 AssetDatabase에 의존하지 않습니다.
    public sealed class GameContentAssets : ScriptableObject
    {
        [Serializable] public sealed class PrefabBinding { public string path; public GameObject prefab; }
        [SerializeField] private TextAsset[] csvFiles;
        [SerializeField] private TextAsset validationRules;
        [SerializeField] private PrefabBinding[] prefabs;

        public CsvValidator CreateValidator() => new CsvValidator(CsvValidationCatalog.Read(validationRules.text));
        public CsvDataSet ReadLocalData()
        {
            var parser = new CsvParser();
            var encoding = new UTF8Encoding(false, true);
            return new CsvDataSet(csvFiles.ToDictionary(asset => asset.name,
                asset => parser.Read(encoding.GetString(asset.bytes), asset.name), StringComparer.Ordinal));
        }
        public GameObject ResolvePrefab(string path)
        {
            string normalized = Normalize(path);
            var binding = prefabs.FirstOrDefault(entry => Normalize(entry.path) == normalized);
            return binding?.prefab != null ? binding.prefab
                : throw new InvalidOperationException("빌드에 등록된 프리팹이 없습니다: " + path);
        }
        private static string Normalize(string path) => path.Replace('\\', '/').Replace(".prefab", "");
    }
}
