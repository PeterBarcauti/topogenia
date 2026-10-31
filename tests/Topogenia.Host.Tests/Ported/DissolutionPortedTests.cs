// DissolutionPortedTests.cs
// The 1.x dissolution tests (RemonteeTests, T-REMO) ported to the whole host on both databases: frame, remainder, refusals.

using System.Text.Json;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Host.Tests.Steps;
using Topogenia.Persistence.Tests.Infrastructure;
using static Topogenia.Host.Tests.Ported.DissolutionPortedPlan;
using static Topogenia.Host.Tests.Selection.MapDrawing;

namespace Topogenia.Host.Tests.Ported;

public sealed class DissolutionPortedTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private async Task<DissolutionPortedPlan> PlanAsync(EngineKind kind, bool withRemainder = true) =>
        await DissolutionPortedPlan.CreateAsync(await ApiProject.CreateAsync(await hosts.OfAsync(kind)), withRemainder);

    private static IReadOnlyList<Guid> FrameSteps(JsonElement frame) =>
        [.. frame.GetProperty("frame_step_ids").EnumerateArray().Select(id => id.GetGuid())];

    private static IReadOnlyList<string?> PainPointTexts(JsonElement step) =>
        [.. step.GetProperty("markers").GetProperty("pain_points").EnumerateArray().Select(marker => marker.GetProperty("description").GetString())];

    /// <summary>T-REMO-01.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_detail_steps_take_the_place_of_the_detailed_step_with_its_flows_and_a_frame_opens_with_the_remainder(EngineKind kind)
    {
        var p = await PlanAsync(kind);
        using var facilitator = p.Project.Facilitator;
        var detail = await p.Project.MapAsync(p.Detail);
        var codes = (Step(detail, p.Preparer).GetProperty("code").GetString(), Step(detail, p.Verifier).GetProperty("code").GetString());
        var envoiCode = Step(await p.Project.MapAsync(), p.Envoi).GetProperty("code").GetString();

        var dissolved = await p.DissolveAsync();
        var map = await p.Project.MapAsync();

        // Extraction (0), Preparer (1), Verifier (2, in the lane « Logistique » created), Envoi shifted (3).
        Assert.Equal((0, 1, 2, 3), (Column(map, p.Extraction), Column(map, p.Preparer), Column(map, p.Verifier), Column(map, p.Envoi)));
        Assert.DoesNotContain(p.Saisie, StepIds(map));
        Assert.Equal(["Controlling", "Logistique"], LaneNames(map));
        Assert.Equal(p.Lane, Step(map, p.Preparer).GetProperty("lane_id").GetGuid());
        Assert.Equal(new HashSet<(Guid, Guid, string?)> { (p.Extraction, p.Preparer, "oui"), (p.Preparer, p.Verifier, null), (p.Verifier, p.Envoi, null) },
            LabelledFlows(map));
        // Codes kept; the start and the end went with the detail.
        Assert.Equal(codes, (Step(map, p.Preparer).GetProperty("code").GetString(), Step(map, p.Verifier).GetProperty("code").GetString()));
        Assert.Equal(4, StepIds(map).Count);
        (await facilitator.GetAsync($"/api/maps/{p.Detail}")).Refused(404, "INTROUVABLE");

        // The frame: its steps and the remainder of Saisie.
        var frame = map.GetProperty("dissolution");
        Assert.Equal((dissolved.GetProperty("id").GetGuid(), p.Saisie), (frame.GetProperty("id").GetGuid(), frame.GetProperty("step_id").GetGuid()));
        Assert.Equal([p.Preparer, p.Verifier], FrameSteps(frame));
        var document = Assert.Single(frame.GetProperty("documents").EnumerateArray());
        Assert.Equal((DocumentName, "output"), (document.GetProperty("name").GetString(), document.GetProperty("direction").GetString()));
        Assert.Equal([envoiCode], document.GetProperty("used_by").EnumerateArray().Select(code => code.GetString()));
        Assert.Equal(PainPointText, Assert.Single(frame.GetProperty("pain_points").EnumerateArray()).GetProperty("description").GetString());
        Assert.NotEqual(JsonValueKind.Null, frame.GetProperty("sheet_id").ValueKind);
    }

    /// <summary>
    /// T-REMO-01, spec 5.6 point 1: the sheet of the remainder, read with the map, is the draft for the facilitator and the
    /// latest validated version only for a reader, who never sees the text of the draft.
    /// </summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_sheet_of_the_remainder_is_seen_as_a_draft_by_the_facilitator_and_as_validated_only_by_a_reader(EngineKind kind)
    {
        const string Draft = "Saisir autrement";
        var p = await PlanAsync(kind);
        using var facilitator = p.Project.Facilitator;
        using var reader = p.Project.Reader;
        Assert.Equal(JsonValueKind.Null, (await p.Project.MapAsync()).GetProperty("dissolution_sheet").ValueKind);
        var validated = (await facilitator.PostAsync($"/api/steps/{p.Saisie}/sheet/validate", null, await SheetReadAsync(facilitator, p.Saisie))).Ok();
        (await facilitator.PutAsync($"/api/steps/{p.Saisie}/sheet", SheetBody(Draft), validated.GetProperty("version").GetInt32())).Ok();
        var dissolved = await p.DissolveAsync();

        foreach (var draft in new[] { dissolved.GetProperty("map").GetProperty("dissolution_sheet"), (await p.Project.MapAsync()).GetProperty("dissolution_sheet") })
        {
            Assert.True(draft.GetProperty("is_draft").GetBoolean());
            Assert.Equal((Draft, ProcedureLine), (draft.GetProperty("content").GetProperty("purpose").GetString(),
                draft.GetProperty("content").GetProperty("procedure")[0].GetProperty("text").GetString()));
            Assert.Equal(1, draft.GetProperty("validated").GetProperty("number").GetInt32());
        }

        var read = await reader.GetAsync($"/api/maps/{p.Project.RootMapId}");
        var published = read.Ok().GetProperty("map").GetProperty("dissolution_sheet");
        Assert.Equal((false, "validated"), (published.GetProperty("is_draft").GetBoolean(), published.GetProperty("status").GetString()));
        Assert.Equal((Purpose, ProcedureLine), (published.GetProperty("content").GetProperty("purpose").GetString(),
            published.GetProperty("content").GetProperty("procedure")[0].GetProperty("text").GetString()));
        Assert.Equal(1, published.GetProperty("validated").GetProperty("number").GetInt32());
        Assert.DoesNotContain(Draft, read.Body, StringComparison.Ordinal);
    }

    /// <summary>T-REMO-02; the return of an assignment (1.x `depuis`) is kept, and the server undo takes it back.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_frame_closes_when_the_remainder_is_assigned_the_uses_follow_and_bringing_back_reopens_it(EngineKind kind)
    {
        var p = await PlanAsync(kind);
        var frame = (await p.DissolveAsync()).GetProperty("id").GetGuid();
        var painPoint = (await p.FrameAsync())!.Value.GetProperty("pain_points")[0].GetProperty("id").GetGuid();
        var document = p.Document!.Value;
        // A step outside the map of the frame is refused: one of another map of the project.
        var other = await p.Project.AddStepAsync(5, "Ailleurs", p.Lane);
        var (subMap, subLane) = await p.Project.DetailAsync(other.Id);
        var outside = await p.Project.AddStepAsync(0, "Dedans", subLane, subMap);
        (await p.AssignAsync(frame, "document", document, outside.Id)).Refused(400, "ETAPE_HORS_CARTE");

        var left = (await p.AssignAsync(frame, "document", document, p.Verifier)).Ok().GetProperty("frame");
        Assert.Equal(0, left.GetProperty("documents").GetArrayLength());
        Assert.Equal(JsonValueKind.Null, (await p.AssignAsync(frame, "pain_point", painPoint, p.Verifier)).Ok().GetProperty("frame").ValueKind);
        var map = await p.Project.MapAsync();
        Assert.Equal(JsonValueKind.Null, map.GetProperty("dissolution").ValueKind);
        // Envoi still uses the document, now produced by Verifier: the flow carries it.
        var carrier = map.GetProperty("flows").EnumerateArray()
            .Single(flow => flow.GetProperty("source_step_id").GetGuid() == p.Verifier && flow.GetProperty("target_step_id").GetGuid() == p.Envoi);
        Assert.Equal([DocumentName], carrier.GetProperty("documents").EnumerateArray().Select(name => name.GetString()));
        Assert.Equal([PainPointText], PainPointTexts(Step(map, p.Verifier)));

        // Back to the remainder: the frame comes back; the server undo of that return closes it again.
        var reopened = (await p.AssignAsync(frame, "pain_point", painPoint, p.Saisie, p.Verifier)).Ok().GetProperty("frame");
        Assert.Single(reopened.GetProperty("pain_points").EnumerateArray());
        Assert.NotNull(await p.FrameAsync());
        using var facilitator = p.Project.Facilitator;
        var undone = (await facilitator.PostAsync($"/api/maps/{p.Project.RootMapId}/undo")).Ok();
        Assert.Equal("affecter", undone.GetProperty("code").GetString());
        Assert.Equal(JsonValueKind.Null, undone.GetProperty("map").GetProperty("dissolution").ValueKind);
        Assert.Equal([PainPointText], PainPointTexts(Step(undone.GetProperty("map"), p.Verifier)));
    }

    /// <summary>T-REMO-03, and « Finish » undone by the server (spec 4.6).</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Finish_stores_the_rest_on_the_last_step_of_the_frame_closes_it_does_nothing_twice_and_is_undone(EngineKind kind)
    {
        var p = await PlanAsync(kind);
        var frame = (await p.DissolveAsync()).GetProperty("id").GetGuid();
        using var facilitator = p.Project.Facilitator;
        // Preparer is deleted: Verifier stays alone in the frame.
        (await facilitator.DeleteAsync($"/api/steps/{p.Preparer}?stitch=true", await p.Project.StepVersionAsync(p.Preparer))).Ok();
        Assert.Equal([p.Verifier], FrameSteps((await p.FrameAsync())!.Value));

        var finished = (await facilitator.PostAsync($"/api/dissolutions/{frame}/finish")).Ok();
        var arrangements = finished.GetProperty("arrangements").EnumerateArray().ToList();
        Assert.Equal(2, arrangements.Count);
        Assert.All(arrangements, arrangement => Assert.Equal(p.Verifier, arrangement.GetProperty("step_id").GetGuid()));
        var map = await p.Project.MapAsync();
        Assert.Equal(JsonValueKind.Null, map.GetProperty("dissolution").ValueKind);
        Assert.Equal([PainPointText], PainPointTexts(Step(map, p.Verifier)));
        // Finish a second time: nothing.
        var again = (await facilitator.PostAsync($"/api/dissolutions/{frame}/finish")).Ok();
        Assert.Equal((0, JsonValueKind.Null), (again.GetProperty("arrangements").GetArrayLength(), again.GetProperty("operation_id").ValueKind));

        var undone = (await facilitator.PostAsync($"/api/maps/{p.Project.RootMapId}/undo")).Ok();
        Assert.Equal((finished.GetProperty("operation_id").GetGuid(), "terminer_remontee"), (undone.GetProperty("id").GetGuid(), undone.GetProperty("code").GetString()));
        var reopened = undone.GetProperty("map").GetProperty("dissolution");
        Assert.Equal((1, 1), (reopened.GetProperty("documents").GetArrayLength(), reopened.GetProperty("pain_points").GetArrayLength()));
        Assert.Empty(PainPointTexts(Step(undone.GetProperty("map"), p.Verifier)));
    }

    /// <summary>T-REMO-05, the part not proved by `DissolveRoutesTests`: two flows of different frequencies are not mixed.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_detail_is_not_dissolved_next_to_a_flow_of_another_frequency(EngineKind kind)
    {
        var project = await ApiProject.CreateAsync(await hosts.OfAsync(kind));
        using var facilitator = project.Facilitator;
        var daily = (await project.AddStepAsync(0, "Suivi journalier")).Id;
        var monthly = (await project.AddStepAsync(1, "Clôture mensuelle")).Id;
        foreach (var (step, frequency) in new[] { (daily, "daily"), (monthly, "month_end_close") })
        {
            var (subMap, subLane) = await project.DetailAsync(step);
            await project.AddStepAsync(0, "Tâche", subLane, subMap);
            (await facilitator.PutAsync($"/api/steps/{step}/sheet", new { fields = new { frequency = new { value = frequency } } }, await SheetReadAsync(facilitator, step)))
                .Ok();
        }
        (await facilitator.PostAsync($"/api/steps/{daily}/dissolve", null, await project.StepVersionAsync(daily))).Refused(400, "FREQUENCES_MELANGEES");
        Assert.Contains(daily, StepIds(await project.MapAsync()));
    }

    /// <summary>T-REMO-07: only what is on the map comes back to the remainder.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Only_what_is_on_the_map_is_brought_back_to_the_remainder(EngineKind kind)
    {
        var p = await PlanAsync(kind);
        var other = await PlanAsync(kind);
        var frame = (await p.DissolveAsync()).GetProperty("id").GetGuid();
        var otherDocument = other.Document!.Value;
        // A document of another project, brought back « from » a step of that project: refused.
        (await p.AssignAsync(frame, "document", otherDocument, p.Saisie, other.Saisie)).Refused(400, "ETAPE_HORS_CARTE");
        // ... or « from » a step of the map that does not hold it: not found, nothing moves.
        (await p.AssignAsync(frame, "document", otherDocument, p.Saisie, p.Envoi)).Refused(404, "INTROUVABLE");
        var otherMap = await other.Project.MapAsync();
        var otherPainPoint = Step(otherMap, other.Saisie).GetProperty("markers").GetProperty("pain_points")[0].GetProperty("pain_point_id").GetGuid();
        (await p.AssignAsync(frame, "pain_point", otherPainPoint, p.Saisie)).Refused(404, "INTROUVABLE");

        otherMap = await other.Project.MapAsync();
        Assert.Contains(otherDocument, Step(otherMap, other.Saisie).GetProperty("markers").GetProperty("documents").EnumerateArray()
            .Select(marker => marker.GetProperty("document_id").GetGuid()));
        Assert.Equal([PainPointText], PainPointTexts(Step(otherMap, other.Saisie)));
        Assert.Single((await p.FrameAsync())!.Value.GetProperty("documents").EnumerateArray());
    }

    /// <summary>T-REMO-08; the return of the usage is also proved by the server undo of its assignment.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_usage_does_not_go_to_a_step_that_already_receives_it_and_its_return_gives_back_only_the_usage_moved(EngineKind kind)
    {
        var p = await PlanAsync(kind);
        using var facilitator = p.Project.Facilitator;
        // Saisie also receives an export produced by Extraction, already received by Envoi.
        var export = await OutputAsync(facilitator, p.Extraction, "Export SAP");
        foreach (var step in new[] { p.Saisie, p.Envoi })
            (await facilitator.PutAsync($"/api/steps/{step}/uses/{export}", new { wait_hours = 2 }, NoUseRead)).Ok();
        var frame = (await p.DissolveAsync()).GetProperty("id").GetGuid();
        Assert.Equal("Export SAP", Assert.Single((await p.FrameAsync())!.Value.GetProperty("usages").EnumerateArray()).GetProperty("name").GetString());

        (await p.AssignAsync(frame, "usage", export, p.Envoi)).Refused(409, "USAGE_EXISTANT");
        (await p.AssignAsync(frame, "usage", export, p.Extraction)).Refused(409, "USAGE_EXISTANT");
        // The document produced by Saisie does not go to Envoi, which uses it.
        (await p.AssignAsync(frame, "document", p.Document!.Value, p.Envoi)).Refused(409, "DOCUMENT_UTILISE_PAR_CIBLE");
        (await p.AssignAsync(frame, "usage", export, p.Preparer)).Ok();
        Assert.Equal(new HashSet<Guid> { p.Preparer, p.Envoi }, await UsersOfAsync(facilitator, p, export));

        // Return: only the usage of Preparer comes back; the one of Envoi stays with Envoi.
        var back = (await p.AssignAsync(frame, "usage", export, p.Saisie, p.Preparer)).Ok().GetProperty("frame");
        Assert.Equal(export, Assert.Single(back.GetProperty("usages").EnumerateArray()).GetProperty("document_id").GetGuid());
        Assert.Equal(new HashSet<Guid> { p.Envoi }, await UsersOfAsync(facilitator, p, export));
        // The server undo of the return puts the usage on Preparer again, and only it.
        (await facilitator.PostAsync($"/api/maps/{p.Project.RootMapId}/undo")).Ok();
        Assert.Equal(new HashSet<Guid> { p.Preparer, p.Envoi }, await UsersOfAsync(facilitator, p, export));
    }

    /// <summary>The steps that use a document, read on the output of its producer (Extraction).</summary>
    private static async Task<IReadOnlySet<Guid>> UsersOfAsync(ApiClient client, DissolutionPortedPlan p, Guid document)
    {
        var sheet = (await client.GetAsync($"/api/steps/{p.Extraction}/sheet")).Ok();
        var output = sheet.GetProperty("outputs").EnumerateArray().Single(item => item.GetProperty("document").GetProperty("id").GetGuid() == document);
        return output.GetProperty("used_by").EnumerateArray().Select(use => use.GetProperty("step_id").GetGuid()).ToHashSet();
    }

    /// <summary>T-REMO-09.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_detail_is_not_dissolved_over_a_frame_open_below(EngineKind kind)
    {
        var p = await PlanAsync(kind);
        using var facilitator = p.Project.Facilitator;
        // In the detail, Preparer is detailed in turn, with an output: it is dissolved.
        var (subMap, subLane) = await p.Project.DetailAsync(p.Preparer, p.Detail);
        await p.Project.AddStepAsync(0, "Ouvrir", subLane, subMap);
        await OutputAsync(facilitator, p.Preparer, "Brouillon");
        (await facilitator.PostAsync($"/api/steps/{p.Preparer}/dissolve", null, await p.Project.StepVersionAsync(p.Preparer, p.Detail))).Ok();
        Assert.NotEqual(JsonValueKind.Null, (await p.Project.MapAsync(p.Detail)).GetProperty("dissolution").ValueKind);

        (await facilitator.PostAsync($"/api/steps/{p.Saisie}/dissolve", null, await p.Project.StepVersionAsync(p.Saisie))).Refused(409, "REMONTEE_EN_COURS_DESSOUS");
        Assert.Contains(p.Saisie, StepIds(await p.Project.MapAsync()));
    }

    /// <summary>T-REMO-11.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_raci_roles_follow_even_on_a_lane_without_step(EngineKind kind)
    {
        var p = await PlanAsync(kind);
        using var facilitator = p.Project.Facilitator;
        var quality = await p.Project.AddLaneAsync("Qualité", p.Detail);
        (await facilitator.PutAsync($"/api/steps/{p.Preparer}/raci", new { lane_id = quality, role = "C" }, await p.Project.StepVersionAsync(p.Preparer, p.Detail))).Ok();
        await p.DissolveAsync();
        var map = await p.Project.MapAsync();
        var inTheFlow = map.GetProperty("lanes").EnumerateArray().Single(lane => lane.GetProperty("name").GetString() == "Qualité").GetProperty("id").GetGuid();
        Assert.Contains(map.GetProperty("raci").EnumerateArray(), role => role.GetProperty("step_id").GetGuid() == p.Preparer
            && role.GetProperty("lane_id").GetGuid() == inTheFlow && role.GetProperty("role").GetString() == "C");
    }

    /// <summary>
    /// T-REMO-12: the batch of a dissolution is no longer shown to the client and the 1.x route that restored it alone is
    /// gone (AR-43); nothing of the remainder goes to an end step.
    /// </summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_batch_of_a_dissolution_is_not_restored_alone_and_nothing_is_assigned_to_an_end(EngineKind kind)
    {
        var p = await PlanAsync(kind);
        var dissolved = await p.DissolveAsync();
        Assert.False(dissolved.TryGetProperty("lot", out _));
        Assert.False(dissolved.TryGetProperty("batch_id", out _));
        using var facilitator = p.Project.Facilitator;
        (await facilitator.PostAsync($"/api/suppressions/{dissolved.GetProperty("operation_id").GetGuid()}/restaurer")).Refused(404, "INTROUVABLE");
        Assert.DoesNotContain(p.Saisie, StepIds(await p.Project.MapAsync()));

        var end = (await p.Project.AddStepAsync(9, "Fin", p.Lane, type: "end")).Id;
        (await p.AssignAsync(dissolved.GetProperty("id").GetGuid(), "document", p.Document!.Value, end)).Refused(400, "ETAPE_SANS_CONTENU");
        Assert.Single((await p.FrameAsync())!.Value.GetProperty("documents").EnumerateArray());
    }
}
