#if UNITY_EDITOR
using EscapeGame.Configuration;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace EscapeGame.Editor
{
    public sealed class ClientConnectionBuildValidator : IPreprocessBuildWithReport
    {
        public int callbackOrder => -100;
        public void OnPreprocessBuild(BuildReport report)
        {
            var environment = (report.summary.options & BuildOptions.Development) != 0
                ? ClientExecutionEnvironment.TestBuild : ClientExecutionEnvironment.StableBuild;
            try
            {
                var settings = UnityClientConnectionConfig.Read(UnityClientConnectionConfig.FilePath, environment);
                Debug.Log($"클라이언트 접속 설정: {environment}, {settings.ServerBaseUrl}. Join은 탈출하기 버튼으로 입장합니다. 호환 테스트 화면 autoConnect={settings.AutoConnect}");
            }
            catch (System.Exception error)
            {
                throw new BuildFailedException(error.Message);
            }
        }
    }
}
#endif
