// DocumentFlowsPortedTests.cs
// The 1.x integration tests of the inputs and outputs of the sheets (T-DOCF) that the document routes tests did not prove, through the whole host on both databases.

using System.Text.Json;
using Topogenia.Host.Tests.Api;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Host.Tests.Sheets;
using Topogenia.Host.Tests.Steps;
using Topogenia.Persistence.Tests.Infrastructure;
using static Topogenia.Host.Tests.Ported.SheetPortingCalls;

namespace Topogenia.Host.Tests.Ported;

/// <summary>
/// Each test carries the intention of one 1.x test of `DocumentsFluxTests` (T-DOCF-nn of `docs/portage-tests.md`) on the
/// map of the 1.x: Extraction (lane A, column 0) then Tableau (A, 1) then Envoyer (A, 2), and Objectifs (lane B, column 0)
/// into Tableau.
/// </summary>
public sealed class DocumentFlowsPortedTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private sealed record FlowPlan(ApiProject Project, Guid Extraction, Guid Tableau, Guid Envoyer, Guid Objectifs, Guid Link);

    private async Task<FlowPlan> PlanAsync(EngineKind kind)
    {
        var project = await ApiProject.CreateAsync(await hosts.OfAsync(kind));
        var extraction = (await project.AddStepAsync(0, "Extraction")).Id;
        var tableau = (await project.AddStepAsync(1, "Tableau")).Id;
        var envoyer = (await project.AddStepAsync(2, "Envoyer")).Id;
        var other = await project.AddLaneAsync("Direction commerciale");
        var objectifs = (await project.AddStepAsync(0, "Objectifs", other)).Id;
        var link = await project.AddFlowAsync(extraction, tableau);
        await project.AddFlowAsync(tableau, envoyer);
        await project.AddFlowAsync(objectifs, tableau);
        return new FlowPlan(project, extraction, tableau, envoyer, objectifs, link);
    }

    /// <summary>
    /// T-DOCF-01: the outputs of the predecessors of every lane are offered unticked; ticking creates the use with its wait, counts in the
    /// completeness, shows on the producer and on the flow; unticking takes it back.
    /// </summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_outputs_of_the_predecessors_of_every_lane_are_offered_unticked_and_a_tick_shows_on_the_producer_and_the_flow(EngineKind kind)
    {
        var plan = await PlanAsync(kind);
        using var facilitator = plan.Project.Facilitator;
        var extraction = await OutputAsync(facilitator, plan.Extraction, "Extraction SAP");
        Assert.Empty(UsersOfSingleOutput(await ReadAsync(facilitator, plan.Extraction)));
        await OutputAsync(facilitator, plan.Objectifs, "Objectifs des ventes");

        var offered = await ReadAsync(facilitator, plan.Tableau);
        Assert.Equal([("Extraction SAP", true, false), ("Objectifs des ventes", true, false)], InputsOf(offered));
        Assert.Equal(0, CompletenessOf(offered));
        Assert.Empty(await FlowDocumentsAsync(plan.Project, plan.Link));

        var ticked = (await UseAsync(facilitator, plan.Tableau, extraction, new { wait_hours = 48 })).Ok();
        var input = InputOf(ticked, extraction);
        Assert.Equal((true, 48m, plan.Extraction), (input.GetProperty("used").GetBoolean(), input.GetProperty("use").GetProperty("wait_hours").GetDecimal(),
            input.GetProperty("producer").GetProperty("id").GetGuid()));
        Assert.Equal(14, CompletenessOf(ticked));
        Assert.Equal([plan.Tableau], UsersOfSingleOutput(await ReadAsync(facilitator, plan.Extraction)));
        Assert.Equal(["Extraction SAP"], await FlowDocumentsAsync(plan.Project, plan.Link));

        var unticked = (await facilitator.DeleteAsync(UseUrl(plan.Tableau, extraction), await plan.Project.UseVersionAsync(plan.Tableau, extraction))).Ok();
        Assert.Equal((false, 0), (InputOf(unticked, extraction).GetProperty("used").GetBoolean(), CompletenessOf(unticked)));
        Assert.Empty(await FlowDocumentsAsync(plan.Project, plan.Link));
    }

    /// <summary>T-DOCF-02: an output declared before the flow is offered as soon as the flow exists.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task An_output_declared_before_its_flow_is_offered_as_soon_as_the_flow_exists(EngineKind kind)
    {
        var project = await ApiProject.CreateAsync(await hosts.OfAsync(kind));
        var step = (await project.AddStepAsync(0, "Tableau croisé")).Id;
        var next = (await project.AddStepAsync(1, "Suivante")).Id;
        using var facilitator = project.Facilitator;
        await OutputAsync(facilitator, step, "Objectifs des ventes");
        Assert.Empty(InputsOf(await ReadAsync(facilitator, next)));
        await project.AddFlowAsync(step, next);
        Assert.Equal([("Objectifs des ventes", true, false)], InputsOf(await ReadAsync(facilitator, next)));
    }

    /// <summary>
    /// T-DOCF-03: "Other" lists the outputs of the steps that are not predecessors; ticked, they stay marked from elsewhere, also after the
    /// deletion of the flow, and are near again when the undo brings the flow back.
    /// </summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task An_output_from_elsewhere_ticked_stays_marked_remote_also_after_its_flow_is_deleted_and_is_near_again_after_the_undo(EngineKind kind)
    {
        var plan = await PlanAsync(kind);
        using var facilitator = plan.Project.Facilitator;
        var extraction = await OutputAsync(facilitator, plan.Extraction, "Extraction SAP");
        await OutputAsync(facilitator, plan.Tableau, "Tableau croisé");

        var other = $"/api/steps/{plan.Envoyer}/documents/available";
        var group = (await facilitator.GetAsync(other)).Ok().EnumerateArray().Single();
        var available = group.GetProperty("documents").EnumerateArray().Single();
        Assert.Equal((plan.Extraction, "Extraction SAP", false), (group.GetProperty("producer").GetProperty("id").GetGuid(),
            available.GetProperty("document").GetProperty("name").GetString(), available.GetProperty("used").GetBoolean()));

        var sheet = (await UseAsync(facilitator, plan.Envoyer, extraction, new { })).Ok();
        Assert.Equal([("Tableau croisé", true, false), ("Extraction SAP", false, true)], InputsOf(sheet));
        Assert.True((await facilitator.GetAsync(other)).Ok()[0].GetProperty("documents")[0].GetProperty("used").GetBoolean());

        (await UseAsync(facilitator, plan.Tableau, extraction, new { })).Ok();
        (await facilitator.DeleteAsync($"/api/flows/{plan.Link}", await plan.Project.FlowVersionAsync(plan.Link))).Ok();
        Assert.Equal([("Extraction SAP", false, true)], InputsOf(await ReadAsync(facilitator, plan.Tableau)));
        (await facilitator.PostAsync($"/api/maps/{plan.Project.RootMapId}/undo")).Ok();
        Assert.Equal([("Extraction SAP", true, true)], InputsOf(await ReadAsync(facilitator, plan.Tableau)));
    }

    /// <summary>T-DOCF-04 (external input): an external input carries its wait, counts as used, is offered to no other step; an output takes no wait.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task An_external_input_carries_its_wait_is_used_by_its_step_only_and_an_output_takes_no_wait(EngineKind kind)
    {
        var plan = await PlanAsync(kind);
        using var facilitator = plan.Project.Facilitator;
        var sheet = (await facilitator.PostAsync($"/api/steps/{plan.Extraction}/documents", new { direction = "input", name = "ERP SAP", wait_hours = 2 })).Ok();
        var external = sheet.GetProperty("inputs").EnumerateArray().Single();
        Assert.Equal((JsonValueKind.Null, true, 2m), (external.GetProperty("producer").ValueKind, external.GetProperty("used").GetBoolean(),
            external.GetProperty("document").GetProperty("wait_hours").GetDecimal()));
        Assert.Equal(14, CompletenessOf(sheet));
        Assert.DoesNotContain(InputsOf(await ReadAsync(facilitator, plan.Tableau)), input => input.Name == "ERP SAP");
        Assert.Equal("wait_hours", (await facilitator.PostAsync($"/api/steps/{plan.Extraction}/documents", new { direction = "output", name = "x", wait_hours = 1 }))
            .Refused(400, "VALEUR_INVALIDE").Field());
    }

    /// <summary>
    /// T-DOCF-05: the assigned contributor ticks in his sheet but produces nothing in another; the reader ticks nothing and sees who uses an
    /// output; ticking its own document is not found and a negative wait is refused.
    /// </summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_assigned_contributor_ticks_in_his_sheet_only_the_reader_reads_the_uses_and_a_negative_wait_is_refused(EngineKind kind)
    {
        var plan = await PlanAsync(kind);
        await AssignContributorAsync(plan.Project, plan.Tableau);
        using var facilitator = plan.Project.Facilitator;
        using var contributor = plan.Project.Contributor;
        using var reader = plan.Project.Reader;
        var extraction = await OutputAsync(facilitator, plan.Extraction, "Extraction SAP");

        (await contributor.PostAsync($"/api/steps/{plan.Extraction}/documents", new { direction = "output", name = "x" })).Refused(403, "ACCES_REFUSE");
        (await reader.PutAsync(UseUrl(plan.Tableau, extraction), new { })).Refused(403, "ACCES_REFUSE");
        Assert.True(InputOf((await UseAsync(contributor, plan.Tableau, extraction, new { })).Ok(), extraction).GetProperty("used").GetBoolean());
        Assert.Equal([plan.Tableau], UsersOfSingleOutput(await ReadAsync(reader, plan.Extraction)));

        (await UseAsync(facilitator, plan.Extraction, extraction, new { })).Refused(404, "INTROUVABLE");
        Assert.Equal("wait_hours", (await UseAsync(facilitator, plan.Tableau, extraction, new { wait_hours = -1 })).Refused(400, "VALEUR_INVALIDE").Field());
    }

    /// <summary>
    /// T-DOCF-06: the %C&amp;A and the pain point of a use belong to the producing step, are seen by the producer, updated without duplicate,
    /// erased while still reported when the description is emptied; bounds and HTML are checked.
    /// </summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_quality_and_the_pain_point_of_a_use_belong_to_the_producer_are_updated_without_duplicate_and_erased_while_reported(EngineKind kind)
    {
        var plan = await PlanAsync(kind);
        using var facilitator = plan.Project.Facilitator;
        var extraction = await OutputAsync(facilitator, plan.Extraction, "Extraction SAP");
        var sheet = (await UseAsync(facilitator, plan.Tableau, extraction, new
        {
            percent_complete_accurate = 0, pain_point = "Requête non mise à jour : nouveaux types de ventes, groupes clients périmés", lost_hours_per_month = 6,
        })).Ok();
        var use = InputOf(sheet, extraction).GetProperty("use");
        var painPoint = sheet.GetProperty("pain_points").EnumerateArray().Single();
        var (id, code) = (painPoint.GetProperty("id").GetGuid(), painPoint.GetProperty("code").GetString()!);
        Assert.Equal((0, id), (use.GetProperty("percent_complete_accurate").GetInt32(), use.GetProperty("pain_point_id").GetGuid()));
        Assert.Matches(@"^[A-Z]{3,6}-I001$", code);
        Assert.Equal(("reported", 6m, plan.Extraction), (painPoint.GetProperty("status").GetString(), painPoint.GetProperty("lost_hours_per_month").GetDecimal(),
            painPoint.GetProperty("step_id").GetGuid()));
        var producer = await ReadAsync(facilitator, plan.Extraction);
        var usedBy = producer.GetProperty("outputs")[0].GetProperty("used_by").EnumerateArray().Single();
        Assert.Equal((0, id), (usedBy.GetProperty("percent_complete_accurate").GetInt32(), usedBy.GetProperty("pain_point_id").GetGuid()));
        Assert.Equal([code], producer.GetProperty("pain_points").EnumerateArray().Select(point => point.GetProperty("code").GetString()));

        sheet = (await UseAsync(facilitator, plan.Tableau, extraction, new { percent_complete_accurate = 40, pain_point = "Groupes clients périmés" })).Ok();
        painPoint = sheet.GetProperty("pain_points").EnumerateArray().Single();
        Assert.Equal((40, id, code, "Groupes clients périmés"), (InputOf(sheet, extraction).GetProperty("use").GetProperty("percent_complete_accurate").GetInt32(),
            painPoint.GetProperty("id").GetGuid(), painPoint.GetProperty("code").GetString(), painPoint.GetProperty("description").GetString()));
        Assert.Equal([id], await PainPointMarkersAsync(plan.Project, plan.Extraction));

        sheet = (await UseAsync(facilitator, plan.Tableau, extraction, new { percent_complete_accurate = 100 })).Ok();
        Assert.Equal((JsonValueKind.Null, 0), (InputOf(sheet, extraction).GetProperty("use").GetProperty("pain_point_id").ValueKind, sheet.GetProperty("pain_points").GetArrayLength()));
        Assert.Empty(await PainPointMarkersAsync(plan.Project, plan.Extraction));

        Assert.Equal("percent_complete_accurate", (await UseAsync(facilitator, plan.Tableau, extraction, new { percent_complete_accurate = 101 })).Refused(400, "VALEUR_INVALIDE").Field());
        (await UseAsync(facilitator, plan.Tableau, extraction, new { pain_point = "<b>x</b>" })).Refused(400, "TEXTE_HTML_INTERDIT");
        Assert.Equal(100, InputOf(await ReadAsync(facilitator, plan.Tableau), extraction).GetProperty("use").GetProperty("percent_complete_accurate").GetInt32());
    }

    /// <summary>T-DOCF-07: a document deleted by its step leaves the steps that used it and the flow that carried it.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_document_deleted_by_its_step_leaves_the_steps_that_used_it_and_the_flow_that_carried_it(EngineKind kind)
    {
        var plan = await PlanAsync(kind);
        using var facilitator = plan.Project.Facilitator;
        var extraction = await OutputAsync(facilitator, plan.Extraction, "Extraction SAP");
        (await UseAsync(facilitator, plan.Tableau, extraction, new { })).Ok();
        Assert.Equal(["Extraction SAP"], await FlowDocumentsAsync(plan.Project, plan.Link));

        var deleted = (await facilitator.DeleteAsync($"/api/steps/{plan.Extraction}/documents/{extraction}",
            await plan.Project.DocumentVersionAsync(plan.Extraction, extraction))).Ok();
        Assert.Empty(deleted.GetProperty("outputs").EnumerateArray());
        Assert.Empty(InputsOf(await ReadAsync(facilitator, plan.Tableau)));
        Assert.Empty(await FlowDocumentsAsync(plan.Project, plan.Link));
    }

    /// <summary>T-DOCF-08: a use ticked by the neighbour step does not change the version of the sheet of the producer, which saves without conflict.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_use_ticked_by_the_neighbour_step_leaves_the_sheet_of_the_producer_saved_without_conflict(EngineKind kind)
    {
        var plan = await PlanAsync(kind);
        using var facilitator = plan.Project.Facilitator;
        using var admin = plan.Project.Admin;
        var extraction = await OutputAsync(facilitator, plan.Extraction, "Extraction SAP");
        var open = (await SaveAsync(facilitator, plan.Extraction, StepSheets.Purpose("Extraire"))).Ok();
        (await UseAsync(admin, plan.Tableau, extraction, new { })).Ok();
        var next = (await facilitator.PutAsync(SheetUrl(plan.Extraction), StepSheets.Purpose("Extraire les ventes"), StepSheets.VersionOf(open))).Ok();
        Assert.Equal(("Extraire les ventes", 29), (StepSheets.PurposeOf(next), CompletenessOf(next)));
    }

    /// <summary>T-DOCF-09: the address of a document is an http(s) URL or a file path; a dangerous or malformed address is refused.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_address_of_a_document_is_an_url_or_a_path_and_a_dangerous_or_malformed_address_is_refused(EngineKind kind)
    {
        var plan = await PlanAsync(kind);
        using var facilitator = plan.Project.Facilitator;
        (string Location, string Type)[] accepted =
        [
            ("https://exemple.sharepoint.com/sites/dx/Ventes.xlsx", "url"), (@"\\serveur\partage\Ventes.xlsx", "path"), (@"C:\Documents\Ventes.xlsx", "path"),
        ];
        foreach (var (location, type) in accepted)
        {
            var name = $"Ventes {type} {location.Length}";
            var sheet = (await facilitator.PostAsync($"/api/steps/{plan.Extraction}/documents", new { direction = "output", name, location })).Ok();
            var document = sheet.GetProperty("outputs").EnumerateArray().Select(output => output.GetProperty("document"))
                .Single(candidate => candidate.GetProperty("name").GetString() == name);
            Assert.Equal((location, type), (document.GetProperty("location").GetString(), document.GetProperty("location_type").GetString()));
        }
        foreach (var location in new[] { "javascript:alert(1)", "www.exemple.fr/ventes", "https://exemple.fr/un fichier.xlsx" })
            (await facilitator.PostAsync($"/api/steps/{plan.Extraction}/documents", new { direction = "output", name = "Ventes", location })).Refused(400, "ADRESSE_INVALIDE");
        Assert.Equal(3, (await ReadAsync(facilitator, plan.Extraction)).GetProperty("outputs").GetArrayLength());
    }

    /// <summary>The pain points still open that the map shows on a step.</summary>
    private static async Task<IReadOnlyList<Guid>> PainPointMarkersAsync(ApiProject project, Guid stepId) =>
        [.. StructureCalls.StepOf(await project.MapAsync(), stepId).GetProperty("markers").GetProperty("pain_points").EnumerateArray()
            .Select(marker => marker.GetProperty("pain_point_id").GetGuid())];
}
