// ExcelText.cs
// The text of an Excel file (.xlsx): each sheet by its name, its rows with their number, the cells separated by tabs at their column; the shared texts, the booleans, the dates and the durations read as a person reads them (spec 3.0 section 3.4, lot 4).

using System.Globalization;
using System.Text;
using System.Xml;

namespace Topogenia.Interchange.Documents;

/// <summary>
/// Reads an Excel workbook: its sheets in their order (`xl/workbook.xml` and its relations), the shared texts
/// (`xl/sharedStrings.xml`), the formats of the cells (`xl/styles.xml`) to tell a date or a duration from a number. Each
/// sheet is a heading, then its rows that hold a value: the number of the row, then its cells separated by tabs at their
/// column (an empty cell keeps its place). A formula gives the value Excel last computed; an error, its code.
/// </summary>
internal static class ExcelText
{
    private const string Vocabulary = "spreadsheetml";

    /// <summary>The most columns of a row written (XFD is the last column of Excel).</summary>
    private const int ColumnsMax = 16_384;

    public static bool Read(OfficeArchive archive, BoundedText text)
    {
        var workbook = archive.Main(OfficeArchive.OfficeDocument, "xl/workbook.xml");
        var (sheets, date1904) = Sheets(archive, workbook);
        if (sheets is null)
            return false;
        var targets = archive.Relations(workbook);
        var shared = Shared(archive, archive.Target(targets, "/sharedStrings") ?? "xl/sharedStrings.xml");
        var formats = Formats(archive, archive.Target(targets, "/styles") ?? "xl/styles.xml");
        foreach (var (name, id, hidden) in sheets)
        {
            if (text.Full)
                break;
            if (!targets.TryGetValue(id, out var target))
                continue;
            text.EndLine().Append("\n## Sheet \"").Append(name).Append('"').Append(hidden ? " (hidden)" : "").Append('\n');
            Sheet(archive, target.Part, new Cells(shared, formats, date1904), text);
        }
        return true;
    }

    /// <summary>The sheets of the workbook in their order (name, relation, hidden), and whether its dates count from 1904; null without a workbook.</summary>
    private static (List<(string Name, string Id, bool Hidden)>? Sheets, bool Date1904) Sheets(OfficeArchive archive, string workbook)
    {
        using var xml = archive.Xml(workbook);
        if (xml is null)
            return (null, false);
        var sheets = new List<(string, string, bool)>();
        var date1904 = false;
        while (xml.Read())
        {
            if (xml.NodeType != XmlNodeType.Element || !OfficeArchive.In(xml, Vocabulary))
                continue;
            if (xml.LocalName == "workbookPr")
                date1904 = xml.GetAttribute("date1904") is "1" or "true";
            else if (xml.LocalName == "sheet" && OfficeArchive.RelationOf(xml) is { } id)
                sheets.Add((xml.GetAttribute("name") ?? "", id, xml.GetAttribute("state") is "hidden" or "veryHidden"));
        }
        return (sheets, date1904);
    }

    /// <summary>The shared texts, each the runs of its `si` but their phonetic readings.</summary>
    private static List<string> Shared(OfficeArchive archive, string part)
    {
        var shared = new List<string>();
        using var xml = archive.Xml(part);
        if (xml is null)
            return shared;
        var current = new StringBuilder();
        var inText = false;
        var moved = false;
        while (moved || xml.Read())
        {
            moved = false;
            if (xml.NodeType == XmlNodeType.Element && OfficeArchive.In(xml, Vocabulary))
            {
                if (xml.LocalName == "rPh")
                {
                    xml.Skip();
                    moved = true;
                }
                else if (xml.LocalName == "si")
                    current.Clear();
                else if (xml.LocalName == "t")
                    inText = !xml.IsEmptyElement;
                if (xml.LocalName == "si" && xml.IsEmptyElement)
                    shared.Add("");
            }
            else if (xml.NodeType == XmlNodeType.EndElement && OfficeArchive.In(xml, Vocabulary))
            {
                if (xml.LocalName == "t")
                    inText = false;
                else if (xml.LocalName == "si")
                    shared.Add(current.ToString());
            }
            else if (inText && xml.NodeType is XmlNodeType.Text or XmlNodeType.SignificantWhitespace or XmlNodeType.Whitespace or XmlNodeType.CDATA)
                current.Append(xml.Value);
        }
        return shared;
    }

    /// <summary>The kind of value of each format of cell, by its index (`s` of a cell): a number, a date, a time of day, a duration.</summary>
    private static List<NumberKind> Formats(OfficeArchive archive, string part)
    {
        var formats = new List<NumberKind>();
        using var xml = archive.Xml(part);
        if (xml is null)
            return formats;
        var custom = new Dictionary<int, string>();
        var inCells = false;
        while (xml.Read())
        {
            if (!OfficeArchive.In(xml, Vocabulary))
                continue;
            if (xml.NodeType == XmlNodeType.EndElement && xml.LocalName == "cellXfs")
                inCells = false;
            if (xml.NodeType != XmlNodeType.Element)
                continue;
            if (xml.LocalName == "numFmt" && int.TryParse(xml.GetAttribute("numFmtId"), CultureInfo.InvariantCulture, out var id))
                custom[id] = xml.GetAttribute("formatCode") ?? "";
            else if (xml.LocalName == "cellXfs")
                inCells = !xml.IsEmptyElement;
            else if (xml.LocalName == "xf" && inCells)
            {
                _ = int.TryParse(xml.GetAttribute("numFmtId"), CultureInfo.InvariantCulture, out var format);
                formats.Add(NumberKinds.Of(format, custom.GetValueOrDefault(format)));
            }
        }
        return formats;
    }

    /// <summary>The rows of a sheet that hold a value: the number of the row, then its cells by column.</summary>
    private static void Sheet(OfficeArchive archive, string part, Cells cells, BoundedText text)
    {
        using var xml = archive.Xml(part);
        if (xml is null)
            return;
        var row = new SortedDictionary<int, string>();
        var number = 0;
        while (!text.Full && xml.Read())
        {
            if (!OfficeArchive.In(xml, Vocabulary))
                continue;
            if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "row")
            {
                row.Clear();
                number = int.TryParse(xml.GetAttribute("r"), CultureInfo.InvariantCulture, out var given) ? given : number + 1;
                if (xml.IsEmptyElement)
                    continue;
            }
            else if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "c")
            {
                var column = ColumnOf(xml.GetAttribute("r")) ?? (row.Count == 0 ? 0 : row.Keys.Max() + 1);
                var value = cells.Value(xml);
                if (!string.IsNullOrWhiteSpace(value) && column < ColumnsMax)
                    row[column] = value.ReplaceLineEndings(" ").Replace('\t', ' ');
            }
            else if (xml.NodeType == XmlNodeType.EndElement && xml.LocalName == "row" && row.Count > 0)
            {
                text.Append(number.ToString(CultureInfo.InvariantCulture));
                var last = row.Keys.Max();
                for (var column = 0; column <= last; column++)
                    text.Append('\t').Append(row.GetValueOrDefault(column));
                text.Append('\n');
            }
        }
    }

    /// <summary>The index of the column of a reference (`C12` is 2), null without one.</summary>
    internal static int? ColumnOf(string? reference)
    {
        if (string.IsNullOrEmpty(reference))
            return null;
        var column = 0;
        var letters = 0;
        foreach (var letter in reference)
        {
            if (letter is < 'A' or > 'Z')
                break;
            column = column * 26 + (letter - 'A' + 1);
            letters++;
            if (letters > 3)
                return null;
        }
        return letters == 0 ? null : column - 1;
    }

    /// <summary>The value of a cell as a person reads it, from its type (`t`), its format (`s`) and its value (`v`, or its inline text).</summary>
    private sealed class Cells(List<string> shared, List<NumberKind> formats, bool date1904)
    {
        public string? Value(XmlReader xml)
        {
            var type = xml.GetAttribute("t");
            var format = int.TryParse(xml.GetAttribute("s"), CultureInfo.InvariantCulture, out var index) && index < formats.Count ? formats[index] : NumberKind.Number;
            if (xml.IsEmptyElement)
                return null;
            string? value = null;
            var inline = new StringBuilder();
            using (var cell = xml.ReadSubtree())
            {
                var inText = false;
                var inValue = false;
                while (cell.Read())
                {
                    if (cell.NodeType == XmlNodeType.Element && OfficeArchive.In(cell, Vocabulary))
                    {
                        inValue = cell.LocalName == "v" && !cell.IsEmptyElement;
                        inText = cell.LocalName == "t" && !cell.IsEmptyElement;
                    }
                    else if (cell.NodeType == XmlNodeType.EndElement)
                        inValue = inText = false;
                    else if (cell.NodeType is XmlNodeType.Text or XmlNodeType.SignificantWhitespace or XmlNodeType.Whitespace or XmlNodeType.CDATA)
                    {
                        if (inValue)
                            value += cell.Value;
                        else if (inText)
                            inline.Append(cell.Value);
                    }
                }
            }
            return type switch
            {
                "s" => int.TryParse(value, CultureInfo.InvariantCulture, out var at) && at >= 0 && at < shared.Count ? shared[at] : null,
                "inlineStr" => inline.ToString(),
                "b" => value == "1" ? "TRUE" : value == "0" ? "FALSE" : value,
                "str" or "e" => value,
                _ => NumberKinds.Write(value, format, date1904),
            };
        }
    }
}
