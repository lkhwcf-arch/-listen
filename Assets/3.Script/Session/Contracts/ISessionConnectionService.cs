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
        Task<SessionCommandResult> JoinAsync(string serverAddress, CancellationToken token);
        Task<SessionCommandResult> ReportSceneLoadedAsync(CancellationToken token);
        Task<SessionCommandResult> QueryAsync(CancellationToken token);
    }
}
