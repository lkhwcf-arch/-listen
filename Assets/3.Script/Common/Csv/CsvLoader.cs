using System;
using System.Threading;
using System.Threading.Tasks;

namespace EscapeGame.Data.Csv
{
    // 파일 읽기와 파싱을 조립합니다. 콘텐츠 초기화와 전역 상태는 소유하지 않습니다.
    public sealed class CsvLoader
    {
        private readonly ICsvTextSource source;
        private readonly CsvParser parser;
        public CsvLoader(ICsvTextSource source, CsvParser parser)
        {
            this.source = source ?? throw new ArgumentNullException(nameof(source));
            this.parser = parser ?? throw new ArgumentNullException(nameof(parser));
        }

        public async Task<CsvDocument> LoadAsync(string sourceName, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(sourceName)) throw new ArgumentException("CSV 경로 또는 이름이 필요합니다.", nameof(sourceName));
            token.ThrowIfCancellationRequested();
            string text = await source.ReadTextAsync(sourceName, token);
            token.ThrowIfCancellationRequested();
            var document = parser.Read(text, sourceName);
            token.ThrowIfCancellationRequested();
            return document;
        }
    }
}
