// OfficeForge.cs
// Files of Office built in memory for the tests of the reading of their text: an archive of the parts given, and the least Word, Excel and Visio files that hold a body.

using System.IO.Compression;
using System.Text;

namespace Topogenia.Interchange.Tests.Support;

/// <summary>
/// Builds the archives of Office the tests read: the parts given as texts (UTF-8), in the order given. A Word, Excel or
/// Visio file has the relations of its root to its main part, as Office writes them, and the parts the test gives.
/// </summary>
internal static class OfficeForge
{
    public const string W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    public const string S = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    public const string V = "http://schemas.microsoft.com/office/visio/2012/main";
    public const string R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private const string Package = "http://schemas.openxmlformats.org/package/2006/relationships";

    public static byte[] Archive(params (string Name, string Content)[] parts)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in parts)
            {
                using var entry = archive.CreateEntry(name).Open();
                entry.Write(Encoding.UTF8.GetBytes(content));
            }
        }
        return stream.ToArray();
    }

    public static string Relations(params (string Id, string Type, string Target)[] relations) =>
        $"""<?xml version="1.0" encoding="UTF-8"?><Relationships xmlns="{Package}">"""
        + string.Concat(relations.Select(relation => $"""<Relationship Id="{relation.Id}" Type="{R}/{relation.Type}" Target="{relation.Target}"/>"""))
        + "</Relationships>";

    /// <summary>A Word file whose main part (`word/document.xml`, or the name given) holds the body given.</summary>
    public static byte[] Word(string body, string main = "word/document.xml") => Archive(
        ("_rels/.rels", Relations(("rId1", "officeDocument", main))),
        (main, $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><w:document xmlns:w="{W}"><w:body>{body}</w:body></w:document>"""));

    /// <summary>A paragraph of Word of one run of text, with the properties given.</summary>
    public static string Paragraph(string text, string properties = "") =>
        $"""<w:p>{(properties.Length > 0 ? $"<w:pPr>{properties}</w:pPr>" : "")}<w:r><w:t xml:space="preserve">{text}</w:t></w:r></w:p>""";

    /// <summary>An Excel workbook of the sheets given (name, rows of `sheetData`, hidden), its shared texts and its styles.</summary>
    public static byte[] Excel((string Name, string Rows, bool Hidden)[] sheets, string[]? shared = null, string? styles = null, bool date1904 = false)
    {
        var parts = new List<(string, string)>
        {
            ("_rels/.rels", Relations(("rId1", "officeDocument", "xl/workbook.xml"))),
            ("xl/workbook.xml", $"""<workbook xmlns="{S}" xmlns:r="{R}"><workbookPr{(date1904 ? " date1904=\"1\"" : "")}/><sheets>"""
                + string.Concat(sheets.Select((sheet, index) => $"""<sheet name="{sheet.Name}" sheetId="{index + 1}"{(sheet.Hidden ? " state=\"hidden\"" : "")} r:id="rId{index + 1}"/>"""))
                + "</sheets></workbook>"),
        };
        var relations = sheets.Select((_, index) => ($"rId{index + 1}", "worksheet", $"worksheets/sheet{index + 1}.xml")).ToList();
        relations.Add(("rIdS", "sharedStrings", "sharedStrings.xml"));
        relations.Add(("rIdT", "styles", "styles.xml"));
        parts.Add(("xl/_rels/workbook.xml.rels", Relations([.. relations])));
        for (var index = 0; index < sheets.Length; index++)
            parts.Add(($"xl/worksheets/sheet{index + 1}.xml", $"""<worksheet xmlns="{S}"><sheetData>{sheets[index].Rows}</sheetData></worksheet>"""));
        if (shared is not null)
            parts.Add(("xl/sharedStrings.xml", $"""<sst xmlns="{S}">""" + string.Concat(shared.Select(text => $"<si><t>{text}</t></si>")) + "</sst>"));
        if (styles is not null)
            parts.Add(("xl/styles.xml", $"""<styleSheet xmlns="{S}">{styles}</styleSheet>"""));
        return Archive([.. parts]);
    }

    /// <summary>A Visio drawing of the pages given (name, `PageContents` inner XML, background) and of the masters given (identifier, universal name).</summary>
    public static byte[] Visio((string Name, string Contents, bool Background)[] pages, (string Id, string Name)[]? masters = null)
    {
        const string visio = "http://schemas.microsoft.com/visio/2010/relationships";
        var parts = new List<(string, string)>
        {
            ("_rels/.rels", $"""<Relationships xmlns="{Package}"><Relationship Id="rId1" Type="{visio}/document" Target="visio/document.xml"/></Relationships>"""),
            ("visio/document.xml", $"""<VisioDocument xmlns="{V}"/>"""),
            ("visio/_rels/document.xml.rels", $"""<Relationships xmlns="{Package}"><Relationship Id="rId1" Type="{visio}/pages" Target="pages/pages.xml"/><Relationship Id="rId2" Type="{visio}/masters" Target="masters/masters.xml"/></Relationships>"""),
            ("visio/pages/pages.xml", $"""<Pages xmlns="{V}" xmlns:r="{R}">"""
                + string.Concat(pages.Select((page, index) => $"""<Page ID="{index}" Name="{page.Name}"{(page.Background ? " Background=\"1\"" : "")}><PageSheet/><Rel r:id="rId{index + 1}"/></Page>"""))
                + "</Pages>"),
            ("visio/pages/_rels/pages.xml.rels", $"""<Relationships xmlns="{Package}">"""
                + string.Concat(pages.Select((_, index) => $"""<Relationship Id="rId{index + 1}" Type="{visio}/page" Target="page{index + 1}.xml"/>"""))
                + "</Relationships>"),
            ("visio/masters/masters.xml", $"""<Masters xmlns="{V}">""" + string.Concat((masters ?? []).Select(master => $"""<Master ID="{master.Id}" NameU="{master.Name}"/>""")) + "</Masters>"),
        };
        for (var index = 0; index < pages.Length; index++)
            parts.Add(($"visio/pages/page{index + 1}.xml", $"""<PageContents xmlns="{V}">{pages[index].Contents}</PageContents>"""));
        return Archive([.. parts]);
    }

    /// <summary>A shape of Visio: its identifier, text, master, place and size; the inner XML of a group given.</summary>
    public static string Shape(string id, string? text, string? master = null, double? x = null, double? y = null, double? width = null, double? height = null,
        string? inner = null, bool group = false) =>
        $"""<Shape ID="{id}"{(master is null ? "" : $" Master=\"{master}\"")}{(group ? " Type=\"Group\"" : "")}>"""
        + Cell("PinX", x) + Cell("PinY", y) + Cell("Width", width) + Cell("Height", height)
        + """<Section N="Geometry"><Row T="MoveTo"><Cell N="X" V="99"/></Row></Section>"""
        + (text is null ? "" : $"<Text><cp IX=\"0\"/>{text}</Text>")
        + (inner is null ? "" : $"<Shapes>{inner}</Shapes>")
        + "</Shape>";

    private static string Cell(string name, double? value) =>
        value is { } number ? $"""<Cell N="{name}" V="{number.ToString(System.Globalization.CultureInfo.InvariantCulture)}"/>""" : "";
}
