// PmapSheetReadingTests.cs
// The sheet block of a .pmap 1.1 step (AR-93, S-7.5-02): every part optional, checked by the rules of the API, its references in the map.

using Topogenia.Domain.Errors;
using Topogenia.Interchange.Pmap;
using static Topogenia.Interchange.Tests.Pmap.PmapReaderTests;

namespace Topogenia.Interchange.Tests.Pmap;

public sealed class PmapSheetReadingTests
{
    /// <summary>Two steps of one map, the first with the sheet <paramref name="first"/>, the second with <paramref name="second"/>.</summary>
    private static string Steps(string first, string second = "{}", string firstType = "task") => Doc($$"""
        { "lanes": [{{TwoLanes}}],
          "steps": [{ "id": "e1", "lane": "c1", "label": "Produire", "type": "{{firstType}}", "sheet": {{first}} },
                    { "id": "e2", "lane": "c2", "label": "Utiliser", "sheet": {{second}} }] }
        """);

    [Fact]
    public void Every_part_of_the_block_is_optional_and_an_empty_block_gives_no_sheet()
    {
        var document = Read(Steps("""{ "purpose": "  Payer  ", "duration_minutes": 15.50 }"""));

        var sheet = document.Map.Steps[0].Sheet!;
        Assert.Equal(("Payer", 15.5m), (sheet.Purpose, sheet.DurationMinutes));
        Assert.Equal((null, null, null, null), (sheet.Actor, sheet.Tools, sheet.Frequency, sheet.FreeText));
        Assert.Empty(sheet.Procedure);
        Assert.Null(document.Map.Steps[1].Sheet);
        Assert.Null(Read(Steps("""{ "purpose": "   ", "procedure": [{ "text": "  " }] }""")).Map.Steps[0].Sheet);
    }

    [Fact]
    public void The_fields_follow_the_rules_the_api_applies_to_a_draft()
    {
        Assert.Equal(("map.steps[0].sheet", ErrorCodes.InvalidValue, (object?)"frequency"),
            Assert.Single(Faults(Steps("""{ "frequency": "sometimes" }"""))) is var fault ? (fault.Path, fault.Code, fault.Values["field"]) : default);
        Assert.Equal(ErrorCodes.TextTooLong, Assert.Single(Faults(Steps($$"""{ "purpose": "{{new string('x', 4001)}}" }"""))).Code);
        Assert.Equal(ErrorCodes.HtmlForbidden, Assert.Single(Faults(Steps("""{ "free_text": "<script>x</script>" }"""))).Code);
        Assert.Equal(ErrorCodes.InvalidValue, Assert.Single(Faults(Steps("""{ "duration_minutes": -5 }"""))).Code);
        Assert.Equal(FileErrorCodes.NotANumber, Assert.Single(Faults(Steps("""{ "duration_minutes": "15" }"""))).Code);
        var tasks = string.Join(",", Enumerable.Range(0, 51).Select(i => $$"""{ "text": "Tâche {{i}}" }"""));
        Assert.Equal(ErrorCodes.InvalidValue, Assert.Single(Faults(Steps($$"""{ "procedure": [{{tasks}}] }"""))).Code);
        Assert.Equal(ErrorCodes.InvalidValue, Assert.Single(Faults(Steps("""{ "procedure": [{ "text": "Saisir", "nature": "typing" }] }"""))).Code);
        Assert.Equal(ErrorCodes.InvalidValue, Assert.Single(Faults(Steps("""{ "procedure": [{ "tool": "Tableur" }] }"""))).Code);
        var read = Read(Steps("""{ "frequency": "weekly", "procedure": [{ "text": "Saisir", "tool": "ERP", "nature": "re_enter" }, { "text": "" }] }""")).Map.Steps[0].Sheet!;
        Assert.Equal(("weekly", "Saisir", "ERP", "re_enter"), (read.Frequency, read.Procedure.Single().Text, read.Procedure[0].Tool, read.Procedure[0].Nature));
    }

    [Fact]
    public void An_unknown_property_of_the_block_or_of_its_lines_lists_the_possible_ones()
    {
        var faults = Faults(Steps("""{ "goal": "x", "procedure": [{ "text": "a", "who": "b" }], "outputs": [{ "name": "D", "size": 1 }] }"""));

        Assert.Equal(["map.steps[0].sheet.goal", "map.steps[0].sheet.procedure[0].who", "map.steps[0].sheet.outputs[0].size"], faults.Select(fault => fault.Path));
        Assert.All(faults, fault => Assert.Equal(FileErrorCodes.UnknownProperty, fault.Code));
        Assert.Equal("actor, purpose, tools, frequency, duration_minutes, procedure, free_text, outputs, inputs, uses", faults[0].Values["possible"]);
    }

    [Fact]
    public void A_start_an_end_and_an_and_or_inclusive_or_gateway_have_no_sheet()
    {
        foreach (var type in new[] { "start", "end", "parallel", "inclusive" })
        {
            var fault = Assert.Single(Faults(Steps("""{ "purpose": "x" }""", firstType: type)));
            Assert.Equal(("map.steps[0].sheet", FileErrorCodes.SheetOnStepType), (fault.Path, fault.Code));
        }
        Assert.NotNull(Read(Steps("""{ "purpose": "x" }""", firstType: "decision")).Map.Steps[0].Sheet);
    }

    [Fact]
    public void Documents_follow_the_rules_of_the_api_name_link_share_and_waiting_time()
    {
        var sheet = Read(Steps("""
            { "outputs": [{ "id": "d1", "name": "Bon de commande", "location": "\\\\serveur\\achats\\bon.pdf", "share_confirmed": true }],
              "inputs": [{ "name": "Devis", "location": "https://exemple.test/devis", "wait_hours": 4.256 }] }
            """)).Map.Steps[0].Sheet!;

        Assert.Equal(new PmapDocumentEntry("d1", "Bon de commande", "\\\\serveur\\achats\\bon.pdf", true, null), sheet.Outputs.Single());
        Assert.Equal(new PmapDocumentEntry(null, "Devis", "https://exemple.test/devis", false, 4.26m), sheet.Inputs.Single());
        Assert.Equal([ErrorCodes.RequiredField, ErrorCodes.InvalidAddress, ErrorCodes.InvalidValue, FileErrorCodes.InvalidValue],
            Faults(Steps("""
                { "outputs": [{ "name": " " }, { "name": "D", "location": "ftp://x" }],
                  "inputs": [{ "name": "E", "wait_hours": 100001 }, { "name": "F", "share_confirmed": "oui" }] }
                """)).Select(fault => fault.Code));
        Assert.Equal(FileErrorCodes.UnknownProperty, Assert.Single(Faults(Steps("""{ "outputs": [{ "name": "D", "wait_hours": 2 }] }"""))).Code);
    }

    [Fact]
    public void A_use_names_an_output_of_another_step_of_the_same_map_once()
    {
        var use = Read(Steps("""{ "outputs": [{ "id": "d1", "name": "Bon" }] }""",
            """{ "uses": [{ "document": "d1", "wait_hours": 2, "percent_complete_accurate": 90, "pain_point": " Ressaisie ", "lost_hours_per_month": 1.25 }] }"""))
            .Map.Steps[1].Sheet!.Uses.Single();
        Assert.Equal(new PmapUse("d1", 2m, 90, "Ressaisie", 1.3m), use);

        Assert.Equal(("map.steps[1].sheet.uses[0].document", FileErrorCodes.UnknownOutput, (object?)"d9"),
            Assert.Single(Faults(Steps("""{ "outputs": [{ "id": "d1", "name": "Bon" }] }""", """{ "uses": [{ "document": "d9" }] }""")))
                is var unknown ? (unknown.Path, unknown.Code, unknown.Values["document"]) : default);
        // Its own output, an external input, an output of another map: never a use (the rule of DocumentUses.Use).
        Assert.Equal(FileErrorCodes.UnknownOutput, Assert.Single(Faults(Steps("""{ "outputs": [{ "id": "d1", "name": "Bon" }], "uses": [{ "document": "d1" }] }"""))).Code);
        Assert.Equal(FileErrorCodes.UnknownOutput, Assert.Single(Faults(Steps("""{ "inputs": [{ "id": "d1", "name": "Bon" }] }""", """{ "uses": [{ "document": "d1" }] }"""))).Code);
        Assert.Equal(FileErrorCodes.UnknownOutput, Assert.Single(Faults(Doc($$"""
            { "lanes": [{{TwoLanes}}],
              "steps": [{ "id": "e1", "lane": "c1", "label": "Macro", "sub_map": { "lanes": [{ "id": "s1", "name": "S" }],
                          "steps": [{ "id": "s2", "lane": "s1", "label": "Dedans", "sheet": { "outputs": [{ "id": "d1", "name": "Bon" }] } }] } },
                        { "id": "e2", "lane": "c2", "label": "Utiliser", "sheet": { "uses": [{ "document": "d1" }] } }] }
            """))).Code);
        Assert.Equal(FileErrorCodes.DuplicateUse,
            Assert.Single(Faults(Steps("""{ "outputs": [{ "id": "d1", "name": "Bon" }] }""", """{ "uses": [{ "document": "d1" }, { "document": "d1" }] }"""))).Code);
    }

    [Fact]
    public void A_use_follows_the_rules_of_the_api_and_a_document_identifier_those_of_the_file()
    {
        string Use(string values) => Steps("""{ "outputs": [{ "id": "d1", "name": "Bon" }] }""", $$"""{ "uses": [{ "document": "d1", {{values}} }] }""");

        Assert.Equal(ErrorCodes.InvalidValue, Assert.Single(Faults(Use("\"percent_complete_accurate\": 101"))).Code);
        Assert.Equal(ErrorCodes.InvalidValue, Assert.Single(Faults(Use("\"percent_complete_accurate\": 50.5"))).Code);
        Assert.Equal(ErrorCodes.InvalidValue, Assert.Single(Faults(Use("\"lost_hours_per_month\": 100000"))).Code);
        Assert.Equal(ErrorCodes.TextTooLong, Assert.Single(Faults(Use($$"""
            "pain_point": "{{new string('x', 2001)}}"
            """))).Code);
        Assert.Equal(FileErrorCodes.Required, Assert.Single(Faults(Steps("""{ "outputs": [{ "id": "d1", "name": "Bon" }] }""", """{ "uses": [{ "wait_hours": 1 }] }"""))).Code);
        Assert.Equal(FileErrorCodes.DuplicateId, Assert.Single(Faults(Steps("""{ "outputs": [{ "id": "e2", "name": "Bon" }] }"""))).Code);
    }
}
