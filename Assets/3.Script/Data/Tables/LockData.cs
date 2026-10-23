using System.Collections.Generic;
using EscapeGame.Data.Csv;

namespace EscapeGame.Data.Tables
{
    // LOCK.csv의 한 행을 나타내는 읽기 전용 정의입니다.
    public sealed class LockData
    {
        public static CsvTableMapping<LockData> Table
        {
            get;
        } = new CsvTableMapping<LockData>
            ("LOCK", row => new LockData(row));
        public int AnchorRef
        {
            get;
        }
        public string DialFace
        {
            get;
        }
        public IReadOnlyList<string> DialLetters
        {
            get;
        }
        public string DisplayFace
        {
            get;
        }

        public LockData(CsvRow row)
        {
            var value = new CsvRowReader(row, Table.SheetName);
            AnchorRef = value.Integer("AnchorRef");
            DialFace = value.Text("DialFace");
            DialLetters = value.List("DialLetters");
            DisplayFace = value.Text("DisplayFace");
        }
    }
}
