using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EscapeGame.Data.Csv;
using EscapeGame.Data.CsvValidation.Contracts;

namespace EscapeGame.Data
{
    // 한 번 읽은 CSV 원본을 보존합니다. 검증과 데이터 클래스 변환은 이 동일한 인스턴스를 사용합니다.
    public sealed class CsvDataSet
    {
        public IReadOnlyDictionary<string, CsvDocument> Documents
        {
            get;
        }
        public IReadOnlyList<CsvValidationIssue> LoadIssues
        {
            get;
        }
        public int FileCount
        {
            get;
        }

        public CsvDataSet(IDictionary<string, CsvDocument> documents, IEnumerable<CsvValidationIssue> loadIssues = null, int? fileCount = null)
        {
            if (documents == null)
                throw new ArgumentNullException(nameof(documents));
            Documents = new ReadOnlyDictionary<string, CsvDocument>(new Dictionary<string, CsvDocument>(documents, StringComparer.Ordinal));
            if (Documents.Any(x => string.IsNullOrWhiteSpace(x.Key) || x.Value == null))
                throw new ArgumentException("Invalid CSV document", nameof(documents));
            LoadIssues = Array.AsReadOnly((loadIssues ?? Enumerable.Empty<CsvValidationIssue>()).ToArray());
            FileCount = fileCount ?? Documents.Count;
            if (FileCount < Documents.Count)
                throw new ArgumentOutOfRangeException(nameof(fileCount));
        }

        public CsvDocument GetDocument(string sheet) => Documents.TryGetValue(sheet, out var document)
            ? document : throw new KeyNotFoundException("CSV table not loaded: " + sheet);

        // 입력 위치는 호출자가 결정합니다. 파일 경로와 TextAsset 이름 등 기존 ICsvTextSource를 그대로 사용할 수 있습니다.
        public static async Task<CsvDataSet> LoadAsync(IEnumerable<KeyValuePair<string, string>> sources, CsvLoader loader, CancellationToken token)
        {
            if (sources == null)
                throw new ArgumentNullException(nameof(sources));
            if (loader == null)
                throw new ArgumentNullException(nameof(loader));
            var documents = new Dictionary<string, CsvDocument>(StringComparer.Ordinal);
            var names = new HashSet<string>(StringComparer.Ordinal);
            var issues = new List<CsvValidationIssue>();
            int fileCount = 0;
            foreach (var source in sources.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                token.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(source.Key) || !names.Add(source.Key))
                    throw new ArgumentException("Empty or duplicate CSV table name: " + source.Key, nameof(sources));
                fileCount++;
                try
                {
                    documents.Add(source.Key, await loader.LoadAsync(source.Value, token));
                }
                catch (CsvParseException error)
                {
                    issues.Add(new CsvValidationIssue(CsvValidationCode.ParseError, source.Key, error.LineNumber,
                        error.FieldNumber.ToString(CultureInfo.InvariantCulture), CsvValidationSeverity.Error, error.Code.ToString()));
                }
                catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is DecoderFallbackException)
                {
                    issues.Add(new CsvValidationIssue(CsvValidationCode.ReadFailed, source.Key, 1, "", CsvValidationSeverity.Error, error.GetType().Name));
                }
            }
            token.ThrowIfCancellationRequested();
            return new CsvDataSet(documents, issues, fileCount);
        }
    }
}
