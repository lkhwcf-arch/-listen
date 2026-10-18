using System.Threading;
using System.Threading.Tasks;
using EscapeGame.Data;

namespace EscapeGame.Session
{
    internal interface ISessionContentGateway
    {
        Task<CsvDataSet> DownloadDataAsync(string sessionId, string participantToken, CancellationToken token);
        Task<SessionPuzzleSubmissionData> SubmitPuzzleAsync(string sessionId, string participantToken, int puzzleId, string[] answers, CancellationToken token);
    }
}
