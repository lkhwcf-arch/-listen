using System.Collections.Generic;
using EscapeGame.Data.Csv;

namespace EscapeGame.Data.Tables
{
    // REGISTRY.csv의 한 행을 나타내는 읽기 전용 정의입니다.
    public sealed class RegistryData
    {
        public static CsvTableMapping<RegistryData> Table { get; } = new CsvTableMapping<RegistryData>
            ("REGISTRY", row => new RegistryData(row));
        public int Id { get; }
        public string Sheet { get; }
        public string File { get; }
        public IReadOnlyList<string> Parent { get; }
        public IReadOnlyList<string> ParentKey { get; }
        public IReadOnlyList<string> Uses { get; }
        public int LoadOrder { get; }
        public bool Required { get; }
        public bool Active { get; }

        public RegistryData(CsvRow row)
        {
            var value = new CsvRowReader(row, Table.SheetName);
            Id = value.Integer("ID");
            Sheet = value.Text("Sheet");
            File = value.Text("File");
            Parent = value.List("Parent", dashMeansEmpty: true);
            ParentKey = value.List("ParentKey", dashMeansEmpty: true);
            Uses = value.List("Uses", dashMeansEmpty: true);
            LoadOrder = value.Integer("LoadOrder");
            Required = value.Flag("Required");
            Active = value.Flag("Active");
        }
    }
}
