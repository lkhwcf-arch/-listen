using System;
using System.Threading;
using System.Threading.Tasks;

namespace EscapeGame.Configuration
{
    // 자동 입장과 재시도 버튼은 같은 명령을 사용합니다. 실제 요청 수명은 기존 세션 모듈이 소유합니다.
    public sealed class ConfiguredServerConnection
    {
        private readonly ClientConnectionSettings settings;
        private readonly Func<string, CancellationToken, Task> connect;
        private bool started;
        private bool running;

        public ConfiguredServerConnection(ClientConnectionSettings settings, Func<string, CancellationToken, Task> connect)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.connect = connect ?? throw new ArgumentNullException(nameof(connect));
        }
        public Task StartAsync(CancellationToken token)
        {
            if (started)
                return Task.CompletedTask;
            started = true;
            return settings.AutoConnect ? ConnectAsync(token) : Task.CompletedTask;
        }
        public async Task ConnectAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (running)
                return;
            running = true;
            try
            {
                await connect(settings.ServerBaseUrl, token);
            }
            finally
            {
                running = false;
            }
        }
    }
}
