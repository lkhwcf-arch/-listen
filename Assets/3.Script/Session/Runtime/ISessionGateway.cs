using System.Threading;
using System.Threading.Tasks;

namespace EscapeGame.Session
{
    // 세션 처리에서 요구하는 통신 포트입니다. HTTP/JSON/Unity 타입을 노출하지 않습니다.
    internal interface ISessionGateway
    {
        Task<SessionJoinData> JoinAsync(CancellationToken token);
        Task<SessionSnapshotData> ReportSceneLoadedAsync(string sessionId, string participantToken, CancellationToken token);
        Task<SessionSnapshotData> QueryAsync(string sessionId, string participantToken, CancellationToken token);
    }

    internal interface ISessionGatewayFactory
    {
        ISessionGateway Create(string serverAddress);
    }
}
