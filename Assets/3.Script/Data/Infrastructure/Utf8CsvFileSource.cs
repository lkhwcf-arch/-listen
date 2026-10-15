using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EscapeGame.Data.Csv;

namespace EscapeGame.Data.Infrastructure
{
    // Unity와 독립적인 데스크톱 파일 어댑터입니다. 디코딩 실패를 조용히 치환하지 않습니다.
    public sealed class Utf8CsvFileSource : ICsvTextSource
    {
        public async Task<string> ReadTextAsync(string filePath, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read,
                4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
            using var bytes = new MemoryStream();
            await stream.CopyToAsync(bytes, 81920, token);
            token.ThrowIfCancellationRequested();
            return new UTF8Encoding(false, true).GetString(bytes.ToArray());
        }
    }
}
