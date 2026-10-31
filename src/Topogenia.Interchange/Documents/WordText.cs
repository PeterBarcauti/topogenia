// WordText.cs
// The text of a Word file (.docx): its paragraphs in their order, the headings marked by their level, the items of a list, the tables row by row; the text deleted by a revision left out (spec 3.0 section 3.4, lot 4).

using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;

namespace Topogenia.Interchange.Documents;

/// <summary>
/// Reads the main part of a Word file (`word/document.xml`, or the part its relations name) in one pass: the text of the
/// runs (`w:t`), their tabs and breaks; a paragraph per line, a heading after as many `#` as its level, an item of a list
/// after `- `; a table as rows of cells separated by ` | `. The properties of runs and tables are skipped (their tab stops
/// are no text), as are the texts deleted (`w:delText`) and the codes of the fields (`w:instrText`), which are no `w:t`.
/// </summary>
internal static partial class WordText
{
    private const string Vocabulary = "wordprocessingml";

    public static bool Read(OfficeArchive archive, BoundedText text)
    {
        using var xml = archive.Xml(archive.Main(OfficeArchive.OfficeDocument, "word/document.xml"));
        if (xml is null)
            return false;
        var state = new State(text);
        var moved = false;
        while (!text.Full && (moved || xml.Read()))
        {
            moved = false;
            switch (xml.NodeType)
            {
                case XmlNodeType.Element when OfficeArchive.In(xml, Vocabulary):
                    moved = state.Open(xml);
                    break;
                case XmlNodeType.EndElement when OfficeArchive.In(xml, Vocabulary):
                    state.Close(xml.LocalName);
                    break;
                case XmlNodeType.Text or XmlNodeType.SignificantWhitespace or XmlNodeType.Whitespace or XmlNodeType.CDATA when state.InText:
                    text.Append(xml.Value);
                    break;
            }
        }
        return true;
    }

    private sealed class State(BoundedText text)
    {
        /// <summary>The depth of the cells of tables the reader is in (a table may hold a table).</summary>
        private int cells;

        /// <summary>The reader is in a `w:t`, whose text nodes are the text of the document.</summary>
        public bool InText { get; private set; }

        /// <summary>Opens an element; true when the reader was moved past it (an element skipped).</summary>
        public bool Open(XmlReader xml)
        {
            switch (xml.LocalName)
            {
                case "pPr":
                    Paragraph(xml);
                    return false;
                case "rPr" or "tblPr" or "tblGrid" or "trPr" or "tcPr" or "sectPr":
                    xml.Skip();
                    return true;
                case "t":
                    InText = !xml.IsEmptyElement;
                    return false;
                case "tab":
                    text.Append(cells > 0 ? ' ' : '\t');
                    return false;
                case "br" or "cr":
                    text.Append(cells > 0 ? " " : "\n");
                    return false;
                case "tr":
                    text.EndLine().Append("| ");
                    return false;
                case "tc" when !xml.IsEmptyElement:
                    cells++;
                    return false;
                default:
                    return false;
            }
        }

        public void Close(string name)
        {
            switch (name)
            {
                case "t":
                    InText = false;
                    break;
                case "p" when cells > 0:
                    text.TrimEnd(' ');
                    text.Append(' ');
                    break;
                case "p" or "tbl":
                    text.EndLine();
                    break;
                case "tc":
                    cells--;
                    text.TrimEnd(' ');
                    text.Append(" | ");
                    break;
                case "tr":
                    text.TrimEnd(' ');
                    text.EndLine();
                    break;
            }
        }

        /// <summary>
        /// The mark of a paragraph from its properties, read to their end: `#` as many times as the level of its heading,
        /// `# ` for a title, `- ` for an item of a list; none in a cell of a table.
        /// </summary>
        private void Paragraph(XmlReader xml)
        {
            string? style = null;
            var listed = false;
            using (var properties = xml.ReadSubtree())
            {
                while (properties.Read())
                {
                    if (properties.NodeType != XmlNodeType.Element || !OfficeArchive.In(properties, Vocabulary))
                        continue;
                    if (properties.LocalName == "pStyle")
                        style = properties.GetAttribute("val", properties.NamespaceURI);
                    else if (properties.LocalName == "numPr")
                        listed = true;
                }
            }
            if (cells > 0)
                return;
            if (style is not null && Heading().Match(style) is { Success: true } heading)
                text.Append(new string('#', int.Parse(heading.Groups[1].Value, CultureInfo.InvariantCulture))).Append(' ');
            else if (style is not null && Title().IsMatch(style))
                text.Append("# ");
            else if (listed)
                text.Append("- ");
        }
    }

    /// <summary>
    /// The identifiers of the styles of the headings, which Word writes in the language of the document without its
    /// accented letters: Heading1, Titre1, berschrift1 (Überschrift 1), Titolo1, Ttulo1 (Título 1), Kop1, Nagwek1 (Nagłówek 1).
    /// </summary>
    [GeneratedRegex(@"^(?:Heading|Titre|berschrift|Titolo|T.?tulo|Kop|Nag.?wek)\s*([1-6])$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Heading();

    [GeneratedRegex("^(?:Title|Titre|Titel|Titolo|T.?tulo|Tytu.?)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Title();
}
