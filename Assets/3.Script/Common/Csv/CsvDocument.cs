using System.Collections.Generic;

namespace EscapeGame.Data.Csv
{
    // 실제 컬럼을 정하지 않은 읽기 전용 로딩 결과입니다.
    public sealed class CsvDocument
    {
        public string SourceName
        {
            get;
        }
        public IReadOnlyList<string> Headers
        {
            get;
        }
        public IReadOnlyList<CsvRow> Rows
        {
            get;
        }
        internal CsvDocument(string sourceName, IEnumerable<string> headers, IEnumerable<CsvRow> rows)
        {
            SourceName = sourceName;
            Headers = new List<string>(headers).AsReadOnly();
            Rows = new List<CsvRow>(rows).AsReadOnly();
        }
    }
}
