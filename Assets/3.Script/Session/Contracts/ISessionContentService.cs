using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EscapeGame.Data;

namespace EscapeGame.Session.Contracts
{
    // 콘텐츠는 공개 세션 계약을 통해 요청하며 참가 토큰과 HTTP DTO를 노출하지 않습니다.
    public interface ISessionContentService
    {
        event Action<IReadOnlyList<int>> RoomsChanged;
        Task<CsvDataSet> DownloadDataAsync(CancellationToken token);
        Task<bool> SubmitPuzzleAsync(int puzzleId, string[] answers, CancellationToken token);
    }
}
