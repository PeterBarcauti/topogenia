// PmapReaderTests.cs
// The reading of a .pmap 1.1 file (annex A 2.17, spec 7.5, ADR 054), ported from the 1.x FormatPmapTests: every rule with its place and its code.

using Topogenia.Domain.Errors;
using Topogenia.Domain.Values;
using Topogenia.Interchange.Pmap;
using Topogenia.Interchange.Tests.Support;

namespace Topogenia.Interchange.Tests.Pmap;

public sealed class PmapReaderTests
{
    public static readonly LanguageSettings Languages = new(["fr", "en"], "fr", new Dictionary<string, string> { ["fr"] = "Acteur 1", ["en"] = "Actor 1" });

    public static string Sample(string name) => File.ReadAllText(Path.Combine(RepositoryRoot.Path, "samples", name));

    public static PmapDocument Read(string text, string letters = "ACI")
    {
        var read = PmapReader.Read(text, new PmapReadSettings(Languages, letters));
        Assert.True(read.Document is not null, string.Join("\n", read.Errors.Select(error => $"{error.Path} : {error.Code}")));
        return read.Document!;
    }

    public static List<FileError> Faults(string text, string letters = "ACI")
    {
        var read = PmapReader.Read(text, new PmapReadSettings(Languages, letters));
        Assert.Null(read.Document);
        Assert.NotEmpty(read.Errors);
        return [.. read.Errors];
    }

    /// <summary>A minimal valid document around a map given in JSON.</summary>
    public static string Doc(string map, string project = """{ "name": "Essai" }""") =>
        $$"""{ "format": "topogenia", "version": "1.1", "project": {{project}}, "map": {{map}} }""";

    public const string TwoLanes = """{ "id": "c1", "name": "Demandeur" }, { "id": "c2", "name": "Acheteur" }""";

    private static int Steps(PmapMap map) => map.Steps.Count + map.Steps.Sum(step => step.SubMap is null ? 0 : Steps(step.SubMap));

    private static int Maps(PmapMap map) => 1 + map.Steps.Sum(step => step.SubMap is null ? 0 : Maps(step.SubMap));

    [Fact]
    public void The_minimal_sample_reads_with_its_defaults()
    {
        var document = Read(Sample("minimal.pmap"));

        Assert.Equal(("Demande d'achat", (string?)null, "fr"), (document.Project.Name, document.Project.Code, document.Project.Language));
        Assert.Equal(["Demandeur", "Acheteur"], document.Map.Lanes.Select(lane => lane.Name));
        Assert.Equal(["person", "department"], document.Map.Lanes.Select(lane => lane.Type));
        Assert.Equal(["start", "task", "task", "end"], document.Map.Steps.Select(step => step.Type));
        Assert.All(document.Map.Steps, step => Assert.Null(step.Column));
        Assert.Equal(3, document.Map.Flows.Count);
    }

    [Fact]
    public void The_complete_sample_reads_its_sub_map_raci_translations_sheets_and_uncertainties()
    {
        var document = Read(Sample("cloture-mensuelle.pmap"));

        Assert.Equal("VTE", document.Project.Code);
        Assert.Equal("Monthly sales closing", document.Project.Translations["en"]);
        Assert.Equal("Controlling North", document.Map.Lanes[0].Translations["en"]);
        var table = document.Map.Steps.Single(step => step.Id == "tcd");
        Assert.Equal(new Dictionary<string, string> { ["dir"] = "A", ["sud"] = "I" }, table.Raci);
        Assert.Equal(3, table.SubMap!.Steps.Count);
        Assert.Equal("Build the pivot table", table.Translations["en"]);
        Assert.Equal(("month_end_close", 90m, 3), (table.Sheet!.Frequency, table.Sheet.DurationMinutes, table.Sheet.Procedure.Count));
        Assert.Equal("copy_paste", table.Sheet.Procedure[1].Nature);
        Assert.Equal(("tableau", "Tableau croisé des ventes"), (table.Sheet.Outputs[0].Id, table.Sheet.Outputs[0].Name));
        var use = Assert.Single(document.Map.Steps.Single(step => step.Id == "complet").Sheet!.Uses);
        Assert.Equal(("tableau", 24m, 80, 3m), (use.Document, use.WaitHours, use.PercentCompleteAccurate, use.LostHoursPerMonth));
        Assert.Equal("automated", document.Map.Steps.Single(step => step.Id == "extraire").Automation);
        Assert.Equal(["oui", "non"], document.Map.Flows.Where(flow => flow.Source == "ecart").Select(flow => flow.Label));
        Assert.Equal("yes", document.Map.Flows.First(flow => flow.Source == "ecart").Translations["en"]);
        Assert.Single(document.Uncertainties);
        Assert.Equal((11, 2), (Steps(document.Map), Maps(document.Map)));
    }

    [Fact]
    public void A_copy_paste_from_an_ai_is_read_code_block_byte_order_mark_trailing_commas_and_comments()
    {
        var json = Sample("minimal.pmap");

        Assert.Equal(4, Read($"Voici le fichier :\n```json\n{json}\n```".Split('\n', 2)[1]).Map.Steps.Count);
        Assert.Equal(4, Read($"```json\n{json}\n```\n").Map.Steps.Count);
        Assert.Equal(4, Read((char)0xFEFF + json).Map.Steps.Count);
        var tolerant = json.Replace("""{ "source": "e3", "target": "e4" }""", """{ "source": "e3", "target": "e4" }, // last arrow""", StringComparison.Ordinal);
        Assert.Equal(3, Read(tolerant).Map.Flows.Count);
    }

    [Fact]
    public void Neither_json_nor_the_format_nor_its_version_is_read()
    {
        Assert.Equal(FileErrorCodes.Empty, Assert.Single(Faults("  ")).Code);
        var unreadable = Assert.Single(Faults("{\n  \"format\": \"topogenia\",\n  \"version\" \"1.1\"\n}"));
        Assert.Equal((FileErrorCodes.NotJson, (object?)3L, (object?)13L), (unreadable.Code, unreadable.Values["line"], unreadable.Values["column"]));
        Assert.Equal(FileErrorCodes.NotObject, Assert.Single(Faults("[1, 2]")).Code);
        var format = Assert.Single(Faults("""{ "format": "ariane", "version": "1.0" }"""));
        Assert.Equal(("format", FileErrorCodes.WrongFormat, (object?)"topogenia"), (format.Path, format.Code, format.Values["expected"]));
        foreach (var version in new[] { "1.0", "1.3", "2.0" })
        {
            var refused = Assert.Single(Faults(Doc($$"""{ "lanes": [{{TwoLanes}}] }""").Replace("\"1.1\"", $"\"{version}\"", StringComparison.Ordinal)));
            Assert.Equal(("version", FileErrorCodes.WrongVersion, (object?)version, (object?)"1.1, 1.2"),
                (refused.Path, refused.Code, refused.Values["version"], refused.Values["supported"]));
        }
    }

    [Fact]
    public void An_unknown_property_is_reported_with_the_possible_ones()
    {
        var fault = Assert.Single(Faults(Doc($$"""{ "lanes": [{{TwoLanes}}], "steps": [{ "id": "e1", "lane": "c1", "label": "Bon", "libele": "Faute" }] }""")));

        Assert.Equal(("map.steps[0].libele", FileErrorCodes.UnknownProperty, (object?)"libele"), (fault.Path, fault.Code, fault.Values["name"]));
        Assert.Equal("id, lane, label, type, column, row, automation, raci, sub_map, translations, sheet", fault.Values["possible"]);
    }

    [Fact]
    public void Required_fields_types_and_values_are_checked_each_at_its_place()
    {
        var faults = Faults(Doc("""
            { "lanes": [{ "id": "c1", "name": "  ", "type": "robot", "color": "bleu" }],
              "steps": [{ "id": "e1", "lane": "c1", "type": "tache", "column": -1, "automation": "oui" }, { "lane": "c1", "label": 12 }] }
            """, """{ "language": "de" }"""));

        Assert.Equal([
            ("project.name", FileErrorCodes.Required),
            ("project.language", FileErrorCodes.UnknownLanguage),
            ("map.lanes[0].name", FileErrorCodes.EmptyText),
            ("map.lanes[0].type", FileErrorCodes.InvalidValue),
            ("map.lanes[0].color", FileErrorCodes.InvalidColor),
            ("map.steps[0].label", FileErrorCodes.Required),
            ("map.steps[0].type", FileErrorCodes.InvalidValue),
            ("map.steps[0].automation", FileErrorCodes.InvalidValue),
            ("map.steps[0].column", FileErrorCodes.InvalidColumn),
            ("map.steps[1].id", FileErrorCodes.Required),
            ("map.steps[1].label", FileErrorCodes.NotAText),
        ], faults.Select(fault => (fault.Path, fault.Code)));
        Assert.Equal("person, department, system", faults[3].Values["possible"]);
        Assert.Equal("task, decision, parallel, inclusive, wait, start, end", faults[6].Values["possible"]);
        Assert.Equal("manual, in_progress, automated", faults[7].Values["possible"]);
        Assert.Equal(10_000, faults[8].Values["max"]);
    }

    [Fact]
    public void Texts_follow_the_rules_of_every_entered_text_of_the_domain()
    {
        var faults = Faults(Doc($$"""
            { "lanes": [{ "id": "c1", "name": "{{new string('x', 151)}}" }, { "id": "c2", "name": "<b>gras</b>" }, { "id": "c3", "name": "A\u0007B" }] }
            """));

        Assert.Equal([ErrorCodes.TextTooLong, ErrorCodes.HtmlForbidden, ErrorCodes.InvalidValue], faults.Select(fault => fault.Code));
        Assert.Equal(("name", (object?)150), ((string?)faults[0].Values["field"], faults[0].Values["max"]));
        // "a < b" is no tag; the surrounding spaces are removed.
        Assert.Equal("Délai < 2 jours ?", Read(Doc("""{ "lanes": [{ "id": "c1", "name": "  Délai < 2 jours ?  " }] }""")).Map.Lanes[0].Name);
    }

    [Fact]
    public void Identifiers_are_unique_in_the_whole_file_and_never_empty()
    {
        var faults = Faults(Doc($$"""
            { "lanes": [{{TwoLanes}}],
              "steps": [{ "id": "c1", "lane": "c1", "label": "Même id qu'un couloir" },
                         { "id": "e2", "lane": "c1", "label": "Macro", "sub_map": { "lanes": [{ "id": "c2", "name": "Doublon" }] } }] }
            """));

        Assert.Equal([("map.steps[0].id", FileErrorCodes.DuplicateId), ("map.steps[1].sub_map.lanes[0].id", FileErrorCodes.DuplicateId)],
            faults.Select(fault => (fault.Path, fault.Code)));
        Assert.Equal(FileErrorCodes.EmptyId, Assert.Single(Faults(Doc("""{ "lanes": [{ "id": "", "name": "A" }] }"""))).Code);
        var invalid = Faults(Doc($$"""{ "lanes": [{ "id": "{{new string('i', 65)}}", "name": "A" }, { "id": " c2", "name": "B" }, { "id": "c3 ", "name": "C" }] }"""))
            .Where(fault => fault.Code == FileErrorCodes.InvalidId).ToList();
        // 65 characters, then a space before and a space after: three identifiers refused, each at its place.
        Assert.Equal(["map.lanes[0].id", "map.lanes[1].id", "map.lanes[2].id"], invalid.Select(fault => fault.Path));
        Assert.Equal((object?)64, invalid[0].Values["max"]);
    }

    [Fact]
    public void Rule_1_a_step_is_on_a_lane_of_its_own_map()
    {
        var faults = Faults(Doc($$"""
            { "lanes": [{{TwoLanes}}],
              "steps": [{ "id": "e1", "lane": "c9", "label": "Perdue" },
                         { "id": "e2", "lane": "c1", "label": "Macro", "sub_map": {
                             "lanes": [{ "id": "s1", "name": "Interne" }],
                             "steps": [{ "id": "e3", "lane": "c2", "label": "Couloir du parent" }] } }] }
            """));

        Assert.Equal([("map.steps[0].lane", FileErrorCodes.UnknownLane, (object?)"c9"), ("map.steps[1].sub_map.steps[0].lane", FileErrorCodes.UnknownLane, "c2")],
            faults.Select(fault => (fault.Path, fault.Code, fault.Values["lane"])));
    }

    [Fact]
    public void Rules_2_and_3_flows_stay_in_their_map_without_loop_nor_duplicate()
    {
        var faults = Faults(Doc($$"""
            { "lanes": [{{TwoLanes}}],
              "steps": [{ "id": "e1", "lane": "c1", "label": "Un" },
                         { "id": "e2", "lane": "c1", "label": "Deux", "sub_map": {
                             "lanes": [{ "id": "s1", "name": "Interne" }],
                             "steps": [{ "id": "e3", "lane": "s1", "label": "Dedans" }],
                             "flows": [{ "source": "e3", "target": "e1" }] } }],
              "flows": [{ "source": "e1", "target": "e2" }, { "source": "e1", "target": "e2" }, { "source": "e1", "target": "e1" },
                        { "source": "e1", "target": "e3" }, { "source": "e1", "target": "e9" }, { "source": "e2", "target": "e1", "label": "{{new string('x', 61)}}" }] }
            """));

        Assert.Equal([
            ("map.steps[1].sub_map.flows[0].target", FileErrorCodes.StepInOtherMap),
            ("map.flows[1]", FileErrorCodes.DuplicateFlow),
            ("map.flows[2]", FileErrorCodes.SelfFlow),
            ("map.flows[3].target", FileErrorCodes.StepInOtherMap),
            ("map.flows[4].target", FileErrorCodes.UnknownStep),
            ("map.flows[5].label", ErrorCodes.TextTooLong),
        ], faults.Select(fault => (fault.Path, fault.Code)));
        Assert.Equal(("e1", "e2"), ((string?)faults[1].Values["source"], (string?)faults[1].Values["target"]));
    }

    [Fact]
    public void Rule_4_columns_for_all_steps_or_none_and_never_two_steps_in_one_cell()
    {
        var partial = Assert.Single(Faults(Doc($$"""
            { "lanes": [{{TwoLanes}}], "steps": [{ "id": "e1", "lane": "c1", "label": "Un", "column": 0 }, { "id": "e2", "lane": "c1", "label": "Deux" }] }
            """)));
        Assert.Equal(("map.steps", FileErrorCodes.PartialColumns), (partial.Path, partial.Code));

        var same = Assert.Single(Faults(Doc($$"""
            { "lanes": [{{TwoLanes}}], "steps": [{ "id": "e1", "lane": "c1", "label": "Un", "column": 2 },
              { "id": "e2", "lane": "c2", "label": "Deux", "column": 2 }, { "id": "e3", "lane": "c1", "label": "Trois", "column": 2 }] }
            """)));
        Assert.Equal(("map.steps[0]", FileErrorCodes.SameCell, (object?)"c1", (object?)2, (object?)"e1, e3"),
            (same.Path, same.Code, same.Values["lane"], same.Values["column"], same.Values["steps"]));
        var rows = Read(Doc($$"""
            { "lanes": [{{TwoLanes}}], "steps": [{ "id": "e1", "lane": "c1", "label": "Un", "column": 2 }, { "id": "e2", "lane": "c1", "label": "Deux", "column": 2, "row": 1 }] }
            """));
        Assert.Equal([0, 1], rows.Map.Steps.Select(step => step.Row));
        Assert.Equal(FileErrorCodes.InvalidRow, Assert.Single(Faults(Doc($$"""{ "lanes": [{{TwoLanes}}], "steps": [{ "id": "e1", "lane": "c1", "label": "Un", "row": 51 }] }"""))).Code);
    }

    [Fact]
    public void Rule_5_no_detail_on_a_step_without_content_and_an_empty_sub_map_is_left_out()
    {
        var fault = Assert.Single(Faults(Doc($$"""
            { "lanes": [{{TwoLanes}}], "steps": [{ "id": "e1", "lane": "c1", "label": "Début", "type": "start",
              "sub_map": { "lanes": [{ "id": "s1", "name": "X" }] } }] }
            """)));
        Assert.Equal(("map.steps[0].sub_map", FileErrorCodes.SubMapOnEmptyStep), (fault.Path, fault.Code));

        var document = Read(Doc($$"""
            { "lanes": [{{TwoLanes}}], "steps": [{ "id": "e1", "lane": "c1", "label": "Rien dedans",
              "sub_map": { "lanes": [{ "id": "s1", "name": "X" }], "steps": [] } }] }
            """));
        Assert.Null(document.Map.Steps[0].SubMap);
    }

    [Fact]
    public void Sub_maps_go_twelve_levels_deep_at_most()
    {
        static string Nested(int levels, int level = 1) => level > levels
            ? $$"""{ "lanes": [{ "id": "l{{level}}", "name": "X" }], "steps": [{ "id": "s{{level}}", "lane": "l{{level}}", "label": "Fin" }] }"""
            : $$"""{ "lanes": [{ "id": "l{{level}}", "name": "X" }], "steps": [{ "id": "s{{level}}", "lane": "l{{level}}", "label": "N", "sub_map": {{Nested(levels, level + 1)}} }] }""";

        Assert.Equal(12, Maps(Read(Doc(Nested(11))).Map));
        var fault = Assert.Single(Faults(Doc(Nested(12))));
        Assert.Equal((FileErrorCodes.TooDeep, (object?)12), (fault.Code, fault.Values["max"]));
    }

    [Fact]
    public void Rule_6_raci()
    {
        string Map(string raci, string type = "task") => Doc($$"""
            { "lanes": [{{TwoLanes}}, { "id": "c3", "name": "Direction" }],
              "steps": [{ "id": "e1", "lane": "c1", "label": "Valider", "type": "{{type}}", "raci": {{raci}} }] }
            """);

        Assert.Equal(new Dictionary<string, string> { ["c2"] = "C", ["c3"] = "A" }, Read(Map("""{ "c1": "R", "c2": "C", "c3": "A" }""")).Map.Steps[0].Raci);
        var faults = Faults(Map("""{ "c2": "R", "c3": "S", "c9": "I" }"""));
        Assert.Equal([FileErrorCodes.RaciRElsewhere, FileErrorCodes.RaciInvalidRole, FileErrorCodes.UnknownLane], faults.Select(fault => fault.Code));
        Assert.Equal(("c2", "c1", "A, C, I"), ((string?)faults[0].Values["lane"], (string?)faults[0].Values["own"], (string?)faults[0].Values["letters"]));
        Assert.Equal(FileErrorCodes.RaciOwnLane, Assert.Single(Faults(Map("""{ "c1": "A" }"""))).Code);
        Assert.Equal(FileErrorCodes.RaciSingleAccountable, Assert.Single(Faults(Map("""{ "c2": "A", "c3": "A" }"""))).Code);
        // RASCI: S is admitted when the setting gives it.
        Assert.Equal("S", Read(Map("""{ "c3": "S" }"""), "ASCI").Map.Steps[0].Raci["c3"]);
        // Spec 7.5: no RACI on a start, an end or an AND or inclusive OR gateway; a decision and a wait take one.
        foreach (var type in new[] { "start", "end", "parallel", "inclusive" })
            Assert.Equal(("map.steps[0].raci", FileErrorCodes.RaciOnStepType), Assert.Single(Faults(Map("""{ "c2": "C" }""", type))) is var fault ? (fault.Path, fault.Code) : default);
        Assert.Equal("C", Read(Map("""{ "c2": "C" }""", "decision")).Map.Steps[0].Raci["c2"]);
    }

    [Fact]
    public void Texts_of_the_project_and_of_a_step_are_bounded_and_a_colour_in_lower_case_is_put_in_upper_case()
    {
        var faults = Faults(Doc($$"""
            { "lanes": [{ "id": "c1", "name": "A" }], "steps": [{ "id": "e1", "lane": "c1", "label": "{{new string('x', 151)}}" }] }
            """, $$"""{ "name": "{{new string('n', 201)}}", "description": "{{new string('d', 4001)}}", "translations": { "en": "{{new string('t', 201)}}" } }"""));

        Assert.Equal([("project.name", (object?)200), ("project.description", 4000), ("project.translations.en", 200), ("map.steps[0].label", 150)],
            faults.Select(fault => (fault.Path, fault.Values["max"])));
        Assert.All(faults, fault => Assert.Equal(ErrorCodes.TextTooLong, fault.Code));
        var document = Read(Doc("""{ "lanes": [{ "id": "c1", "name": "A", "color": "#e3f2fd" }] }""", """{ "name": "Essai", "description": "Périmètre", "translations": { "en": "Trial" } }"""));
        Assert.Equal(("#E3F2FD", "Périmètre", "Trial"), (document.Map.Lanes[0].Color, document.Project.Description, document.Project.Translations["en"]));
    }

    [Fact]
    public void A_code_of_project_malformed_is_left_for_the_opening_to_ask()
    {
        Assert.Null(Read(Doc($$"""{ "lanes": [{{TwoLanes}}] }""", """{ "name": "Essai", "code": "trop-long" }""")).Project.Code);
        Assert.Equal("VTE", Read(Doc($$"""{ "lanes": [{{TwoLanes}}] }""", """{ "name": "Essai", "code": "VTE" }""")).Project.Code);
    }

    [Fact]
    public void Uncertainties_are_texts_of_500_characters_and_50_are_kept()
    {
        var many = string.Join(",", Enumerable.Range(0, 60).Select(i => $"\"Point {i}\""));
        var document = Read(Doc($$"""{ "lanes": [{{TwoLanes}}] }""").Replace("\"map\":", $"\"uncertainties\": [{many}], \"map\":", StringComparison.Ordinal));
        Assert.Equal(50, document.Uncertainties.Count);
        var faults = Faults(Doc($$"""{ "lanes": [{{TwoLanes}}] }""").Replace("\"map\":", $"\"uncertainties\": [3, \"{new string('x', 501)}\"], \"map\":", StringComparison.Ordinal));
        Assert.Equal([("uncertainties[0]", FileErrorCodes.UncertaintyNotText), ("uncertainties[1]", ErrorCodes.TextTooLong)], faults.Select(fault => (fault.Path, fault.Code)));
    }

    [Fact]
    public void Limits_lanes_of_a_map_steps_of_the_file_and_the_faults_reported()
    {
        var lanes = string.Join(",", Enumerable.Range(0, 201).Select(i => $$"""{ "id": "c{{i}}", "name": "C" }"""));
        Assert.Equal(FileErrorCodes.TooManyLanes, Assert.Single(Faults(Doc($$"""{ "lanes": [{{lanes}}] }"""))).Code);
        Assert.Equal(FileErrorCodes.MapWithoutLane, Assert.Single(Faults(Doc("""{ "lanes": [] }"""))).Code);
        var steps = string.Join(",", Enumerable.Range(0, 5001).Select(i => $$"""{ "id": "e{{i}}", "lane": "c1", "label": "X" }"""));
        var tooMany = Assert.Single(Faults(Doc($$"""{ "lanes": [{{TwoLanes}}], "steps": [{{steps}}] }""")));
        Assert.Equal((FileErrorCodes.TooManySteps, (object?)5001, (object?)5000), (tooMany.Code, tooMany.Values["count"], tooMany.Values["max"]));
        var wrong = string.Join(",", Enumerable.Range(0, 100).Select(i => $$"""{ "id": "e{{i}}", "lane": "c9", "label": "X" }"""));
        Assert.Equal(PmapFormat.ErrorsMax, Faults(Doc($$"""{ "lanes": [{{TwoLanes}}], "steps": [{{wrong}}] }""")).Count);
    }

    [Fact]
    public void Half_of_a_surrogate_pair_in_a_text_or_a_name_is_not_json_that_is_read()
    {
        // Escaped, it is JSON that the parser accepts; no text holds it (a 500 before the gate P5, round 2).
        foreach (var project in new[] { """{ "name": "Essai\ud800" }""", """{ "name": "Essai", "translations": { "en\udc00": "Trial" } }""" })
        {
            var broken = Assert.Single(Faults(Doc($$"""{ "lanes": [{{TwoLanes}}] }""", project)));
            Assert.Equal((FileErrorCodes.NotJson, ""), (broken.Code, broken.Path));
        }
    }

    [Fact]
    public void A_json_deeper_than_256_levels_is_not_read()
    {
        var deep = string.Concat(Enumerable.Repeat("[", 300)) + string.Concat(Enumerable.Repeat("]", 300));
        Assert.Equal(FileErrorCodes.NotJson, Assert.Single(Faults(Doc($$"""{ "lanes": [{{TwoLanes}}], "steps": {{deep}} }"""))).Code);
    }

    [Fact]
    public void Translations_of_steps_and_flows_are_read_in_the_configured_languages()
    {
        var document = Read(Doc($$"""
            { "lanes": [{{TwoLanes}}], "steps": [{ "id": "e1", "lane": "c1", "label": "Un", "translations": { "en": "One" } },
              { "id": "e2", "lane": "c2", "label": "Deux" }], "flows": [{ "source": "e1", "target": "e2", "label": "ok", "translations": { "en": "fine" } }] }
            """));
        Assert.Equal(("One", "fine"), (document.Map.Steps[0].Translations["en"], document.Map.Flows[0].Translations["en"]));
        var faults = Faults(Doc($$"""
            { "lanes": [{{TwoLanes}}], "steps": [{ "id": "e1", "lane": "c1", "label": "Un", "translations": { "de": "Eins", "en": 1 } }] }
            """));
        Assert.Equal([("map.steps[0].translations.de", FileErrorCodes.UnknownLanguage), ("map.steps[0].translations.en", FileErrorCodes.TranslationNotText)],
            faults.Select(fault => (fault.Path, fault.Code)));
    }
}
