using EscapeGame.Data.Csv;
using EscapeGame.Data;
using EscapeGame.Data.CsvValidation.Contracts;
using EscapeGame.Data.CsvValidation.Runtime;
using EscapeGame.Data.CsvValidation.Schema;

internal sealed class TestValidator
{
    public CsvValidationCatalog Catalog { get; }
    public TestValidator(string xml) => Catalog = CsvValidationCatalog.Read(xml);
    public CsvDataSet Input(IReadOnlyDictionary<string, string> sources)
    {
        var documents = new Dictionary<string, CsvDocument>(StringComparer.Ordinal);
        var issues = new List<CsvValidationIssue>();
        foreach (var source in sources)
        {
            try { documents.Add(source.Key, new CsvParser().Read(source.Value, source.Key)); }
            catch (CsvParseException error)
            {
                issues.Add(new CsvValidationIssue(CsvValidationCode.ParseError, source.Key, error.LineNumber,
                    error.FieldNumber.ToString(), CsvValidationSeverity.Error, error.Code.ToString()));
            }
        }
        return new CsvDataSet(documents, issues, sources.Count);
    }
    public IReadOnlyList<CsvValidationIssue> Validate(IReadOnlyDictionary<string, string> sources)
        => new CsvValidator(Catalog).Validate(Input(sources)).Issues;
}
