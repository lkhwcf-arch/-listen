using System;
using System.Threading;
using System.Threading.Tasks;
using EscapeGame.Data;
using EscapeGame.Session;
using EscapeGame.Session.Contracts;

namespace EscapeGame.Networking
{
    // 전송 오류만 코어 오류로 변환합니다. 데이터 변환은 SessionApiClient 하나가 담당합니다.
    internal sealed class HttpSessionGateway : ISessionGateway, ISessionContentGateway, ISessionRoomGateway
    {
        private readonly SessionApiClient api;
        internal HttpSessionGateway(SessionApiClient api) => this.api = api ?? throw new ArgumentNullException(nameof(api));
        public Task<SessionJoinData> JoinAsync(CancellationToken token) => ExecuteAsync(() => api.JoinAsync(token));
        public Task<SessionSnapshotData> ReportSceneLoadedAsync(string id, string token, CancellationToken ct) => ExecuteAsync(() => api.ReportSceneLoadedAsync(id, token, ct));
        public Task<SessionSnapshotData> QueryAsync(string id, string token, CancellationToken ct) => ExecuteAsync(() => api.QuerySessionAsync(id, token, ct));
        public Task HeartbeatAsync(string id, string token, CancellationToken ct) => ExecuteAsync(() => api.HeartbeatAsync(id, token, ct));
        public Task LeaveAsync(string id, string token, CancellationToken ct) => ExecuteAsync(() => api.LeaveAsync(id, token, ct));
        public Task<CsvDataSet> DownloadDataAsync(string id, string token, CancellationToken ct) => ExecuteAsync(() => api.DownloadDataAsync(id, token, ct));
        public Task<SessionPuzzleSubmissionData> SubmitPuzzleAsync(string id, string token, int puzzleId, string[] answers, CancellationToken ct)
            => ExecuteAsync(() => api.SubmitPuzzleAsync(id, token, puzzleId, answers, ct));
        public Task<SessionRoomDecisionData> DecideRoomAsync(string id, string token, int roomNo, string[] answers, CancellationToken ct)
            => ExecuteAsync(() => api.DecideRoomAsync(id, token, roomNo, answers, ct));
        private static async Task ExecuteAsync(Func<Task> operation)
        {
            await ExecuteAsync(async () =>
            {
                await operation();
                return true;
            });
        }
        private static async Task<T> ExecuteAsync<T>(Func<Task<T>> operation)
        {
            try
            {
                return await operation();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (SessionResultException)
            {
                throw;
            }
            catch (SessionTransportException error)
            {
                throw new SessionResultException(SessionFailureCode.ConnectionFailed, error);
            }
            catch (Exception error) when (error is ArgumentException || error is InvalidOperationException)
            {
                throw new SessionResultException(SessionFailureCode.InvalidResponse, error);
            }
        }
    }
}
