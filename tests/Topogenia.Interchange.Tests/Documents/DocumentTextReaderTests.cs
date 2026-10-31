// DocumentTextReaderTests.cs
// The text read from the Word, Excel and Visio files joined to a request of the assistant: what each format gives, and every content refused (an archive of another format, a DTD, a bomb of compression, too many entries) or cut (spec 3.0 section 3.4, lot 4).

using Topogenia.Application.Assistant;
using Topogenia.Interchange.Documents;
using static Topogenia.Interchange.Tests.Support.OfficeForge;

namespace Topogenia.Interchange.Tests.Documents;

public sealed class DocumentTextReaderTests
{
    private static string? Read(AttachmentKind kind, byte[] content) => new DocumentTextReader().Read(kind, content);

    [Fact]
    public void A_word_file_gives_its_headings_items_paragraphs_and_tables_without_its_deleted_text_nor_its_tab_stops()
    {
        var body = Paragraph("Procédure d'achat", $"""<w:pStyle w:val="Titre1"/>""")
            + Paragraph("Recevoir la demande", """<w:numPr><w:ilvl w:val="0"/></w:numPr>""")
            + """<w:p><w:pPr><w:tabs><w:tab w:val="left" w:pos="720"/></w:tabs></w:pPr><w:r><w:rPr><w:b/></w:rPr><w:t>Délai</w:t><w:tab/><w:t>48 h</w:t><w:br/><w:t>ensuite</w:t></w:r>"""
            + """<w:del><w:r><w:delText>supprimé</w:delText></w:r></w:del><w:ins><w:r><w:t xml:space="preserve"> ajouté</w:t></w:r></w:ins></w:p>"""
            + """<w:tbl><w:tblPr><w:tblW w:w="0"/></w:tblPr><w:tr><w:tc><w:tcPr/><w:p><w:r><w:t>Acteur</w:t></w:r></w:p></w:tc><w:tc><w:p><w:r><w:t>Durée</w:t></w:r></w:p></w:tc></w:tr>"""
            + """<w:tr><w:tc><w:p><w:r><w:t>Acheteur</w:t></w:r></w:p><w:p><w:r><w:t>Comptable</w:t></w:r></w:p></w:tc><w:tc><w:p><w:r><w:t>2 h</w:t></w:r></w:p></w:tc></w:tr></w:tbl>"""
            + Paragraph("Fin", """<w:pStyle w:val="Title"/>""");

        var text = Read(AttachmentKind.Word, Word(body));

        Assert.Equal("# Procédure d'achat\n- Recevoir la demande\nDélai\t48 h\nensuite ajouté\n| Acteur | Durée |\n| Acheteur Comptable | 2 h |\n# Fin", text);
    }

    [Theory]
    [InlineData("Heading2", "## Étape")]
    [InlineData("berschrift3", "### Étape")]
    [InlineData("Ttulo1", "# Étape")]
    [InlineData("Normal", "Étape")]
    public void The_level_of_a_heading_comes_from_its_style_in_the_language_of_word(string style, string expected) =>
        Assert.Equal(expected, Read(AttachmentKind.Word, Word(Paragraph("Étape", $"""<w:pStyle w:val="{style}"/>"""))));

    [Fact]
    public void The_main_part_is_the_one_the_relations_of_the_root_name()
    {
        Assert.Equal("Ailleurs", Read(AttachmentKind.Word, Word(Paragraph("Ailleurs"), "word/document2.xml")));
    }

    [Fact]
    public void A_relation_that_leaves_the_archive_is_ignored_and_a_word_file_without_its_main_part_is_unreadable()
    {
        // A part of that name exists at the root of the archive: read, it would give its text.
        var content = Archive(("_rels/.rels", Relations(("rId1", "officeDocument", "../../outside.xml"))),
            ("outside.xml", $"""<w:document xmlns:w="{W}"><w:body>{Paragraph("Hors de l'archive")}</w:body></w:document>"""));

        Assert.Null(Read(AttachmentKind.Word, content));
    }

    [Fact]
    public void A_file_in_the_strict_form_of_office_is_read_as_the_transitional_one()
    {
        const string strict = "http://purl.oclc.org/ooxml/wordprocessingml/main";
        var content = Archive(
            ("_rels/.rels", """<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://purl.oclc.org/ooxml/officeDocument/relationships/officeDocument" Target="word/document.xml"/></Relationships>"""),
            ("word/document.xml", $"""<w:document xmlns:w="{strict}"><w:body><w:p><w:pPr><w:pStyle w:val="Heading1"/></w:pPr><w:r><w:t>Strict</w:t></w:r></w:p></w:body></w:document>"""));

        Assert.Equal("# Strict", Read(AttachmentKind.Word, content));
    }

    [Fact]
    public void A_document_without_text_says_so()
    {
        Assert.Equal("(the file holds no text)", Read(AttachmentKind.Word, Word("<w:p/>")));
    }

    [Fact]
    public void An_xml_with_a_dtd_is_refused_before_any_entity_is_read()
    {
        var content = Archive(
            ("_rels/.rels", Relations(("rId1", "officeDocument", "word/document.xml"))),
            ("word/document.xml", $"""<?xml version="1.0"?><!DOCTYPE w:document [<!ENTITY a "aaaaaaaaaa">]><w:document xmlns:w="{W}"><w:body><w:p><w:r><w:t>&a;</w:t></w:r></w:p></w:body></w:document>"""));

        Assert.Null(Read(AttachmentKind.Word, content));
    }

    [Fact]
    public void A_content_that_is_no_archive_is_unreadable()
    {
        Assert.Null(Read(AttachmentKind.Excel, "%PDF-1.7 not an archive"u8.ToArray()));
        Assert.Null(Read(AttachmentKind.Visio, Word(Paragraph("Un fichier Word"))));
    }

    [Fact]
    public void An_entry_larger_than_its_limit_once_uncompressed_is_unreadable_as_a_bomb_of_compression()
    {
        var content = Word(Paragraph(new string('a', 5000)));

        Assert.NotNull(new DocumentTextReader(new OfficeLimits(10, 10_000, 100_000)).Read(AttachmentKind.Word, content));
        Assert.Null(new DocumentTextReader(new OfficeLimits(10, 2_000, 100_000)).Read(AttachmentKind.Word, content));
        Assert.Null(new DocumentTextReader(new OfficeLimits(10, 10_000, 2_000)).Read(AttachmentKind.Word, content));
    }

    [Fact]
    public void An_archive_of_too_many_entries_is_unreadable()
    {
        var content = Word(Paragraph("Texte"));

        Assert.NotNull(new DocumentTextReader(new OfficeLimits(2, 10_000, 100_000)).Read(AttachmentKind.Word, content));
        Assert.Null(new DocumentTextReader(new OfficeLimits(1, 10_000, 100_000)).Read(AttachmentKind.Word, content));
    }

    [Fact]
    public void A_text_past_its_bound_is_cut_and_says_so()
    {
        var text = new DocumentTextReader(textMax: 20).Read(AttachmentKind.Word, Word(Paragraph("Recevoir la demande d'achat") + Paragraph("Valider")));

        Assert.Equal("Recevoir la demande\n[The text was cut here: the application reads at most 20 characters of a file.]", text);
    }

    [Fact]
    public void An_excel_file_gives_its_sheets_and_rows_with_shared_and_inline_texts_booleans_errors_dates_times_and_durations()
    {
        const string styles = """
            <numFmts count="2"><numFmt numFmtId="164" formatCode="[h]:mm"/><numFmt numFmtId="165" formatCode="dd/mm/yyyy\ hh:mm"/></numFmts>
            <cellStyleXfs count="1"><xf numFmtId="14"/></cellStyleXfs>
            <cellXfs count="5"><xf numFmtId="0"/><xf numFmtId="14"/><xf numFmtId="164"/><xf numFmtId="165"/><xf numFmtId="20"/></cellXfs>
            """;
        const string process = """
            <row r="1"><c r="A1" t="s"><v>0</v></c><c r="C1" t="s"><v>1</v></c></row>
            <row r="3"><c r="A3" t="s"><v>2</v></c><c r="B3" s="1"><v>46325</v></c><c r="C3" s="2"><v>1.5</v></c><c r="D3" s="3"><v>46325.5</v></c><c r="E3" s="4"><v>0.75</v></c></row>
            <row r="4"><c r="A4" t="inlineStr"><is><t>Valider</t></is></c><c r="B4" t="b"><v>1</v></c><c r="C4"><v>0.30000000000000004</v></c><c r="D4" t="e"><v>#DIV/0!</v></c><c r="E4" t="str"><f>A4&amp;"x"</f><v>Validerx</v></c></row>
            <row r="5"/>
            <row r="6"><c r="A6"/></row>
            """;
        var content = Excel([("Processus", process, false), ("Cachée", """<row r="2"><c r="B2"><v>42</v></c></row>""", true)], ["Étape", "Durée", "Recevoir"], styles);

        var text = Read(AttachmentKind.Excel, content);

        Assert.Equal("## Sheet \"Processus\"\n1\tÉtape\t\tDurée\n3\tRecevoir\t2026-10-30\t36:00\t2026-10-30 12:00\t18:00\n4\tValider\tTRUE\t0.3\t#DIV/0!\tValiderx\n\n"
            + "## Sheet \"Cachée\" (hidden)\n2\t\t42", text);
    }

    [Fact]
    public void The_dates_of_a_workbook_of_1904_count_from_its_first_day()
    {
        var content = Excel([("Dates", """<row r="1"><c r="A1" s="1"><v>0</v></c></row>""", false)], styles: """<cellXfs count="2"><xf numFmtId="0"/><xf numFmtId="14"/></cellXfs>""",
            date1904: true);

        Assert.Equal("## Sheet \"Dates\"\n1\t1904-01-01", Read(AttachmentKind.Excel, content));
    }

    [Fact]
    public void A_cell_without_reference_follows_the_one_before_and_a_shared_text_out_of_range_is_empty()
    {
        var content = Excel([("Feuille", """<row r="7"><c r="B7"><v>1</v></c><c><v>2</v></c><c r="E7" t="s"><v>9</v></c></row>""", false)], ["seul"]);

        Assert.Equal("## Sheet \"Feuille\"\n7\t\t1\t2", Read(AttachmentKind.Excel, content));
    }

    [Fact]
    public void A_visio_drawing_gives_its_shapes_with_their_master_place_and_size_and_the_links_of_its_connectors()
    {
        var shapes = Shape("1", "Acheteur", "4", 4, 6, 8, 2)
            + Shape("10", "Recevoir la demande", "2", 1.5, 6.25, 1, 0.75)
            + Shape("11", "Valider", "2", 3, 6.25, 1, 0.75)
            + Shape("20", "Oui", "3")
            + Shape("30", null, null, 6, 3, 2, 1, Shape("31", "Archiver", "2", 0.5, 0.5, 1, 0.5), group: true)
            + Shape("40", null, null, 9, 9, 1, 1);
        const string connects = """<Connects><Connect FromSheet="20" FromCell="BeginX" ToSheet="10"/><Connect FromSheet="20" FromCell="EndX" ToSheet="11"/></Connects>""";
        var content = Visio([("Achats", $"<Shapes>{shapes}</Shapes>{connects}", false), ("Fond", Shape("50", "Logo"), true), ("Vide", "", false)],
            [("2", "Process"), ("3", "Dynamic connector"), ("4", "Swimlane")]);

        var text = Read(AttachmentKind.Visio, content);

        const string heading = "Shapes (identifier, text, master, centre x and y and size in inches, y growing upwards):\n";
        Assert.Equal("## Page \"Achats\"\n" + heading
            + "- 1 \"Acheteur\" [Swimlane] at (4, 6) size 8 x 2\n"
            + "- 10 \"Recevoir la demande\" [Process] at (1.5, 6.25) size 1 x 0.75\n"
            + "- 11 \"Valider\" [Process] at (3, 6.25) size 1 x 0.75\n"
            + "- 31 \"Archiver\" [Process] at (5.5, 3) size 1 x 0.5\n"
            + "Links (from the begin of a connector to its end):\n"
            + "- 10 \"Recevoir la demande\" -> 11 \"Valider\" : \"Oui\"\n\n"
            + "## Page \"Vide\"\n" + heading.TrimEnd('\n'), text);
    }

    [Fact]
    public void A_connector_glued_at_one_end_and_a_shape_linked_without_text_are_said()
    {
        var shapes = Shape("1", null, null, 1, 1, 1, 1) + Shape("2", null);
        var content = Visio([("Page-1", $"""<Shapes>{shapes}</Shapes><Connects><Connect FromSheet="2" FromCell="EndX" ToSheet="1"/></Connects>""", false)]);

        var text = Read(AttachmentKind.Visio, content)!;

        Assert.Contains("- 1 (no text) at (1, 1) size 1 x 1\n", text, StringComparison.Ordinal);
        Assert.EndsWith("- (not glued) -> 1 (no text)", text, StringComparison.Ordinal);
    }
}
