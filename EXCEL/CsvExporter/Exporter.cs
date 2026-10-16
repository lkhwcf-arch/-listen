using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace SilentCsv
{
    // Reads Open XML directly. Excel and third-party libraries are not required.
    public static class Exporter
    {
        private sealed class Cell
        {
            public string Value;
            public string Error;
        }

        private sealed class Table
        {
            public string Name;
            public string Source;
            public string Content;
            public int Rows;
            public int Columns;
            public string[] Excluded;
        }

        private static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static readonly XNamespace Rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        private static readonly XNamespace PackageRel = "http://schemas.openxmlformats.org/package/2006/relationships";

        public static int Export(string inputDirectory, string outputDirectory, bool validateOnly)
        {
            inputDirectory = Path.GetFullPath(inputDirectory);
            outputDirectory = Path.GetFullPath(outputDirectory);
            if (!Directory.Exists(inputDirectory))
                throw new InvalidOperationException("엑셀 폴더를 찾을 수 없습니다: " + inputDirectory);

            string[] files = Directory.GetFiles(inputDirectory)
                .Where(p => !Path.GetFileName(p).StartsWith("~$", StringComparison.Ordinal)
                    && (Path.GetExtension(p).Equals(".xlsx", StringComparison.OrdinalIgnoreCase)
                        || Path.GetExtension(p).Equals(".xlsm", StringComparison.OrdinalIgnoreCase)))
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray();
            if (files.Length == 0)
                throw new InvalidOperationException("추출할 .xlsx 또는 .xlsm 파일이 없습니다: " + inputDirectory);

            var tables = new List<Table>();
            var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string file in files)
            {
                foreach (Table table in ReadWorkbook(file))
                {
                    string previous;
                    if (names.TryGetValue(table.Name, out previous))
                        throw new InvalidOperationException("CSV 이름 중복: " + table.Name + ".csv (" + previous + ", " + table.Source + ")");
                    names.Add(table.Name, table.Source);
                    tables.Add(table);
                }
            }
            if (tables.Count == 0)
                throw new InvalidOperationException("추출 가능한 시트가 없습니다.");

            // Validate every workbook before changing any existing output.
            if (!validateOnly) WriteBatch(tables, outputDirectory);
            foreach (Table table in tables)
            {
                Console.WriteLine("{0}.csv : {1}개 컬럼, 데이터 {2}행 / 제외: {3}",
                    table.Name, table.Columns, table.Rows, string.Join(", ", table.Excluded));
            }
            Console.WriteLine();
            Console.WriteLine("{0}: 엑셀 {1}개 → CSV {2}개, 데이터 총 {3}행",
                validateOnly ? "검증 완료" : "추출 완료", files.Length, tables.Count, tables.Sum(t => t.Rows));
            Console.WriteLine("저장 위치: " + outputDirectory);
            return tables.Count;
        }

        private static List<Table> ReadWorkbook(string path)
        {
            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
                {
                    XDocument workbook = ReadXml(archive, "xl/workbook.xml");
                    XDocument relationships = ReadXml(archive, "xl/_rels/workbook.xml.rels");
                    var targets = relationships.Root.Elements(PackageRel + "Relationship")
                        .ToDictionary(e => (string)e.Attribute("Id"), e => e, StringComparer.Ordinal);
                    var shared = new List<string>();
                    if (archive.GetEntry("xl/sharedStrings.xml") != null)
                        shared = ReadXml(archive, "xl/sharedStrings.xml").Root.Elements(Main + "si")
                            .Select(TextValue).ToList();

                    var result = new List<Table>();
                    foreach (XElement sheet in workbook.Root.Element(Main + "sheets").Elements(Main + "sheet"))
                    {
                        string name = (string)sheet.Attribute("name");
                        if (name == "_README" || name == "_COLUMNS") continue;
                        CheckFilename(name);
                        string id = (string)sheet.Attribute(Rel + "id");
                        XElement relation;
                        if (id == null || !targets.TryGetValue(id, out relation)
                            || (string)relation.Attribute("TargetMode") == "External"
                            || !((string)relation.Attribute("Type") ?? "").EndsWith("/worksheet", StringComparison.Ordinal))
                            throw new InvalidOperationException(name + ": 워크시트 연결이 올바르지 않습니다.");
                        var target = new Uri(new Uri("http://xlsx.local/xl/workbook.xml"), (string)relation.Attribute("Target"));
                        string entry = Uri.UnescapeDataString(target.AbsolutePath.TrimStart('/'));
                        result.Add(ReadTable(ReadXml(archive, entry), shared, name, Path.GetFileName(path)));
                    }
                    return result;
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(Path.GetFileName(path) + ": " + ex.Message, ex);
            }
        }

        private static XDocument ReadXml(ZipArchive archive, string name)
        {
            ZipArchiveEntry entry = archive.GetEntry(name);
            if (entry == null) throw new InvalidOperationException("엑셀 내부 파일 누락: " + name);
            using (Stream stream = entry.Open())
            using (XmlReader reader = XmlReader.Create(stream, new XmlReaderSettings {
                DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
                return XDocument.Load(reader);
        }

        private static string TextValue(XElement element)
        {
            // Include rich-text runs, but not phonetic annotations.
            string text = string.Concat(element.Elements().SelectMany(e =>
                e.Name == Main + "t" ? new[] { e.Value } :
                e.Name == Main + "r" ? e.Elements(Main + "t").Select(t => t.Value) : Enumerable.Empty<string>()));
            return Regex.Replace(text, "_x([0-9A-Fa-f]{4})_", m =>
                ((char)int.Parse(m.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture)).ToString());
        }

        private static Cell ReadCell(XElement node, List<string> shared)
        {
            string type = (string)node.Attribute("t") ?? "n";
            XElement value = node.Element(Main + "v");
            if (node.Element(Main + "f") != null && (value == null || (value.Value.Length == 0 && type != "str")))
                return new Cell { Error = "수식 계산 결과가 없습니다. Excel에서 계산 후 저장해 주세요." };
            if (type == "e") return new Cell { Error = "엑셀 오류 값: " + (value == null ? "" : value.Value) };
            if (type == "inlineStr")
                return new Cell { Value = node.Element(Main + "is") == null ? "" : TextValue(node.Element(Main + "is")) };
            string text = value == null ? "" : value.Value;
            if (type == "s")
            {
                int index;
                if (!int.TryParse(text, out index) || index < 0 || index >= shared.Count)
                    return new Cell { Error = "공유 문자열 참조가 올바르지 않습니다." };
                text = shared[index];
            }
            else if (type == "b") text = text == "1" ? "TRUE" : "FALSE";
            return new Cell { Value = text };
        }

        private static int ColumnNumber(string reference)
        {
            Match match = Regex.Match(reference ?? "", "^([A-Z]+)[1-9][0-9]*$");
            if (!match.Success) throw new InvalidOperationException("셀 주소가 올바르지 않습니다: " + reference);
            int column = 0;
            foreach (char letter in match.Groups[1].Value) column = checked(column * 26 + letter - 'A' + 1);
            if (column > 16384) throw new InvalidOperationException("엑셀 최대 열을 초과했습니다.");
            return column;
        }

        private static string ValueAt(Dictionary<int, Dictionary<int, Cell>> rows, int row, int column, string sheet)
        {
            Dictionary<int, Cell> cells;
            Cell cell;
            if (!rows.TryGetValue(row, out cells) || !cells.TryGetValue(column, out cell)) return "";
            if (cell.Error != null)
                throw new InvalidOperationException(sheet + " " + row + "행 " + column + "열: " + cell.Error);
            return cell.Value ?? "";
        }

        private static bool HasValue(Cell cell) { return cell.Error != null || !string.IsNullOrEmpty(cell.Value); }

        private static Table ReadTable(XDocument document, List<string> shared, string name, string source)
        {
            var rows = new Dictionary<int, Dictionary<int, Cell>>();
            XElement sheetData = document.Root.Element(Main + "sheetData");
            if (sheetData == null) throw new InvalidOperationException(name + ": 셀 데이터가 없습니다.");
            foreach (XElement rowNode in sheetData.Elements(Main + "row"))
            {
                int row = int.Parse((string)rowNode.Attribute("r"), CultureInfo.InvariantCulture);
                if (row < 5) continue;
                var cells = new Dictionary<int, Cell>();
                foreach (XElement cell in rowNode.Elements(Main + "c"))
                    cells.Add(ColumnNumber((string)cell.Attribute("r")), ReadCell(cell, shared));
                rows.Add(row, cells);
            }
            if (ValueAt(rows, 5, 1, name).Trim() != "비고")
                throw new InvalidOperationException(name + ": A5에 '비고'가 있어야 합니다.");
            if (!rows.ContainsKey(6)) throw new InvalidOperationException(name + ": 6행 컬럼명이 없습니다.");

            var excluded = new HashSet<string>(StringComparer.Ordinal);
            foreach (int column in rows[5].Keys.Where(c => c > 1))
                foreach (string token in Regex.Split(ValueAt(rows, 5, column, name), @"[,;\r\n]+"))
                    if (token.Trim().Length != 0) excluded.Add(token.Trim());

            var headers = new SortedDictionary<int, string>();
            var headerNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (int column in rows[6].Keys.OrderBy(c => c))
            {
                string header = ValueAt(rows, 6, column, name).Trim();
                if (header.Length == 0) continue;
                if (!headerNames.Add(header)) throw new InvalidOperationException(name + ": 중복 컬럼명: " + header);
                headers.Add(column, header);
            }
            foreach (string column in excluded)
                if (!headerNames.Contains(column))
                    throw new InvalidOperationException(name + ": 5행 비고의 '" + column + "'이(가) 6행 컬럼명에 없습니다. 철자를 확인해 주세요.");

            foreach (var row in rows.Where(r => r.Key > 6))
                foreach (var cell in row.Value)
                    if (!headers.ContainsKey(cell.Key) && HasValue(cell.Value))
                        throw new InvalidOperationException(name + " " + row.Key + "행 " + cell.Key + "열: 데이터가 있지만 6행 컬럼명이 없습니다.");

            var kept = headers.Where(h => !excluded.Contains(h.Value)).ToArray();
            if (kept.Length == 0) throw new InvalidOperationException(name + ": 추출할 컬럼이 없습니다.");
            int lastRow = 6;
            foreach (var row in rows.Where(r => r.Key > 6))
                if (kept.Any(h => row.Value.ContainsKey(h.Key) && HasValue(row.Value[h.Key])))
                    lastRow = Math.Max(lastRow, row.Key);

            var content = new StringBuilder();
            AppendRecord(content, kept.Select(h => h.Value));
            for (int row = 7; row <= lastRow; row++)
                AppendRecord(content, kept.Select(h => ValueAt(rows, row, h.Key, name)));
            return new Table { Name = name, Source = source, Content = content.ToString(),
                Rows = lastRow - 6, Columns = kept.Length, Excluded = headers.Values.Where(excluded.Contains).ToArray() };
        }

        private static void AppendRecord(StringBuilder target, IEnumerable<string> values)
        {
            // Quote every field so empty rows, whitespace, commas and newlines round-trip.
            target.Append(string.Join(",", values.Select(v => "\"" + v.Replace("\"", "\"\"") + "\"")));
            target.Append("\r\n");
        }

        private static void CheckFilename(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
                || name.EndsWith(".") || name.EndsWith(" ")
                || Regex.IsMatch(name, @"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\.|$)", RegexOptions.IgnoreCase))
                throw new InvalidOperationException("CSV 파일명으로 사용할 수 없는 시트 이름: " + name);
        }

        private static void WriteBatch(List<Table> tables, string directory)
        {
            Directory.CreateDirectory(directory);
            string staging = Path.Combine(directory, ".csv-export-" + Guid.NewGuid().ToString("N") + "~");
            Directory.CreateDirectory(staging);
            var committed = new List<string>();
            bool preserveBackups = false;
            try
            {
                foreach (Table table in tables)
                    File.WriteAllText(Path.Combine(staging, table.Name + ".csv"), table.Content, new UTF8Encoding(false, true));
                foreach (Table table in tables)
                {
                    string filename = table.Name + ".csv";
                    string destination = Path.Combine(directory, filename);
                    string staged = Path.Combine(staging, filename);
                    if (File.Exists(destination)) File.Replace(staged, destination, staged + ".backup");
                    else File.Move(staged, destination);
                    committed.Add(filename);
                }
            }
            catch (Exception writeError)
            {
                var rollbackErrors = new List<string>();
                foreach (string filename in Enumerable.Reverse(committed))
                {
                    string destination = Path.Combine(directory, filename);
                    string backup = Path.Combine(staging, filename + ".backup");
                    try
                    {
                        if (File.Exists(backup))
                        {
                            if (File.Exists(destination)) File.Replace(backup, destination, null);
                            else File.Move(backup, destination);
                        }
                        else File.Delete(destination);
                    }
                    catch (Exception rollbackError) { rollbackErrors.Add(filename + ": " + rollbackError.Message); }
                }
                preserveBackups = rollbackErrors.Count > 0;
                if (preserveBackups)
                    throw new IOException("저장 및 복구 실패. 백업 위치: " + staging + " / " + string.Join("; ", rollbackErrors), writeError);
                throw;
            }
            finally
            {
                if (!preserveBackups && Directory.Exists(staging)) Directory.Delete(staging, true);
            }
        }
    }
}
