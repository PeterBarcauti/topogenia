// SheetsAndDocumentsPortedTests.cs
// The 1.x integration tests of the step sheets (T-FICI) that the sheet routes tests did not prove, through the whole host on both databases.

using System.Text.Json;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Host.Tests.Sheets;
using Topogenia.Host.Tests.Steps;
using Topogenia.Persistence.Tests.Infrastructure;
using static Topogenia.Host.Tests.Ported.SheetPortingCalls;

namespace Topogenia.Host.Tests.Ported;

/// <summary>
/// Each test carries the intention of one 1.x test of `FichesTests` (the number T-FICI-nn of `docs/portage-tests.md`);
/// the part already proved by `Sheets/SheetRoutesTests` is not repeated.
/// </summary>
public sealed class SheetsAndDocumentsPortedTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private async Task<ApiProject> ProjectAsync(EngineKind kind) => await ApiProject.CreateAsync(await hosts.OfAsync(kind));

    /// <summary>T-FICI-01: a new sheet is empty, without completeness, document nor responsible; nobody but the facilitator has a right.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_new_sheet_is_empty_without_document_nor_responsible_and_an_unassigned_contributor_has_no_right(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        var step = await StepSheets.StepAsync(project, 0, "Tableau croisé");
        using var facilitator = project.Facilitator;
        using var contributor = project.Contributor;
        using var reader = project.Reader;
        var sheet = await ReadAsync(facilitator, step);
        Assert.Equal(("empty", 0, JsonValueKind.Null), (StatusOf(sheet), CompletenessOf(sheet), sheet.GetProperty("responsible").ValueKind));
        Assert.All(new[] { "actor", "purpose", "tools", "frequency", "duration_minutes", "free_text" },
            field => Assert.Equal(JsonValueKind.Null, sheet.GetProperty("content").GetProperty(field).ValueKind));
        Assert.Equal((0, 0, 0), (sheet.GetProperty("content").GetProperty("procedure").GetArrayLength(), sheet.GetProperty("inputs").GetArrayLength(),
            sheet.GetProperty("outputs").GetArrayLength()));
        Assert.Equal((true, true, true, true), Rights(sheet));
        Assert.Equal((false, false, false, false), Rights(await ReadAsync(contributor, step)));
        Assert.Equal((false, false, false, false), Rights(await ReadAsync(reader, step)));
    }

    /// <summary>T-FICI-02: the contributor fills the step assigned to him (responsible from the step), another step only once he holds its lane.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_contributor_fills_the_step_assigned_to_him_and_another_step_of_the_lane_only_once_he_holds_the_lane(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        var step = await StepSheets.StepAsync(project, 0, "Tableau croisé");
        var other = await StepSheets.StepAsync(project, 1, "Envoyer");
        using var contributor = project.Contributor;
        using var reader = project.Reader;
        var purpose = StepSheets.Purpose("Suivre les ventes");
        (await contributor.PutAsync(SheetUrl(step), purpose)).Refused(403, "ACCES_REFUSE");
        (await reader.PutAsync(SheetUrl(step), purpose)).Refused(403, "ACCES_REFUSE");

        var contributorId = await AssignContributorAsync(project, step);
        var saved = (await SaveAsync(contributor, step, purpose)).Ok();
        Assert.Equal(("draft", true), (StatusOf(saved), Rights(saved).Fill));
        Assert.Equal((contributorId, "step"), (saved.GetProperty("responsible").GetProperty("user_id").GetGuid(), saved.GetProperty("responsible").GetProperty("origin").GetString()));

        (await contributor.PutAsync(SheetUrl(other), purpose)).Refused(403, "ACCES_REFUSE");
        await StepSheets.MakeContributorActorAsync(project);
        var held = (await SaveAsync(contributor, other, purpose)).Ok();
        Assert.Equal(("draft", "lane"), (StatusOf(held), held.GetProperty("responsible").GetProperty("origin").GetString()));
    }

    /// <summary>T-FICI-03: the draft is hidden from the reader on the sheet and on the map until its validation creates version 1.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_draft_is_hidden_from_the_reader_on_the_sheet_and_on_the_map_until_its_validation_publishes_version_one(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        var step = await StepSheets.StepAsync(project, 0, "Tableau croisé");
        await AssignContributorAsync(project, step);
        using var contributor = project.Contributor;
        using var facilitator = project.Facilitator;
        using var reader = project.Reader;
        (await SaveAsync(contributor, step, new
        {
            fields = new { purpose = new { value = "Suivre les ventes" } },
            procedure = new[] { new { text = "Filtrer l'extraction", tool = "Excel", nature = "extract" } },
            free_text = "Mon texte",
        })).Ok();

        var seen = await ReadAsync(facilitator, step);
        Assert.Equal(("Suivre les ventes", true), (StepSheets.PurposeOf(seen), seen.GetProperty("is_draft").GetBoolean()));
        var hidden = await ReadAsync(reader, step);
        Assert.Equal(("empty", (string?)null, 0, (string?)null), (StatusOf(hidden), StepSheets.PurposeOf(hidden),
            hidden.GetProperty("content").GetProperty("procedure").GetArrayLength(), hidden.GetProperty("content").GetProperty("free_text").GetString()));
        Assert.Equal(("empty", 0, false), await SummaryAsync(reader, project, step));

        var validated = (await ValidateAsync(contributor, step)).Ok();
        Assert.Equal(("validated", 1), (StatusOf(validated), validated.GetProperty("validated").GetProperty("number").GetInt32()));
        var published = await ReadAsync(reader, step);
        var content = published.GetProperty("content");
        Assert.Equal(("validated", false, "Suivre les ventes", "Mon texte"), (StatusOf(published), published.GetProperty("is_draft").GetBoolean(),
            StepSheets.PurposeOf(published), content.GetProperty("free_text").GetString()));
        var line = content.GetProperty("procedure").EnumerateArray().Single();
        Assert.Equal(("Filtrer l'extraction", "Excel", "extract"), (line.GetProperty("text").GetString(), line.GetProperty("tool").GetString(), line.GetProperty("nature").GetString()));
        Assert.Equal(validated.GetProperty("validated").GetProperty("validated_by").GetGuid(), published.GetProperty("updated_by").GetGuid());
    }

    /// <summary>T-FICI-04: an unchanged save keeps the validated state; a change is a draft while the others keep version 1; validating gives version 2.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task An_unchanged_save_keeps_a_sheet_validated_a_change_is_a_draft_while_the_others_keep_version_one_until_version_two(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        var step = await StepSheets.StepAsync(project, 0, "Tableau croisé");
        await AssignContributorAsync(project, step);
        using var contributor = project.Contributor;
        using var reader = project.Reader;
        (await SaveAsync(contributor, step, StepSheets.Purpose("Version 1"))).Ok();
        (await ValidateAsync(contributor, step)).Ok();

        Assert.Equal("validated", StatusOf((await SaveAsync(contributor, step, StepSheets.Purpose("Version 1"))).Ok()));
        var draft = (await SaveAsync(contributor, step, Fields(("purpose", "Version 2"), ("tools", "Excel")))).Ok();
        Assert.Equal(("draft", 1), (StatusOf(draft), draft.GetProperty("validated").GetProperty("number").GetInt32()));
        var kept = await ReadAsync(reader, step);
        Assert.Equal(("validated", "Version 1", (string?)null, 14), (StatusOf(kept), StepSheets.PurposeOf(kept),
            kept.GetProperty("content").GetProperty("tools").GetString(), CompletenessOf(kept)));
        Assert.Equal(("validated", 14, false), await SummaryAsync(reader, project, step));

        Assert.Equal(2, (await ValidateAsync(contributor, step)).Ok().GetProperty("validated").GetProperty("number").GetInt32());
        Assert.Equal("Version 2", StepSheets.PurposeOf(await ReadAsync(reader, step)));
    }

    /// <summary>T-FICI-05 (view of the map): four rubrics of seven, the prefilled actor not counted, on the sheet and on the map summary.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_completeness_counts_four_rubrics_of_seven_without_the_actor_on_the_sheet_and_on_the_map(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        var step = await StepSheets.StepAsync(project, 0, "Tableau croisé");
        using var facilitator = project.Facilitator;
        var saved = (await SaveAsync(facilitator, step, new
        {
            fields = new Dictionary<string, object>
            {
                ["actor"] = new { value = "Isabelle" },
                ["purpose"] = new { value = "Suivre les ventes" },
                ["frequency"] = new { value = "month_end_close" },
                ["duration_minutes"] = new { number = 45 },
            },
            procedure = new object[] { new { text = "Extraire", tool = "SAP VA05" }, new { text = "Croiser" } },
        })).Ok();
        Assert.Equal((57, 45m, 2), (CompletenessOf(saved), saved.GetProperty("content").GetProperty("duration_minutes").GetDecimal(),
            saved.GetProperty("content").GetProperty("procedure").GetArrayLength()));
        var summary = StructureCalls.StepOf(await project.MapAsync(), step).GetProperty("sheet");
        Assert.Equal(("draft", 57, true, "month_end_close", 45m), (summary.GetProperty("status").GetString(), summary.GetProperty("completeness").GetInt32(),
            summary.GetProperty("can_fill").GetBoolean(), summary.GetProperty("frequency").GetString(), summary.GetProperty("duration_minutes").GetDecimal()));
    }

    /// <summary>
    /// T-FICI-06: the procedure is trimmed and loses its empty lines; an unknown nature, a tool without text, 51 lines and HTML are
    /// refused. A null line, dropped by the 1.x, is a body of the wrong shape in 2.0 (A-2.18-06).
    /// </summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_procedure_is_trimmed_without_its_empty_lines_and_refuses_a_wrong_nature_a_lone_tool_fifty_one_lines_and_html(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        var step = await StepSheets.StepAsync(project, 0, "Tableau croisé");
        using var facilitator = project.Facilitator;
        var saved = (await SaveAsync(facilitator, step, new
        {
            procedure = new object[] { new { text = "  Extraire  ", tool = " SAP ", nature = "extract" }, new { text = "" }, new { text = "Contrôler les totaux", nature = "check" } },
        })).Ok();
        Assert.Equal([("Extraire", "SAP", "extract"), ("Contrôler les totaux", null, "check")], saved.GetProperty("content").GetProperty("procedure").EnumerateArray()
            .Select(line => (line.GetProperty("text").GetString(), line.GetProperty("tool").GetString(), line.GetProperty("nature").GetString())));

        var refused = new object[]
        {
            new[] { new { text = "x", nature = "inconnue" } },
            new[] { new { text = "", tool = "Excel" } },
            Enumerable.Range(0, 51).Select(rank => new { text = $"op {rank}" }).ToArray(),
        };
        foreach (var procedure in refused)
            Assert.Equal("procedure", (await SaveAsync(facilitator, step, new { procedure })).Refused(400, "VALEUR_INVALIDE").Field());
        (await SaveAsync(facilitator, step, new { procedure = new[] { new { text = "<b>x</b>" } } })).Refused(400, "TEXTE_HTML_INTERDIT");
        (await SaveAsync(facilitator, step, "{\"procedure\": [null]}")).Refused(400, "REQUETE_INVALIDE");
        Assert.Equal(2, (await ReadAsync(facilitator, step)).GetProperty("content").GetProperty("procedure").GetArrayLength());
    }

    /// <summary>T-FICI-07: an empty sheet is not created, a never validated sheet cleared becomes empty again and an empty sheet is not validated.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task An_empty_save_creates_no_sheet_and_a_never_validated_sheet_cleared_is_empty_again_and_not_validated(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        var step = await StepSheets.StepAsync(project, 0, "Tableau croisé");
        using var facilitator = project.Facilitator;
        var nothing = (await SaveAsync(facilitator, step, new { fields = new { } })).Ok();
        Assert.Equal(("empty", JsonValueKind.Null), (StatusOf(nothing), nothing.GetProperty("version").ValueKind));

        Assert.Equal("draft", StatusOf((await SaveAsync(facilitator, step, StepSheets.Purpose("x"))).Ok()));
        Assert.Equal("empty", StatusOf((await SaveAsync(facilitator, step, StepSheets.Purpose("  "))).Ok()));
        (await ValidateAsync(facilitator, step)).Refused(400, "FICHE_VIDE");
        Assert.Equal("empty", StatusOf(await ReadAsync(facilitator, step)));
    }

    /// <summary>T-FICI-08 (resumption): the author takes a sheet to review back as a draft that still shows the request; the next validation clears it.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_author_resumes_a_sheet_to_review_as_a_draft_still_showing_the_request_until_the_next_validation_clears_it(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        var step = await StepSheets.StepAsync(project, 0, "Tableau croisé");
        await AssignContributorAsync(project, step);
        using var contributor = project.Contributor;
        using var facilitator = project.Facilitator;
        (await SaveAsync(contributor, step, StepSheets.Purpose("Suivre les ventes"))).Ok();
        (await ValidateAsync(contributor, step)).Ok();
        const string Request = "Quel fichier est reçu exactement ?";
        var asked = (await facilitator.PostAsync(SheetUrl(step, "/clarification"), new { text = Request }, await StepSheets.ReadVersionAsync(facilitator, step))).Ok();
        Assert.Equal("needs_review", StatusOf(asked));

        var resumed = (await SaveAsync(contributor, step, Fields(("purpose", "Suivre les ventes"), ("tools", "SAP VA05")))).Ok();
        Assert.Equal(("draft", Request), (StatusOf(resumed), resumed.GetProperty("clarification_request").GetString()));
        var revalidated = (await ValidateAsync(contributor, step)).Ok();
        Assert.Equal(("validated", JsonValueKind.Null), (StatusOf(revalidated), revalidated.GetProperty("clarification_request").ValueKind));
    }

    /// <summary>T-FICI-10: an unknown or retired key, a frequency out of the list, a wrong duration and a number on a text field are refused on their field.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Every_wrong_value_of_the_1x_is_refused_with_the_name_of_its_field(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        var step = await StepSheets.StepAsync(project, 0, "Tableau croisé");
        using var facilitator = project.Facilitator;
        (string Key, string? Text, decimal? Number)[] cases =
        [
            ("inconnu", "x", null), ("action", "ancienne question", null), ("frequency", "parfois", null), ("duration_minutes", null, -5m),
            ("duration_minutes", "45", null), ("duration_minutes", null, 100001m), ("purpose", "x", 3m),
        ];
        foreach (var (key, text, number) in cases)
        {
            var body = new { fields = new Dictionary<string, object> { [key] = new { value = text, number } } };
            Assert.Equal(key, (await SaveAsync(facilitator, step, body)).Refused(400, "VALEUR_INVALIDE").Field());
        }
        Assert.Equal("empty", StatusOf(await ReadAsync(facilitator, step)));
    }

    /// <summary>T-FICI-11: HTML and the lengths 4 000 and 20 000 are refused; the free text alone is saved without counting in the completeness.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Html_and_overlong_texts_are_refused_and_the_free_text_alone_is_saved_without_completeness(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        var step = await StepSheets.StepAsync(project, 0, "Tableau croisé");
        using var facilitator = project.Facilitator;
        (await SaveAsync(facilitator, step, StepSheets.Purpose("<b>gras</b>"))).Refused(400, "TEXTE_HTML_INTERDIT");
        (await SaveAsync(facilitator, step, StepSheets.Purpose(new string('x', 4001)))).Refused(400, "TEXTE_TROP_LONG");
        (await SaveAsync(facilitator, step, new { free_text = new string('x', 20001) })).Refused(400, "TEXTE_TROP_LONG");
        Assert.Equal("empty", StatusOf(await ReadAsync(facilitator, step)));

        var saved = (await SaveAsync(facilitator, step, new { free_text = "L'extraction arrive et elle est retravaillée." })).Ok();
        Assert.Equal(("draft", 0, "L'extraction arrive et elle est retravaillée."), (StatusOf(saved), CompletenessOf(saved),
            saved.GetProperty("content").GetProperty("free_text").GetString()));
    }

    /// <summary>T-FICI-12: the suggestions show only what the user may see, and nothing to the one who does not fill the sheet.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_suggestions_of_frequency_and_tools_show_only_what_the_user_may_see_and_nothing_to_a_reader(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        var step = await StepSheets.StepAsync(project, 0, "Tableau croisé");
        var next = await StepSheets.StepAsync(project, 1, "Envoyer");
        await AssignContributorAsync(project, next);
        using var facilitator = project.Facilitator;
        using var contributor = project.Contributor;
        using var reader = project.Reader;
        (await SaveAsync(facilitator, step, new
        {
            fields = new { frequency = new { value = "monthly" }, tools = new { value = "Excel, SAP VA05" } },
            procedure = new[] { new { text = "Publier", tool = "Power BI" } },
        })).Ok();
        var code = StructureCalls.StepOf(await project.MapAsync(), step).GetProperty("code").GetString();
        static (string? Frequency, string? Code) FrequencyOf(JsonElement sheet) =>
            sheet.GetProperty("suggestions").GetProperty("frequency") is { ValueKind: JsonValueKind.Object } frequency
                ? (frequency.GetProperty("frequency").GetString(), frequency.GetProperty("step_code").GetString())
                : (null, null);
        static IEnumerable<string?> ToolsOf(JsonElement sheet) => sheet.GetProperty("suggestions").GetProperty("tools").EnumerateArray().Select(tool => tool.GetString());

        var ofFacilitator = await ReadAsync(facilitator, next);
        Assert.Equal(("monthly", code), FrequencyOf(ofFacilitator));
        Assert.Equal(["Excel", "Power BI", "SAP VA05"], ToolsOf(ofFacilitator));
        var ofContributor = await ReadAsync(contributor, next);
        Assert.Equal(((string?)null, (string?)null), FrequencyOf(ofContributor));
        Assert.Empty(ToolsOf(ofContributor));

        (await ValidateAsync(facilitator, step)).Ok();
        ofContributor = await ReadAsync(contributor, next);
        Assert.Equal(("monthly", code), FrequencyOf(ofContributor));
        Assert.Equal(["Excel", "Power BI", "SAP VA05"], ToolsOf(ofContributor));
        var ofReader = await ReadAsync(reader, next);
        Assert.Equal(((string?)null, (string?)null), FrequencyOf(ofReader));
        Assert.Empty(ToolsOf(ofReader));
    }
}
