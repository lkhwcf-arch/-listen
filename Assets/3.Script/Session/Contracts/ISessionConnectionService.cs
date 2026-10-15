using System;
using System.Threading;
using System.Threading.Tasks;

namespace EscapeGame.Session.Contracts
{
    public interface ISessionConnectionService
    {
        SessionInfo State { get; }
        bool IsBusy { get; }
        string ServerBaseUrl { get; }
        event Action<SessionInfo> StateChanged;
        event Action<ClientSessionPhase?, ClientSessionPhase> PhaseChanged;
        event Action<SessionFailureCode> BackgroundFailed;
        event Action<SessionFailureCode> Disconnected;
        Task<SessionCommandResult> LeaveAsync(CancellationToken token);
        Task<SessionCommandResult> HeartbeatAsync(CancellationToken token);
        // 로컬 연결을 종료합니다. 서버의 정상 퇴장 API와는 별도입니다.
        void Disconnect(SessionFailureCode reason);
        Task<SessionCommandResult> JoinAsync(string serverAddress, CancellationToken token);
        Task<SessionCommandResult> ReportSceneLoadedAsync(CancellationToken token);
        Task<SessionCommandResult> QueryAsync(CancellationToken token);
    }
}
