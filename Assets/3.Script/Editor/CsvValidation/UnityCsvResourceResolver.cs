using System;
using System.Collections.Generic;
using System.IO;
using EscapeGame.Data.CsvValidation.Runtime;
using EscapeGame.Data.CsvValidation.Schema;
using UnityEditor;
using UnityEngine;

namespace EscapeGame.Editor
{
    internal sealed class UnityCsvResourceResolver : ICsvResourceResolver
    {
        private readonly Dictionary<CsvResourceKind, HashSet<string>> paths = new Dictionary<CsvResourceKind, HashSet<string>>
        {
            [CsvResourceKind.Prefab] = new HashSet<string>(StringComparer.Ordinal),
            [CsvResourceKind.Audio] = new HashSet<string>(StringComparer.Ordinal)
        };
        public UnityCsvResourceResolver()
        {
            foreach (string asset in AssetDatabase.GetAllAssetPaths())
            {
                if (!Path.HasExtension(asset)) continue;
                CsvResourceKind kind = asset.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) ? CsvResourceKind.Prefab :
                    AssetDatabase.GetMainAssetTypeAtPath(asset) == typeof(AudioClip) ? CsvResourceKind.Audio : CsvResourceKind.None;
                if (kind == CsvResourceKind.None) continue;
                var set = paths[kind];
                string withoutExtension = asset.Substring(0, asset.Length - Path.GetExtension(asset).Length);
                set.Add(asset); set.Add(withoutExtension);
                const string marker = "/Resources/";
                int index = asset.LastIndexOf(marker, StringComparison.Ordinal);
                if (index < 0) continue;
                set.Add(asset.Substring(index + marker.Length));
                set.Add(withoutExtension.Substring(index + marker.Length));
            }
        }
        public bool Exists(string path, CsvResourceKind kind) => paths.TryGetValue(kind, out var set) && set.Contains(path);
    }
}
