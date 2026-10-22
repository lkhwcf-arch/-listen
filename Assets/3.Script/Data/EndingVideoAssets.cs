using System;
using UnityEngine;
using UnityEngine.Video;

namespace EscapeGame.Data
{
    // GAME_CONFIG 16011 경로와 빌드에 포함할 엔딩 영상을 연결합니다. Resources/EndingVideo 에셋 하나만 사용합니다.
    public sealed class EndingVideoAssets : ScriptableObject
    {
        public const int ConfigId = 16011;
        [SerializeField] private string path;
        [SerializeField] private VideoClip clip;
        public VideoClip Resolve(string configPath)
        {
            if (clip == null) throw new InvalidOperationException("Resources/EndingVideo에 엔딩 영상이 연결되지 않았습니다.");
            if (Normalize(configPath) != Normalize(path))
                throw new InvalidOperationException("GAME_CONFIG 16011 경로와 등록된 엔딩 영상이 다릅니다: " + configPath + " / " + path);
            return clip;
        }
        // Assets/ 접두어, 구분자, 확장자 차이는 같은 경로로 봅니다.
        private static string Normalize(string value)
        {
            value = (value ?? "").Trim().Replace('\\', '/');
            if (value.StartsWith("Assets/", StringComparison.Ordinal)) value = value.Substring("Assets/".Length);
            int dot = value.LastIndexOf('.');
            return dot > value.LastIndexOf('/') ? value.Substring(0, dot) : value;
        }
    }
}
