// ForeignKeyTests.cs
// Foreign keys on both engines: unknown references refused, composite key step(lane_id, map_id), maps and steps that reference each other (spec 4.2).

using Microsoft.EntityFrameworkCore;
using Topogenia.Domain.Model;
using Topogenia.Domain.Values;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Persistence.Tests.Model;

public sealed class ForeignKeyTests(MigratedDatabases databases) : IClassFixture<MigratedDatabases>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<(TestDatabase Database, ProjectGraph Graph)> WrittenAsync(EngineKind kind)
    {
        var database = await databases.OfAsync(kind);
        var graph = ProjectGraph.New();
        await Refusals.SaveAsync(database, [.. graph.Records]);
        return (database, graph);
    }

    private static Step NewStep(ProjectGraph graph, int code) =>
        graph.Steps[0] with { Id = Guid.CreateVersion7(), Code = graph.ReadableCode('E', 100 + code), Column = 80 + code };

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_row_that_names_an_unknown_row_is_refused(EngineKind kind)
    {
        var (database, graph) = await WrittenAsync(kind);
        await Refusals.AssertRefusedAsync(database, GuardKind.ForeignKey, "FK_flow_source_step",
            graph.LiveFlow with { Id = Guid.CreateVersion7(), SourceStepId = Guid.CreateVersion7() });
        await Refusals.AssertRefusedAsync(database, GuardKind.ForeignKey, "FK_lane_map",
            graph.Lanes[0] with { Id = Guid.CreateVersion7(), Code = graph.ReadableCode('C', 99), MapId = Guid.CreateVersion7() });
    }

    /// <summary>A comment of presentation names a step (ADR 099): unknown, it is refused; present, it keeps its step from being removed before it.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_comment_of_presentation_names_its_step_and_keeps_it_from_being_removed_first(EngineKind kind)
    {
        var (database, graph) = await WrittenAsync(kind);
        var comment = new Application.Presentation.PresentationComment(Guid.CreateVersion7(), "Ici la demande part.", "fr",
            Application.Presentation.CommentSource.Manual, 1, graph.At(20), graph.Owner.Id);
        await Refusals.AssertRefusedAsync(database, GuardKind.ForeignKey, "FK_presentation_comment_step", comment);
        var step = NewStep(graph, 5);
        await Refusals.SaveAsync(database, step, comment with { StepId = step.Id });
        await using var context = database.Context();
        context.Remove(step);
        var refusal = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(Token));
        Assert.Contains(kind == EngineKind.SqlServer ? "FK_presentation_comment_step" : "FOREIGN KEY constraint failed", refusal.InnerException!.Message,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// A comment of presentation of a flow names a flow (ADR 099, lot J10): unknown, or the identifier of a step, it is refused;
    /// present, it keeps its flow from being removed before it, a flow in the trash as a live one; its author is a user.
    /// </summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_comment_of_presentation_of_a_flow_names_its_flow_and_keeps_it_from_being_removed_first(EngineKind kind)
    {
        var (database, graph) = await WrittenAsync(kind);
        var comment = new Application.Presentation.FlowPresentationComment(Guid.CreateVersion7(), "Ici le dossier change de mains.", "fr",
            Application.Presentation.CommentSource.Manual, 1, graph.At(20), graph.Owner.Id);
        await Refusals.AssertRefusedAsync(database, GuardKind.ForeignKey, "FK_flow_presentation_comment_flow", comment);
        await Refusals.AssertRefusedAsync(database, GuardKind.ForeignKey, "FK_flow_presentation_comment_flow", comment with { FlowId = graph.Steps[0].Id });
        await Refusals.AssertRefusedAsync(database, GuardKind.ForeignKey, "FK_flow_presentation_comment_updated_by",
            comment with { FlowId = graph.LiveFlow.Id, UpdatedBy = Guid.CreateVersion7() });
        var flow = graph.DeletedFlow with { Id = Guid.CreateVersion7() };
        await Refusals.SaveAsync(database, flow, comment with { FlowId = flow.Id }, comment with { FlowId = flow.Id, Language = "en" });
        await using var context = database.Context();
        context.Remove(flow);
        var refusal = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(Token));
        Assert.Contains(kind == EngineKind.SqlServer ? "FK_flow_presentation_comment_flow" : "FOREIGN KEY constraint failed", refusal.InnerException!.Message,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// The voice a project keeps names its project (ADR 103): unknown, or the identifier of a map, it is refused; present,
    /// it keeps its project from being removed before it, a project in the trash as a live one; its author is a user.
    /// </summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_voice_names_its_project_and_keeps_it_from_being_removed_first(EngineKind kind)
    {
        var (database, graph) = await WrittenAsync(kind);
        var voice = new Application.Presentation.PresentationVoice(Guid.CreateVersion7(), "fr", "Hortense", true, 1, graph.At(20), graph.Owner.Id);
        await Refusals.AssertRefusedAsync(database, GuardKind.ForeignKey, "FK_presentation_voice_project", voice);
        await Refusals.AssertRefusedAsync(database, GuardKind.ForeignKey, "FK_presentation_voice_project", voice with { ProjectId = graph.RootMap.Id });
        await Refusals.AssertRefusedAsync(database, GuardKind.ForeignKey, "FK_presentation_voice_updated_by",
            voice with { ProjectId = graph.Project.Id, UpdatedBy = Guid.CreateVersion7() });
        var project = graph.Project with { Id = Guid.CreateVersion7(), Code = ProjectGraph.New().Code, DeletedAt = graph.At(21), DeletedBy = graph.Owner.Id };
        await Refusals.SaveAsync(database, project, voice with { ProjectId = project.Id }, voice with { ProjectId = project.Id, Language = "en", Local = false });
        await using var context = database.Context();
        context.Remove(project);
        var refusal = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(Token));
        Assert.Contains(kind == EngineKind.SqlServer ? "FK_presentation_voice_project" : "FOREIGN KEY constraint failed", refusal.InnerException!.Message,
            StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_step_on_a_lane_of_another_map_is_refused_by_the_composite_key(EngineKind kind)
    {
        var (database, graph) = await WrittenAsync(kind);
        // The lane exists and the map exists, but the lane is not on the map of the step (RG-C02, spec 4.2).
        await Refusals.AssertRefusedAsync(database, GuardKind.ForeignKey, "FK_step_lane", NewStep(graph, 2) with { MapId = graph.SubMap.Id });
        await Refusals.SaveAsync(database, NewStep(graph, 3) with { MapId = graph.SubMap.Id, LaneId = graph.SubLane.Id });
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_step_moved_to_another_map_must_take_a_lane_of_that_map(EngineKind kind)
    {
        var (database, graph) = await WrittenAsync(kind);
        await using (var context = database.Context())
        {
            var step = await context.Set<Step>().SingleAsync(item => item.Id == graph.Steps[6].Id, Token);
            context.Entry(step).Property(item => item.MapId).CurrentValue = graph.SubMap.Id;
            var refusal = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(Token));
            Assert.Contains(kind == EngineKind.SqlServer ? "FK_step_lane" : "FOREIGN KEY constraint failed", refusal.InnerException!.Message, StringComparison.Ordinal);
        }
        await using (var context = database.Context())
        {
            var step = await context.Set<Step>().SingleAsync(item => item.Id == graph.Steps[6].Id, Token);
            context.Entry(step).Property(item => item.MapId).CurrentValue = graph.SubMap.Id;
            context.Entry(step).Property(item => item.LaneId).CurrentValue = graph.SubLane.Id;
            context.Entry(step).Property(item => item.Column).CurrentValue = 40;
            await context.SaveChangesAsync(Token);
        }
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_row_still_referenced_cannot_be_removed(EngineKind kind)
    {
        var (database, graph) = await WrittenAsync(kind);
        await using var context = database.Context();
        context.Remove(graph.Steps[1]);
        var refusal = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(Token));
        Assert.Contains(kind == EngineKind.SqlServer ? "FK_" : "FOREIGN KEY constraint failed", refusal.InnerException!.Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Maps_and_steps_that_reference_each_other_are_created_in_one_save_and_removed_from_the_detail_up(EngineKind kind)
    {
        var database = await databases.OfAsync(kind);
        var graph = ProjectGraph.New();
        var owner = graph.Owner;
        var project = graph.Project with { UpdatedBy = null };
        var root = graph.RootMap;
        var lane = graph.Lanes[0] with { LaneType = LaneType.Department, ActorUserId = null };
        var step = graph.Steps[0] with { AssigneeUserId = null, UpdatedBy = null, LaneId = lane.Id };
        var detail = graph.SubMap with { ParentStepId = step.Id };
        var detailLane = graph.SubLane with { SourceLaneId = lane.Id };
        var detailStep = graph.SubStep;
        object[] records = [owner, project, root, lane, step, detail, detailLane, detailStep];
        await Refusals.SaveAsync(database, records);
        await using (var context = database.Context())
        {
            // The step carries the detailed map: removing it first is refused.
            context.Remove(step);
            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(Token));
        }
        await using (var context = database.Context())
        {
            context.RemoveRange(records.Skip(2).Reverse());
            await context.SaveChangesAsync(Token);
        }
        await using var reader = database.Context();
        Assert.False(await reader.Set<ProcessMap>().AnyAsync(map => map.ProjectId == project.Id, Token));
        Assert.False(await reader.Set<Step>().AnyAsync(item => item.Id == step.Id || item.Id == detailStep.Id, Token));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Identifiers_kept_without_foreign_key_accept_an_object_that_is_gone(EngineKind kind)
    {
        // Operations trimmed from the log, purged projects and lanes, polymorphic translations (docs/modele-donnees.md).
        var (database, graph) = await WrittenAsync(kind);
        var gone = Guid.CreateVersion7();
        await Refusals.SaveAsync(database,
            NewStep(graph, 4) with { OriginOperationId = gone },
            graph.Lanes[0] with { Id = Guid.CreateVersion7(), Code = graph.ReadableCode('C', 70), SortOrder = 7, SourceLaneId = gone },
            graph.Batch with { Id = Guid.CreateVersion7(), OperationId = gone },
            ProjectGraph.OtherUser(graph.Owner, "former") with { LastProjectId = gone },
            new AuditEvent(Guid.CreateVersion7(), graph.At(30), null, "project.purge", gone, null),
            graph.LogEntry with { Id = Guid.CreateVersion7(), Sequence = 40, RedoneFromId = gone },
            new DissolutionStep(graph.Dissolution.Id, graph.Steps[5].Id, gone, 0, 0),
            new DissolutionObject(graph.Dissolution.Id, Domain.Dissolutions.DissolutionObjectTypes.Flow, gone),
            new Translation(TranslationObject.Lane, gone, TranslationField.Name, "en", "Gone", TranslationSource.Auto, graph.At(30), null));
    }
}
