using System;
using EscapeGame.Networking;

namespace EscapeGame.Configuration
{
    public enum ClientExecutionEnvironment { Editor = 0, TestBuild = 1, StableBuild = 2 }

    [Serializable]
    public sealed class ClientConnectionProfile
    {
        public string serverBaseUrl;
        public bool autoConnect;
    }

    [Serializable]
    public sealed class ClientConnectionDocument
    {
        public int schemaVersion;
        public ClientConnectionProfile editor;
        public ClientConnectionProfile testBuild;
        public ClientConnectionProfile stableBuild;
    }

    // 변경 가능한 JSON 필드와 실제 소비자가 사용하는 불변 설정을 분리합니다.
    public sealed class ClientConnectionSettings
    {
        public ClientExecutionEnvironment Environment { get; }
        public string ServerBaseUrl { get; }
        public bool AutoConnect { get; }
        private ClientConnectionSettings(ClientExecutionEnvironment environment, ClientConnectionProfile profile)
        {
            Environment = environment;
            ServerBaseUrl = HttpServerAddress.Normalize(profile.serverBaseUrl);
            AutoConnect = profile.autoConnect;
        }

        public static ClientConnectionSettings Select(ClientConnectionDocument document, ClientExecutionEnvironment environment)
        {
            if (document == null || document.schemaVersion != 1)
                throw new InvalidOperationException("접속 config의 schemaVersion은 1이어야 합니다.");
            var profile = environment switch
            {
                ClientExecutionEnvironment.Editor => document.editor,
                ClientExecutionEnvironment.TestBuild => document.testBuild,
                ClientExecutionEnvironment.StableBuild => document.stableBuild,
                _ => throw new ArgumentOutOfRangeException(nameof(environment))
            };
            if (profile == null) throw new InvalidOperationException($"접속 config에 {environment} 설정이 없습니다.");
            try { return new ClientConnectionSettings(environment, profile); }
            catch (ArgumentException error)
            { throw new InvalidOperationException($"{environment} 서버 주소를 config에 등록하세요. {error.Message}", error); }
        }
    }
}
