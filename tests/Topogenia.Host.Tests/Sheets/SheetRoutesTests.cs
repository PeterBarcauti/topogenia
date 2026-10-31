// SheetRoutesTests.cs
// The routes of the sheet of a step through the whole host on both databases: read, draft, validation, clarification asked, changed and withdrawn (annex A 1, A 2.10, A 2.16, spec 3.0 section 16).

using System.Globalization;
using System.Text.Json;
using Topogenia.Host.Tests.Api;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Sheets;

public sealed class SheetRoutesTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private async Task<ApiProject> ProjectAsync(EngineKind kind) => await ApiProject.CreateAsync(await hosts.OfAsync(kind));

    private static string Url(Guid stepId, string tail = "") => $"/api/steps/{stepId}/sheet{tail}";

    private static (bool Fill, bool Validate, bool Clarify, bool Assign) Rights(JsonElement sheet)
    {
        var rights = sheet.GetProperty("permissions");
        return (rights.GetProperty("fill").GetBoolean(), rights.GetProperty("validate").GetBoolean(),
            rights.GetProperty("request_clarification").GetBoolean(), rights.GetProperty("assign").GetBoolean());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_sheet_of_a_new_step_is_empty_with_the_rights_of_the_reader_and_a_stranger_learns_nothing(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        var step = await StepSheets.StepAsync(project, 0);
        using var facilitator = project.Facilitator;
        using var reader = project.Reader;
        using var stranger = project.Stranger;
        var sheet = (await facilitator.GetAsync(Url(step))).Ok();
        Assert.Equal((step, "empty", JsonValueKind.Null), (sheet.GetProperty("step_id").GetGuid(), sheet.GetProperty("status").GetString(), sheet.GetProperty("version").ValueKind));
        Assert.Equal((true, true, true, true), Rights(sheet));
        Assert.Equal(0, sheet.GetProperty("content").GetProperty("procedure").GetArrayLength());
        Assert.Equal((false, false, false, false), Rights((await reader.GetAsync(Url(step))).Ok()));
        (await stranger.GetAsync(Url(step))).Refused(404, "INTROUVABLE");
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_saved_draft_is_answered_as_the_sheet_and_the_reader_sees_only_the_validated_version(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        var step = await StepSheets.StepAsync(project, 0);
        using var facilitator = project.Facilitator;
        using var reader = project.Reader;
        var saved = (await facilitator.PutAsync(Url(step), new
        {
            fields = new { purpose = new { value = "Passer la commande" }, frequency = new { value = "weekly" }, duration_minutes = new { number = 30 } },
            procedure = new[] { new { text = "Saisir le bon", tool = "ERP", nature = "re_enter" } },
            free_text = "Voir le classeur",
        }, await StepSheets.ReadVersionAsync(facilitator, step))).Ok();
        var content = saved.GetProperty("content");
        Assert.Equal(("draft", true), (saved.GetProperty("status").GetString(), saved.GetProperty("is_draft").GetBoolean()));
        Assert.Equal(("Passer la commande", "weekly", 30m, "Voir le classeur"), (content.GetProperty("purpose").GetString(),
            content.GetProperty("frequency").GetString(), content.GetProperty("duration_minutes").GetDecimal(), content.GetProperty("free_text").GetString()));
        Assert.Equal(("Saisir le bon", "ERP", "re_enter"), (content.GetProperty("procedure")[0].GetProperty("text").GetString(),
            content.GetProperty("procedure")[0].GetProperty("tool").GetString(), content.GetProperty("procedure")[0].GetProperty("nature").GetString()));
        var hidden = (await reader.GetAsync(Url(step))).Ok();
        Assert.Equal(("empty", (string?)null, JsonValueKind.Null), (hidden.GetProperty("status").GetString(), StepSheets.PurposeOf(hidden), hidden.GetProperty("version").ValueKind));

        var validated = (await facilitator.PostAsync(Url(step, "/validate"), null, StepSheets.VersionOf(saved))).Ok();
        Assert.Equal(("validated", 1), (validated.GetProperty("status").GetString(), validated.GetProperty("validated").GetProperty("number").GetInt32()));
        (await facilitator.PutAsync(Url(step), StepSheets.Purpose("Commander autrement"), StepSheets.VersionOf(validated))).Ok();
        var published = (await reader.GetAsync(Url(step))).Ok();
        Assert.Equal(("validated", "Passer la commande", false), (published.GetProperty("status").GetString(), StepSheets.PurposeOf(published), published.GetProperty("is_draft").GetBoolean()));
        Assert.Equal("Commander autrement", StepSheets.PurposeOf((await facilitator.GetAsync(Url(step))).Ok()));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_draft_sent_without_a_rubric_filled_before_erases_it(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        var step = await StepSheets.StepAsync(project, 0);
        using var facilitator = project.Facilitator;
        var first = (await facilitator.PutAsync(Url(step), new
        {
            fields = new { purpose = new { value = "Passer la commande" }, tools = new { value = "ERP" } },
            procedure = new[] { new { text = "Saisir le bon", tool = "ERP", nature = "re_enter" } },
        }, await StepSheets.ReadVersionAsync(facilitator, step))).Ok();
        Assert.Equal(("ERP", 1), (first.GetProperty("content").GetProperty("tools").GetString(), first.GetProperty("content").GetProperty("procedure").GetArrayLength()));

        // The content sent is complete: the tools and the procedure left out are erased, not kept from the previous draft.
        (await facilitator.PutAsync(Url(step), StepSheets.Purpose("Commander"), StepSheets.VersionOf(first))).Ok();
        var read = (await facilitator.GetAsync(Url(step))).Ok();
        var content = read.GetProperty("content");
        Assert.Equal(("draft", "Commander"), (read.GetProperty("status").GetString(), StepSheets.PurposeOf(read)));
        Assert.Equal((JsonValueKind.Null, 0), (content.GetProperty("tools").ValueKind, content.GetProperty("procedure").GetArrayLength()));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_stale_if_match_on_the_sheet_is_a_conflict_and_the_draft_stays_as_it_was(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        var step = await StepSheets.StepAsync(project, 0);
        using var facilitator = project.Facilitator;
        var read = StepSheets.VersionOf((await facilitator.PutAsync(Url(step), StepSheets.Purpose("Premier"), await StepSheets.ReadVersionAsync(facilitator, step))).Ok());
        var unquoted = read.ToString(CultureInfo.InvariantCulture);
        var second = (await facilitator.SendAsync(HttpMethod.Put, Url(step), StepSheets.Purpose("Second"), null, ("If-Match", unquoted))).Ok();
        Assert.NotEqual(read, StepSheets.VersionOf(second));
        var details = (await facilitator.PutAsync(Url(step), StepSheets.Purpose("Troisième"), read)).Refused(409, "CONFLIT_VERSION");
        Assert.Equal(("anne", "à l'instant"), (details.GetProperty("by").GetString(), details.GetProperty("when").GetString()));
        (await facilitator.PostAsync(Url(step, "/validate"), null, read)).Refused(409, "CONFLIT_VERSION");
        (await facilitator.SendAsync(HttpMethod.Put, Url(step), StepSheets.Purpose("Quatrième"), null, ("If-Match", "\"v2\""))).Refused(400, "VERSION_INVALIDE");
        var after = (await facilitator.GetAsync(Url(step))).Ok();
        Assert.Equal(("Second", "draft"), (StepSheets.PurposeOf(after), after.GetProperty("status").GetString()));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_contributor_fills_and_validates_only_the_sheets_of_the_lane_he_holds(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        var step = await StepSheets.StepAsync(project, 0);
        using var contributor = project.Contributor;
        using var reader = project.Reader;
        using var stranger = project.Stranger;
        using var facilitator = project.Facilitator;
        (await contributor.PutAsync(Url(step), StepSheets.Purpose("Commander"))).Refused(403, "ACCES_REFUSE");
        (await contributor.PostAsync(Url(step, "/validate"))).Refused(403, "ACCES_REFUSE");
        (await reader.PutAsync(Url(step), StepSheets.Purpose("Commander"))).Refused(403, "ACCES_REFUSE");
        (await stranger.PutAsync(Url(step), StepSheets.Purpose("Commander"))).Refused(404, "INTROUVABLE");
        (await stranger.PostAsync(Url(step, "/validate"))).Refused(404, "INTROUVABLE");
        Assert.Equal("empty", (await facilitator.GetAsync(Url(step))).Ok().GetProperty("status").GetString());

        await StepSheets.MakeContributorActorAsync(project);
        var saved = (await contributor.PutAsync(Url(step), StepSheets.Purpose("Commander"), await StepSheets.ReadVersionAsync(contributor, step))).Ok();
        Assert.Equal((true, true, false, false), Rights(saved));
        var responsible = saved.GetProperty("responsible");
        Assert.Equal((await StepSheets.ContributorIdAsync(project), "lane"), (responsible.GetProperty("user_id").GetGuid(), responsible.GetProperty("origin").GetString()));
        var validated = (await contributor.PostAsync(Url(step, "/validate"), null, StepSheets.VersionOf(saved))).Ok();
        Assert.Equal("validated", validated.GetProperty("status").GetString());
        Assert.Equal("Commander", StepSheets.PurposeOf((await reader.GetAsync(Url(step))).Ok()));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_body_of_the_sheet_is_refused_on_the_key_of_its_wrong_field_and_an_empty_sheet_is_not_validated(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        var step = await StepSheets.StepAsync(project, 0);
        using var facilitator = project.Facilitator;
        static string? Field(JsonElement details) => details.GetProperty("field").GetString();
        var read = await StepSheets.ReadVersionAsync(facilitator, step);
        Assert.Equal("color", Field((await facilitator.PutAsync(Url(step), new { fields = new { color = new { value = "bleu" } } }, read)).Refused(400, "VALEUR_INVALIDE")));
        Assert.Equal("frequency", Field((await facilitator.PutAsync(Url(step), new { fields = new { frequency = new { value = "hourly" } } }, read)).Refused(400, "VALEUR_INVALIDE")));
        Assert.Equal("duration_minutes", Field((await facilitator.PutAsync(Url(step), new { fields = new { duration_minutes = new { value = "long" } } }, read)).Refused(400, "VALEUR_INVALIDE")));
        Assert.Equal("procedure", Field((await facilitator.PutAsync(Url(step), new { procedure = new[] { new { text = "Saisir", nature = "dance" } } }, read)).Refused(400, "VALEUR_INVALIDE")));
        (await facilitator.PostAsync(Url(step, "/validate"), null, read)).Refused(400, "FICHE_VIDE");
        Assert.Equal("empty", (await facilitator.GetAsync(Url(step))).Ok().GetProperty("status").GetString());
        var start = await StepSheets.StepAsync(project, 1, "Début", "start");
        (await facilitator.PutAsync(Url(start), StepSheets.Purpose("Commencer"), await StepSheets.ReadVersionAsync(facilitator, start))).Refused(400, "ETAPE_SANS_CONTENU");
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_facilitator_alone_asks_for_a_clarification_on_a_validated_sheet_which_goes_to_needs_review(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        var step = await StepSheets.StepAsync(project, 0);
        await StepSheets.MakeContributorActorAsync(project);
        using var facilitator = project.Facilitator;
        using var contributor = project.Contributor;
        using var reader = project.Reader;
        var url = Url(step, "/clarification");
        var draft = (await facilitator.PutAsync(Url(step), StepSheets.Purpose("Commander"), await StepSheets.ReadVersionAsync(facilitator, step))).Ok();
        (await facilitator.PostAsync(url, new { text = "Quel outil ?" }, StepSheets.VersionOf(draft))).Refused(400, "FICHE_ETAT_INVALIDE");
        var validated = (await facilitator.PostAsync(Url(step, "/validate"), null, StepSheets.VersionOf(draft))).Ok();
        (await contributor.PostAsync(url, new { text = "Quel outil ?" })).Refused(403, "ACCES_REFUSE");
        Assert.Equal("text", (await facilitator.PostAsync(url, new { }, StepSheets.VersionOf(validated))).Refused(400, "CHAMP_OBLIGATOIRE").GetProperty("field").GetString());
        (await facilitator.PostAsync(url, new { text = "Quel outil ?" }, StepSheets.VersionOf(draft))).Refused(409, "CONFLIT_VERSION");
        var asked = (await facilitator.PostAsync(url, new { text = "Quel outil ?" }, StepSheets.VersionOf(validated))).Ok();
        Assert.Equal(("needs_review", "Quel outil ?"), (asked.GetProperty("status").GetString(), asked.GetProperty("clarification_request").GetString()));
        var seen = (await reader.GetAsync(Url(step))).Ok();
        Assert.Equal(("needs_review", "Quel outil ?"), (seen.GetProperty("status").GetString(), seen.GetProperty("clarification_request").GetString()));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_facilitator_alone_changes_then_withdraws_the_question_and_the_sheet_is_validated_again(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        var step = await StepSheets.StepAsync(project, 0);
        await StepSheets.MakeContributorActorAsync(project);
        using var facilitator = project.Facilitator;
        using var contributor = project.Contributor;
        using var reader = project.Reader;
        var url = Url(step, "/clarification");
        var draft = (await facilitator.PutAsync(Url(step), StepSheets.Purpose("Commander"), await StepSheets.ReadVersionAsync(facilitator, step))).Ok();
        var validated = (await facilitator.PostAsync(Url(step, "/validate"), null, StepSheets.VersionOf(draft))).Ok();
        (await facilitator.PutAsync(url, new { text = "Quel outil ?" }, StepSheets.VersionOf(validated))).Refused(400, "FICHE_ETAT_INVALIDE");
        (await facilitator.DeleteAsync(url, StepSheets.VersionOf(validated))).Refused(400, "FICHE_ETAT_INVALIDE");
        var asked = (await facilitator.PostAsync(url, new { text = "Quel outil ?" }, StepSheets.VersionOf(validated))).Ok();

        (await contributor.PutAsync(url, new { text = "Quel logiciel ?" }, StepSheets.VersionOf(asked))).Refused(403, "ACCES_REFUSE");
        (await reader.DeleteAsync(url, StepSheets.VersionOf(asked))).Refused(403, "ACCES_REFUSE");
        Assert.Equal("text", (await facilitator.PutAsync(url, new { }, StepSheets.VersionOf(asked))).Refused(400, "CHAMP_OBLIGATOIRE").GetProperty("field").GetString());
        (await facilitator.PutAsync(url, new { text = "Quel logiciel ?" }, StepSheets.VersionOf(validated))).Refused(409, "CONFLIT_VERSION");
        var changed = (await facilitator.PutAsync(url, new { text = "Quel logiciel ?" }, StepSheets.VersionOf(asked))).Ok();
        Assert.Equal(("needs_review", "Quel logiciel ?"), (changed.GetProperty("status").GetString(), changed.GetProperty("clarification_request").GetString()));
        Assert.Equal("Quel logiciel ?", (await reader.GetAsync(Url(step))).Ok().GetProperty("clarification_request").GetString());

        (await facilitator.DeleteAsync(url, StepSheets.VersionOf(asked))).Refused(409, "CONFLIT_VERSION");
        var withdrawn = (await facilitator.DeleteAsync(url, StepSheets.VersionOf(changed))).Ok();
        Assert.Equal("validated", withdrawn.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, withdrawn.GetProperty("clarification_request").ValueKind);
        var seen = (await reader.GetAsync(Url(step))).Ok();
        Assert.Equal(("validated", JsonValueKind.Null), (seen.GetProperty("status").GetString(), seen.GetProperty("clarification_request").ValueKind));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_sheet_of_a_deleted_step_is_not_found(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var lane = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/lanes", new { name = "Achats" })).Ok().GetProperty("id").GetGuid();
        var step = await StepSheets.StepAsync(project, 0, laneId: lane);
        var saved = (await facilitator.PutAsync(Url(step), StepSheets.Purpose("Commander"), await StepSheets.ReadVersionAsync(facilitator, step))).Ok();
        (await facilitator.DeleteAsync($"/api/lanes/{lane}?steps=delete", await project.LaneVersionAsync(lane))).Ok();
        (await facilitator.GetAsync(Url(step))).Refused(404, "INTROUVABLE");
        (await facilitator.PutAsync(Url(step), StepSheets.Purpose("Commander"), StepSheets.VersionOf(saved))).Refused(404, "INTROUVABLE");
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_client_that_read_no_sheet_sends_version_zero_which_is_a_conflict_once_the_sheet_is_saved(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        var step = await StepSheets.StepAsync(project, 0);
        using var facilitator = project.Facilitator;
        var read = await StepSheets.ReadVersionAsync(facilitator, step);
        Assert.Equal(StepSheets.NoRecord, read);
        var saved = (await facilitator.PutAsync(Url(step), StepSheets.Purpose("Premier"), read)).Ok();
        Assert.Equal(("draft", "Premier"), (saved.GetProperty("status").GetString(), StepSheets.PurposeOf(saved)));
        Assert.NotEqual(StepSheets.NoRecord, StepSheets.VersionOf(saved));
        (await facilitator.PutAsync(Url(step), StepSheets.Purpose("Second"), StepSheets.NoRecord)).Refused(409, "CONFLIT_VERSION");
        (await facilitator.PostAsync(Url(step, "/validate"), null, StepSheets.NoRecord)).Refused(409, "CONFLIT_VERSION");
        var after = (await facilitator.GetAsync(Url(step))).Ok();
        Assert.Equal(("Premier", "draft", StepSheets.VersionOf(saved)), (StepSheets.PurposeOf(after), after.GetProperty("status").GetString(), StepSheets.VersionOf(after)));
    }
}
