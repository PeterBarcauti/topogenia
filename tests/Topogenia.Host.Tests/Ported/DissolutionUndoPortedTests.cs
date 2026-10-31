// DissolutionUndoPortedTests.cs
// The 1.x tests that cancelled a dissolution (RemonteeTests, T-REMO) ported to the server undo through the whole host on both databases.

using System.Text.Json;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Host.Tests.Steps;
using Topogenia.Persistence.Tests.Infrastructure;
using static Topogenia.Host.Tests.Ported.DissolutionPortedPlan;
using static Topogenia.Host.Tests.Selection.MapDrawing;

namespace Topogenia.Host.Tests.Ported;

public sealed class DissolutionUndoPortedTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private async Task<DissolutionPortedPlan> PlanAsync(EngineKind kind, bool withRemainder = true) =>
        await DissolutionPortedPlan.CreateAsync(await ApiProject.CreateAsync(await hosts.OfAsync(kind)), withRemainder);

    private static string UndoUrl(DissolutionPortedPlan p) => $"/api/maps/{p.Project.RootMapId}/undo";

    /// <summary>T-REMO-04: the undo also puts the steps of the flow back in their cells, which the 1.x client did (spec 4.6).</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Undoing_the_dissolution_gives_back_the_detailed_step_its_flows_its_detail_and_the_lanes_and_it_can_be_dissolved_again(EngineKind kind)
    {
        var p = await PlanAsync(kind);
        var dissolved = await p.DissolveAsync();
        using var facilitator = p.Project.Facilitator;
        var undone = (await facilitator.PostAsync(UndoUrl(p))).Ok();
        Assert.Equal((dissolved.GetProperty("operation_id").GetGuid(), "remonter"), (undone.GetProperty("id").GetGuid(), undone.GetProperty("code").GetString()));

        var map = await p.Project.MapAsync();
        Assert.Equal(JsonValueKind.Null, map.GetProperty("dissolution").ValueKind);
        var saisie = Step(map, p.Saisie);
        Assert.Equal((1, p.Detail), (saisie.GetProperty("column").GetInt32(), saisie.GetProperty("sub_map_id").GetGuid()));
        Assert.Equal((0, 2), (Column(map, p.Extraction), Column(map, p.Envoi)));
        Assert.Equal(["Controlling"], LaneNames(map));
        Assert.Equal(new HashSet<(Guid, Guid, string?)> { (p.Extraction, p.Saisie, "oui"), (p.Saisie, p.Envoi, null) }, LabelledFlows(map));
        Assert.Single(saisie.GetProperty("markers").GetProperty("documents").EnumerateArray());
        var detail = await p.Project.MapAsync(p.Detail);
        Assert.Equal((4, 3), (detail.GetProperty("steps").GetArrayLength(), detail.GetProperty("flows").GetArrayLength()));
        Assert.Equal((1, 2), (Column(detail, p.Preparer), Column(detail, p.Verifier)));

        // And it can be dissolved again.
        await p.DissolveAsync();
        Assert.DoesNotContain(p.Saisie, StepIds(await p.Project.MapAsync()));
    }

    /// <summary>
    /// T-REMO-10: a flow drawn since between the frame and the flow makes the inverse break an invariant: CARTE_MODIFIEE
    /// (AR-04, AR-43), and the refusal clears the stacks of the user on the map (AR-05). The flow is drawn by a second
    /// facilitator, because the undo of the first one takes back his own last operation first.
    /// </summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_undo_of_a_dissolution_is_refused_as_map_modified_when_a_flow_links_the_frame_to_the_flow(EngineKind kind)
    {
        var p = await PlanAsync(kind);
        await p.DissolveAsync();
        using var admin = p.Project.Admin;
        using var facilitator = p.Project.Facilitator;
        (await admin.PostAsync($"/api/maps/{p.Project.RootMapId}/flows", new { source_step_id = p.Extraction, target_step_id = p.Verifier })).Ok();

        (await facilitator.PostAsync(UndoUrl(p))).Refused(409, "CARTE_MODIFIEE");
        var map = await p.Project.MapAsync();
        Assert.DoesNotContain(p.Saisie, StepIds(map));
        Assert.NotEqual(JsonValueKind.Null, map.GetProperty("dissolution").ValueKind);
        var history = (await facilitator.GetAsync($"/api/maps/{p.Project.RootMapId}/history")).Ok();
        Assert.Equal((0, 0), (history.GetProperty("undo").GetArrayLength(), history.GetProperty("redo").GetArrayLength()));
    }

    /// <summary>T-REMO-10: a step put then deleted in the lane created by the dissolution does not block its undo, which removes the lane.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_step_put_then_deleted_in_the_lane_created_by_the_dissolution_does_not_block_its_undo(EngineKind kind)
    {
        var p = await PlanAsync(kind);
        await p.DissolveAsync();
        using var admin = p.Project.Admin;
        using var facilitator = p.Project.Facilitator;
        var logistique = (await p.Project.MapAsync()).GetProperty("lanes").EnumerateArray()
            .Single(lane => lane.GetProperty("name").GetString() == "Logistique").GetProperty("id").GetGuid();
        var passage = (await admin.PostAsync($"/api/maps/{p.Project.RootMapId}/steps", new { lane_id = logistique, column = 9, label = "Passage" }))
            .Ok().GetProperty("id").GetGuid();
        (await admin.DeleteAsync($"/api/steps/{passage}", await p.Project.StepVersionAsync(passage))).Ok();

        var undone = (await facilitator.PostAsync(UndoUrl(p))).Ok();
        Assert.Equal("remonter", undone.GetProperty("code").GetString());
        Assert.Equal(["Controlling"], LaneNames(undone.GetProperty("map")));
        Assert.Contains(p.Saisie, StepIds(undone.GetProperty("map")));
    }

    /// <summary>
    /// T-REMO-10, A-2.9-48: a live step left in the lane created by the dissolution makes its undo break an invariant:
    /// CARTE_MODIFIEE (AR-79), and the lane and the step are kept.
    /// </summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_step_left_in_the_lane_created_by_the_dissolution_blocks_its_undo_as_map_modified(EngineKind kind)
    {
        var p = await PlanAsync(kind);
        await p.DissolveAsync();
        using var admin = p.Project.Admin;
        using var facilitator = p.Project.Facilitator;
        var logistique = (await p.Project.MapAsync()).GetProperty("lanes").EnumerateArray()
            .Single(lane => lane.GetProperty("name").GetString() == "Logistique").GetProperty("id").GetGuid();
        var passage = (await admin.PostAsync($"/api/maps/{p.Project.RootMapId}/steps", new { lane_id = logistique, column = 9, label = "Passage" }))
            .Ok().GetProperty("id").GetGuid();

        (await facilitator.PostAsync(UndoUrl(p))).Refused(409, "CARTE_MODIFIEE");
        var map = await p.Project.MapAsync();
        Assert.Equal(["Controlling", "Logistique"], LaneNames(map));
        Assert.Equal(logistique, Step(map, passage).GetProperty("lane_id").GetGuid());
        Assert.DoesNotContain(p.Saisie, StepIds(map));
    }

    /// <summary>
    /// A-2.15-04, AR-66, AR-108: the batch of a dissolution is never taken back as an ordinary batch. The facilitator
    /// renamed the detailed step, the administrator dissolved it since: the undo of the renaming is OPERATION_DEPASSEE
    /// with the author (not CARTE_MODIFIEE), the step stays dissolved and the frame open.
    /// </summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_undo_of_an_operation_on_a_step_dissolved_since_by_another_user_is_outdated(EngineKind kind)
    {
        var p = await PlanAsync(kind);
        using var admin = p.Project.Admin;
        using var facilitator = p.Project.Facilitator;
        (await facilitator.PatchAsync($"/api/steps/{p.Saisie}", new { label = "Saisir" }, await p.Project.StepVersionAsync(p.Saisie))).Ok();
        (await admin.PostAsync($"/api/steps/{p.Saisie}/dissolve", null, await p.Project.StepVersionAsync(p.Saisie))).Ok();

        var details = (await facilitator.PostAsync(UndoUrl(p))).Refused(409, "OPERATION_DEPASSEE");
        Assert.Equal(("admin", "à l'instant"), (details.GetProperty("by").GetString(), details.GetProperty("when").GetString()));
        var map = await p.Project.MapAsync();
        Assert.DoesNotContain(p.Saisie, StepIds(map));
        Assert.Equal([p.Preparer, p.Verifier], map.GetProperty("dissolution").GetProperty("frame_step_ids").EnumerateArray().Select(id => id.GetGuid()));
    }

    /// <summary>
    /// B-2.9-41: Ctrl+Z takes back the assignments one by one, the last first, then the whole dissolution (spec 4.6: by
    /// the server).
    /// </summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_undo_takes_back_the_assignments_one_by_one_then_the_dissolution(EngineKind kind)
    {
        var p = await PlanAsync(kind);
        var frame = (await p.DissolveAsync()).GetProperty("id").GetGuid();
        var painPoint = (await p.FrameAsync())!.Value.GetProperty("pain_points")[0].GetProperty("id").GetGuid();
        (await p.AssignAsync(frame, "document", p.Document!.Value, p.Verifier)).Ok();
        (await p.AssignAsync(frame, "pain_point", painPoint, p.Verifier)).Ok();
        Assert.Null(await p.FrameAsync());
        using var facilitator = p.Project.Facilitator;

        var first = (await facilitator.PostAsync(UndoUrl(p))).Ok();
        Assert.Equal("affecter", first.GetProperty("code").GetString());
        Assert.Equal((0, 1), Remainder(first.GetProperty("map")));
        Assert.Empty(Step(first.GetProperty("map"), p.Verifier).GetProperty("markers").GetProperty("pain_points").EnumerateArray());

        var second = (await facilitator.PostAsync(UndoUrl(p))).Ok();
        Assert.Equal("affecter", second.GetProperty("code").GetString());
        Assert.Equal((1, 1), Remainder(second.GetProperty("map")));

        var third = (await facilitator.PostAsync(UndoUrl(p))).Ok();
        Assert.Equal("remonter", third.GetProperty("code").GetString());
        Assert.Equal(JsonValueKind.Null, third.GetProperty("map").GetProperty("dissolution").ValueKind);
        Assert.Contains(p.Saisie, StepIds(third.GetProperty("map")));
        Assert.DoesNotContain(p.Verifier, StepIds(third.GetProperty("map")));
    }

    /// <summary>The documents and pain points left in the open frame of a map.</summary>
    private static (int Documents, int PainPoints) Remainder(JsonElement map)
    {
        var frame = map.GetProperty("dissolution");
        return (frame.GetProperty("documents").GetArrayLength(), frame.GetProperty("pain_points").GetArrayLength());
    }

    /// <summary>
    /// T-REMO-10, spec 4.6: an object touched by the dissolution changed since by another user: OPERATION_DEPASSEE with the
    /// author and the time of the change.
    /// </summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_undo_of_a_dissolution_is_outdated_when_a_step_it_touched_changed_since_and_names_the_author(EngineKind kind)
    {
        var p = await PlanAsync(kind);
        await p.DissolveAsync();
        using var admin = p.Project.Admin;
        using var facilitator = p.Project.Facilitator;
        (await admin.PatchAsync($"/api/steps/{p.Preparer}", new { label = "Préparer la saisie" }, await p.Project.StepVersionAsync(p.Preparer))).Ok();

        var details = (await facilitator.PostAsync(UndoUrl(p))).Refused(409, "OPERATION_DEPASSEE");
        Assert.Equal(("admin", "à l'instant"), (details.GetProperty("by").GetString(), details.GetProperty("when").GetString()));
        Assert.DoesNotContain(p.Saisie, StepIds(await p.Project.MapAsync()));
    }

    /// <summary>
    /// T-REMO-13: without remainder the frame stays open until « Finish »; closing an empty frame is not logged (spec 4.6),
    /// so the next undo still takes back the dissolution, frame closed.
    /// </summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Without_remainder_the_frame_stays_until_it_is_closed_and_the_dissolution_is_still_undone(EngineKind kind)
    {
        var p = await PlanAsync(kind, withRemainder: false);
        var frameId = (await p.DissolveAsync()).GetProperty("id").GetGuid();
        var frame = (await p.FrameAsync())!.Value;
        Assert.Equal((0, 0, 0), (frame.GetProperty("documents").GetArrayLength(), frame.GetProperty("usages").GetArrayLength(),
            frame.GetProperty("pain_points").GetArrayLength()));
        Assert.Equal([p.Preparer, p.Verifier], frame.GetProperty("frame_step_ids").EnumerateArray().Select(id => id.GetGuid()));

        using var facilitator = p.Project.Facilitator;
        var closed = (await facilitator.PostAsync($"/api/dissolutions/{frameId}/finish")).Ok();
        Assert.Equal((0, JsonValueKind.Null), (closed.GetProperty("arrangements").GetArrayLength(), closed.GetProperty("operation_id").ValueKind));
        Assert.Null(await p.FrameAsync());

        var undone = (await facilitator.PostAsync(UndoUrl(p))).Ok();
        Assert.Equal("remonter", undone.GetProperty("code").GetString());
        Assert.Contains(p.Saisie, StepIds(await p.Project.MapAsync()));
    }
}
