using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace EscapeGame.Data.CsvValidation.Schema
{
    public enum CsvValueKind { Text, Integer, Number }
    public enum CsvResourceKind { None, Prefab, Audio }

    public sealed class CsvColumnDefinition
    {
        public string Name { get; }
        public CsvValueKind Kind { get; }
        public bool Optional { get; }
        public double? Minimum { get; }
        public double? Maximum { get; }
        public IReadOnlyList<string> Values { get; }
        internal CsvColumnDefinition(XElement node)
        {
            Name = Required(node, "name");
            Kind = Enum.Parse<CsvValueKind>((string)node.Attribute("type") ?? "Text");
            Optional = (bool?)node.Attribute("optional") ?? false;
            Minimum = Bound(node, "min"); Maximum = Bound(node, "max");
            Values = Array.AsReadOnly(((string)node.Attribute("values") ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
            if (Minimum > Maximum) throw new FormatException("Invalid column bounds: " + Name);
        }
        private static double? Bound(XElement node, string name)
        {
            string value = (string)node.Attribute(name);
            if (value == null) return null;
            double number = double.Parse(value, CultureInfo.InvariantCulture);
            if (double.IsNaN(number) || double.IsInfinity(number)) throw new FormatException("Invalid bound");
            return number;
        }
        internal static string Required(XElement node, string name)
        {
            string value = (string)node.Attribute(name);
            if (string.IsNullOrWhiteSpace(value)) throw new FormatException("Missing attribute: " + name);
            return value;
        }
    }

    public sealed class CsvReferenceDefinition
    {
        public string Column { get; }
        public string Target { get; }
        public string Sentinel { get; }
        public string TargetKind { get; }
        public bool SameTable { get; }
        internal CsvReferenceDefinition(XElement node)
        {
            Column = CsvColumnDefinition.Required(node, "column");
            Target = CsvColumnDefinition.Required(node, "target");
            Sentinel = (string)node.Attribute("sentinel");
            TargetKind = (string)node.Attribute("kind") ?? "";
            SameTable = Target == "$self";
        }
    }

    public sealed class CsvTableDefinition
    {
        public string Name { get; }
        public string Tag { get; }
        public string Player { get; }
        public string Kind { get; }
        public string Key { get; }
        public string KeyGroup { get; }
        public CsvResourceKind Resource { get; }
        public IReadOnlyList<CsvColumnDefinition> Columns { get; }
        public IReadOnlyList<CsvReferenceDefinition> References { get; }
        public IReadOnlyList<IReadOnlyList<string>> UniqueKeys { get; }
        internal CsvTableDefinition(XElement table, XElement profile)
        {
            string Attribute(string key, string fallback = "") => (string)table.Attribute(key) ?? (string)profile?.Attribute(key) ?? fallback;
            Name = CsvColumnDefinition.Required(table, "name");
            Tag = Attribute("tag"); Player = Attribute("player"); Kind = Attribute("kind");
            Key = Attribute("key", "ID"); KeyGroup = Attribute("keyGroup");
            Resource = Enum.Parse<CsvResourceKind>(Attribute("resource", "None"));
            var nodes = (profile?.Elements() ?? Enumerable.Empty<XElement>()).Concat(table.Elements()).ToArray();
            Columns = Array.AsReadOnly(nodes.Where(x => x.Name == "column").Select(x => new CsvColumnDefinition(x)).ToArray());
            References = Array.AsReadOnly(nodes.Where(x => x.Name == "reference").Select(x => new CsvReferenceDefinition(x)).ToArray());
            UniqueKeys = Array.AsReadOnly(nodes.Where(x => x.Name == "unique").Select(x =>
                (IReadOnlyList<string>)Array.AsReadOnly(CsvColumnDefinition.Required(x, "columns").Split(','))).ToArray());
            var names = new HashSet<string>(Columns.Select(x => x.Name), StringComparer.Ordinal);
            if (names.Count != Columns.Count || !names.Contains(Key) || References.Any(x => !names.Contains(x.Column)) || UniqueKeys.Any(x => x.Any(c => !names.Contains(c))))
                throw new FormatException("Invalid schema: " + Name);
        }
    }

    public sealed class CsvValidationCatalog
    {
        public IReadOnlyList<CsvTableDefinition> Tables { get; }
        private CsvValidationCatalog(IEnumerable<CsvTableDefinition> tables) => Tables = Array.AsReadOnly(tables.ToArray());
        public CsvTableDefinition Find(string name) => Tables.FirstOrDefault(x => x.Name == name);
        public static CsvValidationCatalog Read(string xml)
        {
            using var input = new System.IO.StringReader(xml);
            using var reader = XmlReader.Create(input, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            var root = XDocument.Load(reader).Root;
            if (root == null || root.Name != "validation") throw new FormatException("Invalid validation catalog");
            var profiles = root.Elements("profile").ToDictionary(x => CsvColumnDefinition.Required(x, "name"), StringComparer.Ordinal);
            var tables = root.Elements("table").Select(table =>
            {
                string profile = (string)table.Attribute("profile");
                if (profile != null && !profiles.ContainsKey(profile)) throw new FormatException("Unknown profile: " + profile);
                return new CsvTableDefinition(table, profile == null ? null : profiles[profile]);
            }).ToArray();
            if (tables.Select(x => x.Name).Distinct(StringComparer.Ordinal).Count() != tables.Length || tables.Count(x => x.Tag == CsvTableTags.Registry) != 1)
                throw new FormatException("Duplicate table or missing registry definition");
            foreach (var table in tables)
                foreach (var reference in table.References)
                    if (!reference.SameTable && !tables.Any(x => x.Tag == reference.Target)) throw new FormatException("Unknown target tag: " + reference.Target);
            return new CsvValidationCatalog(tables);
        }
    }

    // 규칙은 실제 파일명 대신 역할을 사용합니다. 파일명은 설정 한 곳에만 있습니다.
    public static class CsvTableTags
    {
        public const string Registry = "registry", Room = "room", Anchor = "anchor", Prefab = "prefab",
            Puzzle = "puzzle", Slot = "slot", Console = "console", Sound = "sound", SoundLink = "sound-link",
            Config = "config", Lock = "lock", Arrow = "arrow", Wire = "wire";
    }
}
