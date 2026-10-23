using System;
using System.IO;
using UnityEngine;

namespace EscapeGame.Configuration
{
    // 현재 Windows/Linux 플레이어용 파일 입력과 Unity 환경 판별 어댑터입니다.
    public static class UnityClientConnectionConfig
    {
        public const string RelativePath = "Configuration/connection.json";
        public static string FilePath => Path.Combine(Application.streamingAssetsPath, RelativePath);
        public static ClientExecutionEnvironment CurrentEnvironment => Application.isEditor
            ? ClientExecutionEnvironment.Editor
            : Debug.isDebugBuild ? ClientExecutionEnvironment.TestBuild : ClientExecutionEnvironment.StableBuild;

        public static ClientConnectionSettings Load() => Read(FilePath, CurrentEnvironment);
        public static ClientConnectionSettings Read(string path, ClientExecutionEnvironment environment)
        {
            try
            {
                var document = JsonUtility.FromJson<ClientConnectionDocument>(File.ReadAllText(path));
                return ClientConnectionSettings.Select(document, environment);
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException ||
                error is ArgumentException || error is InvalidOperationException)
            {
                throw new InvalidOperationException($"접속 설정 오류 ({path}): {error.Message}", error);
            }
        }
    }
}
