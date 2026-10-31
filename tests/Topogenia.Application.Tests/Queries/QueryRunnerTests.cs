// QueryRunnerTests.cs
// The read template: declared rights, no lock nor writing, map view and history of the reader (spec 3.4, annex A 1, B 2.6).

using Topogenia.Application.Authorization;
using Topogenia.Application.Maps;
using Topogenia.Application.OperationLog;
using Topogenia.Application.Ports;
using Topogenia.Application.Queries;
using Topogenia.Application.Tests.Support;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;

namespace Topogenia.Application.Tests.Queries;

[RequiresSheetFill]
public sealed class SheetProbeQuery(Guid stepId) : ProjectQuery<WorkspaceContent>(AccessTarget.Step(stepId))
{
    public override Guid? SheetStepId => Target.Id;

    public override WorkspaceContent Content => WorkspaceContent.Sheets;

    public override WorkspaceContent Read(ProjectWorkspace workspace, QueryContext context) => Content;
}

public sealed class UndeclaredQuery(Guid mapId) : ProjectQuery<bool>(AccessTarget.Map(mapId))
{
    public override bool Read(ProjectWorkspace workspace, QueryContext context) => true;
}

public sealed class QueryRunnerTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_reader_reads_the_map_without_any_lock_nor_writing()
    {
        var project = new TestProject();
        project.ActingAs(project.Reader);
        var read = await project.Queries.RunAsync(new GetMapQuery(project.RootMapId), Token);
        Assert.Equal(new MapRightsView(false, MemberRole.Reader), read.View.Rights);
        Assert.Equal(["access", "load"], project.Database.Calls);
        Assert.Equal(0, project.Project.StructureVersion);
        Assert.Equal(0, project.Database.Commits);
        Assert.Equal([WorkspaceContent.Sheets | WorkspaceContent.Documents | WorkspaceContent.Translations], project.Database.LoadedContents);
    }

    [Fact]
    public async Task A_non_member_and_a_map_in_the_trash_are_not_found()
    {
        var project = new TestProject();
        project.ActingAs(project.Stranger);
        Assert.Equal(ErrorCodes.NotFound, (await Assert.ThrowsAsync<DomainException>(() =>
            project.Queries.RunAsync(new GetMapQuery(project.RootMapId), Token))).Code);
        Assert.Equal(["access"], project.Database.Calls);
        project.ActingAs(project.Facilitator);
        project.PutInTrash();
        Assert.Equal(ErrorCodes.NotFound, (await Assert.ThrowsAsync<DomainException>(() =>
            project.Queries.RunAsync(new GetMapQuery(project.RootMapId), Token))).Code);
    }

    [Fact]
    public async Task An_anonymous_read_and_a_query_without_right_are_refused_before_any_read()
    {
        var project = new TestProject();
        await Assert.ThrowsAsync<InvalidOperationException>(() => project.Queries.RunAsync(new UndeclaredQuery(project.RootMapId), Token));
        project.CurrentUser.User = null;
        Assert.Equal(ErrorCodes.NotAuthenticated, (await Assert.ThrowsAsync<DomainException>(() =>
            project.Queries.RunAsync(new GetMapQuery(project.RootMapId), Token))).Code);
        Assert.Empty(project.Database.Calls);
    }

    [Fact]
    public async Task The_read_of_a_map_carries_the_history_of_its_reader_only()
    {
        var project = new TestProject();
        var done = await project.Runner.RunAsync(new Topogenia.Application.Lanes.AddLaneCommand(
            new Domain.Lanes.AddLaneRequest(project.RootMapId, "Achats")), Token);
        await project.Runner.RunAsync(new Topogenia.Application.Lanes.AddLaneCommand(new Domain.Lanes.AddLaneRequest(project.RootMapId, "Ventes")), Token);
        await project.Runner.RunAsync(new UndoCommand(project.RootMapId), Token);
        var read = await project.Queries.RunAsync(new GetMapQuery(project.RootMapId), Token);
        Assert.Equal([done.OperationId!.Value], read.History.Undo.Select(item => item.OperationId));
        Assert.Equal([OperationKinds.CreateLane], read.History.Redo.Select(item => item.Kind));
        project.ActingAs(project.Admin);
        var other = await project.Queries.RunAsync(new GetMapQuery(project.RootMapId), Token);
        Assert.Equal((0, 0), (other.History.Undo.Count, other.History.Redo.Count));
        Assert.Equal(["Acteur 1", "Achats"], other.View.Lanes.Select(lane => lane.Name));
    }

    [Fact]
    public async Task A_read_that_requires_the_right_to_fill_is_open_to_who_may_fill_and_loads_only_its_content()
    {
        var project = new TestProject();
        var assigned = project.Step(0, assignee: project.Contributor.Id);
        var other = project.Step(1);
        project.ActingAs(project.Contributor);
        Assert.Equal(WorkspaceContent.Sheets, await project.Queries.RunAsync(new SheetProbeQuery(assigned), Token));
        Assert.Equal([WorkspaceContent.Sheets], project.Database.LoadedContents);
        Assert.Equal(ErrorCodes.AccessDenied, (await Assert.ThrowsAsync<DomainException>(() =>
            project.Queries.RunAsync(new SheetProbeQuery(other), Token))).Code);
    }

    [Fact]
    public async Task The_history_of_a_map_is_read_alone_and_a_deleted_map_has_none()
    {
        var project = new TestProject();
        var done = await project.Runner.RunAsync(new Topogenia.Application.Lanes.AddLaneCommand(
            new Domain.Lanes.AddLaneRequest(project.RootMapId, "Achats")), Token);
        var history = await project.Queries.RunAsync(new GetHistoryQuery(project.RootMapId), Token);
        Assert.Equal([done.OperationId!.Value], history.Undo.Select(item => item.OperationId));
        Assert.Equal([WorkspaceContent.None], project.Database.LoadedContents.Skip(1));
        var (subMap, _) = project.SubMap(project.Step(0));
        project.Database.Add(project.Database.Get<Domain.Model.ProcessMap>(Domain.Model.ProcessMap.KeyOf(subMap)) with { DeletedAt = TestProject.Now });
        Assert.Equal(ErrorCodes.NotFound, (await Assert.ThrowsAsync<DomainException>(() =>
            project.Queries.RunAsync(new GetHistoryQuery(subMap), Token))).Code);
    }
}
