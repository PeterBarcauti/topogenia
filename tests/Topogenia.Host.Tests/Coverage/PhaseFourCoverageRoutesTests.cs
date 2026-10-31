// PhaseFourCoverageRoutesTests.cs
// Lines of annexes A and B still open at the gate of phase 4, proved through the whole host on both databases: lanes, remainder, daily purge (P4-T10).

using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Topogenia.Application.Maintenance;
using Topogenia.Application.Ports;
using Topogenia.Domain.Model;
using Topogenia.Domain.Workspace;
using Topogenia.Host.Tests.Dissolutions;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Host.Tests.Steps;
using Topogenia.Persistence.Tests.Infrastructure;
using static Topogenia.Host.Tests.Ported.StructurePortedCalls;
using static Topogenia.Host.Tests.Selection.MapDrawing;

namespace Topogenia.Host.Tests.Coverage;

public sealed class PhaseFourCoverageRoutesTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    /// <summary>The clock of the host moved by some days, for the daily purge.</summary>
    private sealed class LaterClock(int days) : IClock
    {
        public DateTime UtcNow => DateTime.UtcNow.AddDays(days);
    }

    private async Task<ApiProject> ProjectAsync(EngineKind kind) => await ApiProject.CreateAsync(await hosts.OfAsync(kind));

    private static IReadOnlyList<Guid> UndoOperations(JsonElement history) =>
        [.. history.GetProperty("undo").EnumerateArray().Select(item => item.GetProperty("operation_id").GetGuid())];

    private static IReadOnlyList<Guid> DocumentsOf(JsonElement map, Guid stepId) =>
        [.. Step(map, stepId).GetProperty("markers").GetProperty("documents").EnumerateArray().Select(marker => marker.GetProperty("document_id").GetGuid())];

    private static async Task<ProjectWorkspace> WorkspaceAsync(ApiHost host, Guid projectId)
    {
        await using var scope = host.Services.CreateAsyncScope();
        return (await scope.ServiceProvider.GetRequiredService<IWorkspaceStore>().LoadAsync(projectId, WorkspaceContent.All, TestContext.Current.CancellationToken))!;
    }

    /// <summary>A-1.3-04 (R957): a lane whose translation is in a language the host does not configure is refused, and nothing is written.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_lane_added_with_a_translation_in_a_language_not_configured_is_refused_and_nothing_is_written(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var refused = await facilitator.PostAsync(LanesUrl(project), new { name = "Ventes", translations = new Dictionary<string, string> { ["eo"] = "Vendado" } });
        Assert.Equal("translations", refused.Refused(400, "LANGUE_INVALIDE").Field());
        Assert.Equal(["Acteur 1"], LaneNames(await project.MapAsync()));
        Assert.Empty(UndoOperations((await facilitator.GetAsync($"/api/maps/{project.RootMapId}/history")).Ok()));

        // The same lane in a configured language is written with its translation: the language alone was refused.
        var added = (await facilitator.PostAsync(LanesUrl(project), new { name = "Ventes", translations = new Dictionary<string, string> { ["en"] = "Sales" } })).Ok();
        Assert.Equal("Sales", LaneOf(await project.MapAsync(), added.GetProperty("id").GetGuid()).GetProperty("translations").GetProperty("en").GetString());
    }

    /// <summary>B-2.12-21 (EnTetesCouloirs.tsx): a lane added without a type is a person (the name « Acteur N » is given by the interface, phase 6).</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_lane_added_without_a_type_is_a_person(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        var system = (await AddLaneAsync(project, "Progiciel")).GetProperty("id").GetGuid();
        var map = await project.MapAsync();
        Assert.Equal("person", LaneOf(map, system).GetProperty("type").GetString());
        Assert.Equal(["Acteur 1", "Progiciel"], LaneNames(map));
    }

    /// <summary>
    /// A-1.5-09 (1.x `ServiceStructure.Remontee.cs:400`): bringing an element back to the remainder of a closed frame would
    /// reopen it; while another frame is open on the map it is refused REMONTEE_EN_COURS and nothing moves.
    /// </summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Bringing_back_to_a_closed_frame_while_another_frame_is_open_on_the_map_is_refused_with_dissolution_in_progress(EngineKind kind)
    {
        var row = await DetailedRow.CreateAsync(await ProjectAsync(kind), "Bon de commande");
        var root = row.Project.RootMapId;
        using var facilitator = row.Project.Facilitator;
        var closed = (await facilitator.PostAsync($"/api/steps/{row.Macro}/dissolve", null, await row.MacroVersionAsync())).Ok().GetProperty("id").GetGuid();
        var document = row.Documents[0];
        (await facilitator.PostAsync($"/api/dissolutions/{closed}/assign", new { kind = "document", object_id = document, step_id = row.First })).Ok();
        Assert.Equal(JsonValueKind.Null, (await row.Project.MapAsync()).GetProperty("dissolution").ValueKind);

        var macro = (await facilitator.PostAsync($"/api/maps/{root}/group", new { steps = new[] { row.After }, label = "Clôture" })).Ok().GetProperty("id").GetGuid();
        var open = (await facilitator.PostAsync($"/api/steps/{macro}/dissolve", null, await row.Project.StepVersionAsync(macro))).Ok().GetProperty("id").GetGuid();

        var back = new { kind = "document", object_id = document, step_id = row.Macro, from_step_id = row.First };
        (await facilitator.PostAsync($"/api/dissolutions/{closed}/assign", back)).Refused(409, "REMONTEE_EN_COURS");
        var map = await row.Project.MapAsync();
        Assert.Equal(open, map.GetProperty("dissolution").GetProperty("id").GetGuid());
        Assert.Equal([document], DocumentsOf(map, row.First));

        // Once the other frame is finished, the same request reopens the closed frame with the document.
        (await facilitator.PostAsync($"/api/dissolutions/{open}/finish")).Ok();
        var reopened = (await facilitator.PostAsync($"/api/dissolutions/{closed}/assign", back)).Ok();
        Assert.Equal(closed, reopened.GetProperty("map").GetProperty("dissolution").GetProperty("id").GetGuid());
        Assert.Empty(DocumentsOf(reopened.GetProperty("map"), row.First));
    }

    /// <summary>
    /// A-2.9-58 (R1204): the daily purge run past the retention keeps the batch of an open frame, whose dissolved step
    /// still carries the remainder, and purges the other expired batches of the same project.
    /// </summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_daily_purge_past_the_retention_keeps_the_batch_of_an_open_frame_and_purges_the_other_expired_batches(EngineKind kind)
    {
        var host = await hosts.OfAsync(kind);
        var row = await DetailedRow.CreateAsync(await ApiProject.CreateAsync(host), "Bon de commande");
        using var facilitator = row.Project.Facilitator;
        var archive = await row.Project.AddStepAsync(6, "Archiver");
        (await facilitator.DeleteAsync($"/api/steps/{archive.Id}", await row.Project.StepVersionAsync(archive.Id))).Ok();
        var frame = (await facilitator.PostAsync($"/api/steps/{row.Macro}/dissolve", null, await row.MacroVersionAsync())).Ok().GetProperty("id").GetGuid();
        var before = await WorkspaceAsync(host, row.Project.Id);
        var frameBatch = before.All<Dissolution>().Single(dissolution => dissolution.Id == frame).DeletionBatchId;
        var archiveBatch = before.All<Step>().Single(step => step.Id == archive.Id).DeletionBatchId!.Value;
        // Three batches: the flows replaced by the group of the row, the deleted step, the dissolved step of the open frame.
        Assert.Equal(3, before.All<DeletionBatch>().Count());

        await using (var scope = host.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            var report = await new ExpiredPurge(new LaterClock(31), services.GetRequiredService<ITrashPurge>(), services.GetRequiredService<IWorkspaceStore>(),
                services.GetRequiredService<AppSettings>(), services.GetRequiredService<IAuditStore>()).RunAsync(TestContext.Current.CancellationToken);
            Assert.Empty(report.Failures);
            Assert.Equal((false, 2), report.Purged.Where(done => done.ProjectId == row.Project.Id).Select(done => (done.Whole, done.Batches)).Single());
        }

        var after = await WorkspaceAsync(host, row.Project.Id);
        Assert.Equal([frameBatch], after.All<DeletionBatch>().Select(batch => batch.Id));
        Assert.DoesNotContain(after.All<Step>(), step => step.Id == archive.Id || step.DeletionBatchId == archiveBatch);
        Assert.Contains(after.All<Step>(), step => step.Id == row.Macro && step.DeletionBatchId == frameBatch);
        var remainder = (await row.Project.MapAsync()).GetProperty("dissolution");
        Assert.Equal(frame, remainder.GetProperty("id").GetGuid());
        Assert.Equal(["Bon de commande"], remainder.GetProperty("documents").EnumerateArray().Select(item => item.GetProperty("name").GetString()));
        Assert.Equal(row.Documents[0], (await facilitator.PostAsync($"/api/dissolutions/{frame}/finish")).Ok().GetProperty("arrangements")[0]
            .GetProperty("object_id").GetGuid());
    }
}
