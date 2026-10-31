// DocumentRoutesTests.cs
// The routes of the documents of a step and of their uses through the whole host on both databases (annex A 1, A 2.11, A 2.16).

using System.Text.Json;
using Topogenia.Host.Tests.Api;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Host.Tests.Sheets;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Documents;

public sealed class DocumentRoutesTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private async Task<ApiProject> ProjectAsync(EngineKind kind) => await ApiProject.CreateAsync(await hosts.OfAsync(kind));

    private static string Documents(Guid stepId, Guid? documentId = null) => $"/api/steps/{stepId}/documents{(documentId is { } id ? $"/{id}" : "")}";

    private static string Uses(Guid stepId, Guid documentId) => $"/api/steps/{stepId}/uses/{documentId}";

    private static object Output(string name) => new { direction = "output", name };

    /// <summary>The documents of the outputs of a sheet.</summary>
    private static List<JsonElement> OutputsOf(JsonElement sheet) =>
        sheet.GetProperty("outputs").EnumerateArray().Select(output => output.GetProperty("document")).ToList();

    /// <summary>
    /// The `If-Match` a client sends to tick a document after reading the sheet of the step: the version of the use of the
    /// document, or 0 when the step does not use it yet (no use read, ADR 045).
    /// </summary>
    private static async Task<int> UseReadAsync(ApiClient client, Guid stepId, Guid documentId)
    {
        var inputs = (await client.GetAsync($"/api/steps/{stepId}/sheet")).Ok().GetProperty("inputs").EnumerateArray();
        var use = inputs.Where(input => input.GetProperty("document").GetProperty("id").GetGuid() == documentId)
            .Select(input => input.GetProperty("use")).SingleOrDefault(use => use.ValueKind == JsonValueKind.Object);
        return use.ValueKind == JsonValueKind.Object ? use.GetProperty("version").GetInt32() : StepSheets.NoRecord;
    }

    /// <summary>A step of the root map with one output, and the identifier of the output.</summary>
    private static async Task<(Guid Step, Guid Document)> ProducerAsync(ApiProject project, int column, string name = "Bon de commande")
    {
        var step = await StepSheets.StepAsync(project, column, "Commander");
        using var facilitator = project.Facilitator;
        var sheet = (await facilitator.PostAsync(Documents(step), Output(name))).Ok();
        return (step, OutputsOf(sheet).Single().GetProperty("id").GetGuid());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task An_output_and_an_external_input_added_are_answered_as_the_sheet_and_seen_by_every_member(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        var step = await StepSheets.StepAsync(project, 0);
        using var facilitator = project.Facilitator;
        using var reader = project.Reader;
        var withOutput = (await facilitator.PostAsync(Documents(step), new
        {
            direction = "output", name = "Bon de commande", location = "https://intranet.example/bons", share_confirmed = true,
        })).Ok();
        var output = OutputsOf(withOutput).Single();
        Assert.Equal(("Bon de commande", "url", true, 1), (output.GetProperty("name").GetString(), output.GetProperty("location_type").GetString(),
            output.GetProperty("share_confirmed").GetBoolean(), output.GetProperty("version").GetInt32()));
        var withInput = (await facilitator.PostAsync(Documents(step), new { direction = "input", name = "Facture", location = @"\\serveur\factures", wait_hours = 4 })).Ok();
        var input = withInput.GetProperty("inputs").EnumerateArray().Single().GetProperty("document");
        Assert.Equal(("input", "path", 4m), (input.GetProperty("direction").GetString(), input.GetProperty("location_type").GetString(), input.GetProperty("wait_hours").GetDecimal()));
        var seen = (await reader.GetAsync($"/api/steps/{step}/sheet")).Ok();
        Assert.Equal(["Bon de commande"], OutputsOf(seen).Select(document => document.GetProperty("name").GetString()));
        Assert.Single(seen.GetProperty("inputs").EnumerateArray());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_body_of_a_document_is_refused_on_the_name_of_its_wrong_field(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        var step = await StepSheets.StepAsync(project, 0);
        using var facilitator = project.Facilitator;
        static string? Field(JsonElement details) => details.GetProperty("field").GetString();
        Assert.Equal("direction", Field((await facilitator.PostAsync(Documents(step), new { direction = "sideways", name = "Bon" })).Refused(400, "VALEUR_INVALIDE")));
        Assert.Equal("name", Field((await facilitator.PostAsync(Documents(step), new { direction = "output" })).Refused(400, "CHAMP_OBLIGATOIRE")));
        Assert.Equal("wait_hours", Field((await facilitator.PostAsync(Documents(step), new { direction = "output", name = "Bon", wait_hours = 2 })).Refused(400, "VALEUR_INVALIDE")));
        (await facilitator.PostAsync(Documents(step), new { direction = "output", name = "Bon", location = "ftp://serveur/bons" })).Refused(400, "ADRESSE_INVALIDE");
        Assert.Empty(OutputsOf((await facilitator.GetAsync($"/api/steps/{step}/sheet")).Ok()));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_document_is_updated_with_its_version_belongs_to_its_step_and_a_stale_version_is_a_conflict(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        var (step, document) = await ProducerAsync(project, 0);
        var other = await StepSheets.StepAsync(project, 1, "Livrer");
        using var facilitator = project.Facilitator;
        var read = OutputsOf((await facilitator.GetAsync($"/api/steps/{step}/sheet")).Ok()).Single().GetProperty("version").GetInt32();
        var updated = (await facilitator.PutAsync(Documents(step, document), new { name = "Commande signée", location = @"P:\commandes" }, read)).Ok();
        var renamed = OutputsOf(updated).Single();
        Assert.Equal(("Commande signée", "output", "path"), (renamed.GetProperty("name").GetString(), renamed.GetProperty("direction").GetString(), renamed.GetProperty("location_type").GetString()));
        var details = (await facilitator.PutAsync(Documents(step, document), new { name = "Commande perdue" }, read)).Refused(409, "CONFLIT_VERSION");
        Assert.Equal("anne", details.GetProperty("by").GetString());
        (await facilitator.PutAsync(Documents(other, document), new { name = "Commande perdue" }, renamed.GetProperty("version").GetInt32())).Refused(404, "INTROUVABLE");
        Assert.Equal("Commande signée", OutputsOf((await facilitator.GetAsync($"/api/steps/{step}/sheet")).Ok()).Single().GetProperty("name").GetString());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_document_is_deleted_with_its_uses_and_a_document_absent_from_the_step_is_not_found(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        var (producer, document) = await ProducerAsync(project, 0);
        var consumer = await StepSheets.StepAsync(project, 1, "Livrer");
        using var facilitator = project.Facilitator;
        (await facilitator.PutAsync(Uses(consumer, document), new { wait_hours = 2 }, await UseReadAsync(facilitator, consumer, document))).Ok();
        var version = await project.DocumentVersionAsync(producer, document);
        (await facilitator.DeleteAsync(Documents(consumer, document), version)).Refused(404, "INTROUVABLE");
        var deleted = (await facilitator.DeleteAsync(Documents(producer, document), version)).Ok();
        Assert.Empty(OutputsOf(deleted));
        Assert.Empty((await facilitator.GetAsync($"/api/steps/{consumer}/sheet")).Ok().GetProperty("inputs").EnumerateArray());
        (await facilitator.DeleteAsync(Documents(producer, document), version)).Refused(404, "INTROUVABLE");
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Documents_and_uses_are_written_by_the_one_who_may_fill_only(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        var (producer, document) = await ProducerAsync(project, 0);
        var consumer = await StepSheets.StepAsync(project, 1, "Livrer");
        using var contributor = project.Contributor;
        using var reader = project.Reader;
        using var stranger = project.Stranger;
        (await contributor.PostAsync(Documents(producer), Output("Devis"))).Refused(403, "ACCES_REFUSE");
        (await contributor.DeleteAsync(Documents(producer, document))).Refused(403, "ACCES_REFUSE");
        (await reader.PutAsync(Documents(producer, document), new { name = "Devis" })).Refused(403, "ACCES_REFUSE");
        (await reader.PutAsync(Uses(consumer, document), new { wait_hours = 1 })).Refused(403, "ACCES_REFUSE");
        (await contributor.DeleteAsync(Uses(consumer, document))).Refused(403, "ACCES_REFUSE");
        (await stranger.PostAsync(Documents(producer), Output("Devis"))).Refused(404, "INTROUVABLE");
        (await stranger.PutAsync(Uses(consumer, document), new { wait_hours = 1 })).Refused(404, "INTROUVABLE");

        await StepSheets.MakeContributorActorAsync(project);
        Assert.Equal(2, OutputsOf((await contributor.PostAsync(Documents(producer), Output("Devis"))).Ok()).Count);
        Assert.True((await contributor.PutAsync(Uses(consumer, document), new { wait_hours = 1 }, await UseReadAsync(contributor, consumer, document))).Ok().GetProperty("inputs")[0].GetProperty("used").GetBoolean());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_use_ticks_the_output_of_another_step_with_its_pain_point_and_unticking_is_idempotent(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        var (producer, document) = await ProducerAsync(project, 0);
        var consumer = await StepSheets.StepAsync(project, 1, "Livrer");
        using var facilitator = project.Facilitator;
        var ticked = (await facilitator.PutAsync(Uses(consumer, document), new
        {
            wait_hours = 2, percent_complete_accurate = 80, pain_point = "Toujours en retard", lost_hours_per_month = 3.5m,
        }, await UseReadAsync(facilitator, consumer, document))).Ok();
        var input = ticked.GetProperty("inputs").EnumerateArray().Single();
        Assert.Equal((document, producer, true), (input.GetProperty("document").GetProperty("id").GetGuid(), input.GetProperty("producer").GetProperty("id").GetGuid(),
            input.GetProperty("used").GetBoolean()));
        Assert.Equal((2m, 80), (input.GetProperty("use").GetProperty("wait_hours").GetDecimal(), input.GetProperty("use").GetProperty("percent_complete_accurate").GetInt32()));
        var painPoint = ticked.GetProperty("pain_points").EnumerateArray().Single();
        Assert.Equal(("Toujours en retard", 3.5m, producer), (painPoint.GetProperty("description").GetString(), painPoint.GetProperty("lost_hours_per_month").GetDecimal(),
            painPoint.GetProperty("step_id").GetGuid()));
        Assert.Equal(painPoint.GetProperty("id").GetGuid(), input.GetProperty("use").GetProperty("pain_point_id").GetGuid());

        var unticked = (await facilitator.DeleteAsync(Uses(consumer, document), await UseReadAsync(facilitator, consumer, document))).Ok();
        Assert.Equal((0, 0), (unticked.GetProperty("inputs").GetArrayLength(), unticked.GetProperty("pain_points").GetArrayLength()));
        var again = (await facilitator.DeleteAsync(Uses(consumer, document), await UseReadAsync(facilitator, consumer, document))).Ok();
        Assert.Equal(0, again.GetProperty("inputs").GetArrayLength());
        Assert.Empty((await facilitator.GetAsync($"/api/steps/{producer}/sheet")).Ok().GetProperty("outputs")[0].GetProperty("used_by").EnumerateArray());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_use_checks_the_version_read_its_values_and_a_producer_that_is_another_step(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        var (producer, document) = await ProducerAsync(project, 0);
        var consumer = await StepSheets.StepAsync(project, 1, "Livrer");
        using var facilitator = project.Facilitator;
        static int UseVersion(JsonElement sheet) => sheet.GetProperty("inputs")[0].GetProperty("use").GetProperty("version").GetInt32();
        var read = UseVersion((await facilitator.PutAsync(Uses(consumer, document), new { wait_hours = 2 }, await UseReadAsync(facilitator, consumer, document))).Ok());
        var changed = (await facilitator.PutAsync(Uses(consumer, document), new { wait_hours = 5 }, read)).Ok();
        Assert.Equal(5m, changed.GetProperty("inputs")[0].GetProperty("use").GetProperty("wait_hours").GetDecimal());
        (await facilitator.PutAsync(Uses(consumer, document), new { wait_hours = 9 }, read)).Refused(409, "CONFLIT_VERSION");
        static string? Field(JsonElement details) => details.GetProperty("field").GetString();
        var current = UseVersion(changed);
        Assert.Equal("percent_complete_accurate", Field((await facilitator.PutAsync(Uses(consumer, document), new { percent_complete_accurate = 150 }, current)).Refused(400, "VALEUR_INVALIDE")));
        Assert.Equal("lost_hours_per_month", Field((await facilitator.PutAsync(Uses(consumer, document), new { pain_point = "Lent", lost_hours_per_month = -1 }, current)).Refused(400, "VALEUR_INVALIDE")));
        (await facilitator.PutAsync(Uses(producer, document), new { wait_hours = 1 }, await UseReadAsync(facilitator, producer, document))).Refused(404, "INTROUVABLE");
        var unknown = Guid.NewGuid();
        (await facilitator.PutAsync(Uses(consumer, unknown), new { wait_hours = 1 }, await UseReadAsync(facilitator, consumer, unknown))).Refused(404, "INTROUVABLE");
        Assert.Equal(5m, (await facilitator.GetAsync($"/api/steps/{consumer}/sheet")).Ok().GetProperty("inputs")[0].GetProperty("use").GetProperty("wait_hours").GetDecimal());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_other_window_lists_the_outputs_of_the_other_steps_with_the_marker_of_a_used_one(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        var (producer, document) = await ProducerAsync(project, 0);
        var consumer = await StepSheets.StepAsync(project, 1, "Livrer");
        using var facilitator = project.Facilitator;
        using var reader = project.Reader;
        using var stranger = project.Stranger;
        var url = $"/api/steps/{consumer}/documents/available";
        var group = (await reader.GetAsync(url)).Ok().EnumerateArray().Single();
        var offered = group.GetProperty("documents").EnumerateArray().Single();
        Assert.Equal((producer, document, false), (group.GetProperty("producer").GetProperty("id").GetGuid(), offered.GetProperty("document").GetProperty("id").GetGuid(),
            offered.GetProperty("used").GetBoolean()));
        (await facilitator.PutAsync(Uses(consumer, document), new { wait_hours = 1 }, await UseReadAsync(facilitator, consumer, document))).Ok();
        Assert.True((await reader.GetAsync(url)).Ok()[0].GetProperty("documents")[0].GetProperty("used").GetBoolean());
        Assert.Empty((await reader.GetAsync($"/api/steps/{producer}/documents/available")).Ok().EnumerateArray());
        (await stranger.GetAsync(url)).Refused(404, "INTROUVABLE");
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_client_that_read_no_use_sends_version_zero_which_is_a_conflict_once_the_document_is_used(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        var (_, document) = await ProducerAsync(project, 0);
        var consumer = await StepSheets.StepAsync(project, 1, "Livrer");
        using var facilitator = project.Facilitator;
        var read = await UseReadAsync(facilitator, consumer, document);
        Assert.Equal(StepSheets.NoRecord, read);
        var use = (await facilitator.PutAsync(Uses(consumer, document), new { wait_hours = 2 }, read)).Ok().GetProperty("inputs")[0].GetProperty("use");
        var version = use.GetProperty("version").GetInt32();
        Assert.Equal(2m, use.GetProperty("wait_hours").GetDecimal());
        Assert.NotEqual(StepSheets.NoRecord, version);
        (await facilitator.PutAsync(Uses(consumer, document), new { wait_hours = 7 }, StepSheets.NoRecord)).Refused(409, "CONFLIT_VERSION");
        var after = (await facilitator.GetAsync($"/api/steps/{consumer}/sheet")).Ok().GetProperty("inputs")[0].GetProperty("use");
        Assert.Equal((2m, version), (after.GetProperty("wait_hours").GetDecimal(), after.GetProperty("version").GetInt32()));
    }
}
