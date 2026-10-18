using EscapeGame.Data.Csv;

namespace EscapeGame.Data.Tables
{
    // COMPONENT.csv의 한 행을 나타내는 읽기 전용 정의입니다.
    public sealed class ComponentData
    {
        public static CsvTableMapping<ComponentData> Table { get; } = new CsvTableMapping<ComponentData>
            ("COMPONENT", row => new ComponentData(row));
        public int Id { get; }
        public string Script { get; }
        public string Params { get; }

        public ComponentData(CsvRow row)
        {
            var value = new CsvRowReader(row, Table.SheetName);
            Id = value.Integer("ID");
            Script = value.Text("Script");
            Params = value.Text("Params");
        }
    }
}
