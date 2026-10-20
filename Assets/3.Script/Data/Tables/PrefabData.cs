using EscapeGame.Data.Csv;

namespace EscapeGame.Data.Tables
{
    // PREFAB.csv의 한 행을 나타내는 읽기 전용 정의입니다.
    public sealed class PrefabData
    {
        public static CsvTableMapping<PrefabData> Table { get; } = new CsvTableMapping<PrefabData>
            ("PREFAB", row => new PrefabData(row));
        public int Id { get; }
        public string Path { get; }
        public string Group { get; }
        public System.Collections.Generic.IReadOnlyList<int> Components { get; }

        public PrefabData(CsvRow row)
        {
            var value = new CsvRowReader(row, Table.SheetName);
            Id = value.Integer("ID");
            Path = value.Text("Path");
            Group = value.Text("Group");
            Components = value.IdList("Components", '|');
        }
    }
}
