// UndoOfCreationRoutesTests.cs
// The undo of a step or a detail created, after a document or a summary was written on it out of the undo stack, through the whole host on both databases: the step or the map goes to the trash with what lies on it, the redo brings both back (audit 3.1 V4, A3 refuted).

using System.Text.Json;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Host.Tests.Selection;
using Topogenia.Host.Tests.Steps;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Api;

/// <summary>
/// A document added to a step, a summary written for a map, are out of the undo stack (neither is undone). The undo of
/// the creation of their step or map deletes it logically (`ProjectWorkspace.ApplyInverse`): it lies in the trash with its
/// document or its summary, as any step or map deleted, until the purge takes them together (`PurgeScope`). So the undo is
/// not refused, and the redo brings the step or the map back with what was written on it.
/// </summary>
public sealed class UndoOfCreationRoutesTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private static (int Undo, int Redo) Stacks(JsonElement history) =>
        (history.GetProperty("undo").GetArrayLength(), history.GetProperty("redo").GetArrayLength());

    private static IEnumerable<Guid> StepsOf(JsonElement map) => map.GetProperty("steps").EnumerateArray().Select(step => step.GetProperty("id").GetGuid());

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_undo_of_a_step_created_that_got_a_document_meanwhile_keeps_the_document_and_the_redo_brings_both_back(EngineKind kind)
    {
        var project = await ApiProject.CreateAsync(await hosts.OfAsync(kind));
        using var facilitator = project.Facilitator;
        var map = $"/api/maps/{project.RootMapId}";
        var step = await MapDrawing.StepAsync(facilitator, project.RootMapId, project.FirstLaneId, 0, "Saisir");
        (await facilitator.PostAsync($"/api/steps/{step}/documents", new { direction = "output", name = "Bon de commande" })).Ok();

        var undone = (await facilitator.PostAsync($"{map}/undo")).Ok();
        Assert.DoesNotContain(step, StepsOf(undone.GetProperty("map")));
        Assert.Equal((0, 1), Stacks(undone.GetProperty("history")));
        var redone = (await facilitator.PostAsync($"{map}/redo")).Ok();

        Assert.Contains(step, StepsOf(redone.GetProperty("map")));
        var outputs = (await facilitator.GetAsync($"/api/steps/{step}/sheet")).Ok().GetProperty("outputs");
        Assert.Equal(["Bon de commande"], outputs.EnumerateArray().Select(output => output.GetProperty("document").GetProperty("name").GetString()));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_undo_of_a_detail_whose_map_got_a_summary_meanwhile_keeps_the_summary_and_the_redo_brings_both_back(EngineKind kind)
    {
        var project = await ApiProject.CreateAsync(await hosts.OfAsync(kind));
        using var facilitator = project.Facilitator;
        var map = $"/api/maps/{project.RootMapId}";
        var step = await MapDrawing.StepAsync(facilitator, project.RootMapId, project.FirstLaneId, 0, "Traiter");
        var detail = (await facilitator.PostAsync($"/api/steps/{step}/detail", null, await project.StepVersionAsync(step))).Ok().GetProperty("id").GetGuid();
        (await facilitator.PutAsync($"{map}/summaries", new { summaries = new[] { new { map_id = detail, text = "Le détail du traitement." } } })).Ok();

        var undone = (await facilitator.PostAsync($"{map}/undo")).Ok();
        Assert.Equal(JsonValueKind.Null, undone.GetProperty("map").GetProperty("steps")[0].GetProperty("sub_map_id").ValueKind);
        (await facilitator.PostAsync($"{map}/redo")).Ok();

        Assert.Equal(detail, (await project.MapAsync()).GetProperty("steps")[0].GetProperty("sub_map_id").GetGuid());
        Assert.Equal("Le détail du traitement.", (await project.MapAsync(detail)).GetProperty("summary").GetProperty("text").GetString());
    }
}
