// BesideTemplateTests.cs
// The steps of the templates for the records kept beside the closed core (ADR 099 revised): read after the workspace and before the domain, written after the change set and before the operation log, in the same unit; read by a query before its projection.

using Topogenia.Application.Authorization;
using Topogenia.Application.Commands;
using Topogenia.Application.Ports;
using Topogenia.Application.Presentation;
using Topogenia.Application.Queries;
using Topogenia.Application.Tests.Support;
using Topogenia.Domain.Lanes;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;

namespace Topogenia.Application.Tests.Commands;

public sealed class BesideTemplateTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A command that adds a lane and notes, among the calls of the database, when it reads and writes beside the core.</summary>
    [RequiresRole(MemberRole.Facilitator)]
    private sealed class BesideProbe(TestProject project, bool fails = false) : ProjectCommand<LaneResult>(AccessTarget.Map(project.RootMapId))
    {
        public int Reads { get; private set; }

        public bool WorkspaceGiven { get; private set; }

        public override string? LogKind => "lane.add";

        public override Guid? MapOf(ProjectWorkspace workspace) => Target.Id;

        public override Task ReadBesideAsync(ProjectWorkspace workspace, BesidePorts beside, CancellationToken cancellationToken)
        {
            Reads++;
            WorkspaceGiven = workspace.ProjectId == project.ProjectId && ReferenceEquals(beside, project.Beside);
            project.Database.Calls.Add("beside-read");
            return Task.CompletedTask;
        }

        public override LaneResult Execute(ProjectWorkspace workspace, OperationContext context)
        {
            project.Database.Calls.Add("domain");
            return LaneOperations.Add(workspace, context, new AddLaneRequest(Target.Id, "Achats"));
        }

        public override Task WriteBesideAsync(BesidePorts beside, CancellationToken cancellationToken)
        {
            project.Database.Calls.Add("beside-write");
            return fails ? throw new InvalidOperationException("refused") : Task.CompletedTask;
        }
    }

    /// <summary>A query that notes when it reads beside the core.</summary>
    [RequiresRole(MemberRole.Reader)]
    private sealed class BesideQueryProbe(TestProject project) : ProjectQuery<string>(AccessTarget.Map(project.RootMapId))
    {
        private string read = "nothing";

        public override Task ReadBesideAsync(ProjectWorkspace workspace, BesidePorts beside, CancellationToken cancellationToken)
        {
            project.Database.Calls.Add("beside-read");
            read = ReferenceEquals(beside, project.Beside) ? workspace.Project.Code : "other ports";
            return Task.CompletedTask;
        }

        public override string Read(ProjectWorkspace workspace, QueryContext context) => read;
    }

    [Fact]
    public async Task A_command_reads_beside_the_core_after_the_load_and_writes_beside_it_after_its_change_set_and_before_its_log()
    {
        var project = new TestProject();
        var command = new BesideProbe(project);
        await project.Runner.RunAsync(command, Token);
        Assert.Equal(["access", "lock", "load", "beside-read", "domain", "save", "beside-write", "log"], project.Database.Calls);
        Assert.True(command.WorkspaceGiven);
        Assert.Equal(1, project.Database.Commits);
    }

    [Fact]
    public async Task A_failure_of_the_write_beside_the_core_takes_the_change_set_back()
    {
        var project = new TestProject();
        await Assert.ThrowsAsync<InvalidOperationException>(() => project.Runner.RunAsync(new BesideProbe(project, fails: true), Token));
        Assert.Single(project.Database.All<Domain.Model.Lane>());
        Assert.Empty(project.Database.Entries);
        Assert.Equal((0, 0), (project.Database.Commits, project.Project.StructureVersion));
    }

    [Fact]
    public async Task A_command_run_again_after_a_deadlock_reads_beside_the_core_again()
    {
        var project = new TestProject();
        project.Database.CommitFailures.Enqueue(new TimeoutException("deadlock"));
        var command = new BesideProbe(project);
        await project.Runner.RunAsync(command, Token);
        Assert.Equal(2, command.Reads);
        Assert.Equal(2, project.Database.All<Domain.Model.Lane>().Count());
    }

    [Fact]
    public async Task A_command_that_reads_nothing_beside_the_core_runs_as_before()
    {
        var project = new TestProject();
        await project.Runner.RunAsync(new AddLaneProbe(project.RootMapId, "Achats"), Token);
        Assert.Equal(["access", "lock", "load", "save", "log"], project.Database.Calls);
        Assert.Equal(0, project.Comments.Writes);
    }

    [Fact]
    public async Task A_query_reads_beside_the_core_after_the_load_and_before_its_projection()
    {
        var project = new TestProject();
        Assert.Equal(project.Project.Code, await project.Queries.RunAsync(new BesideQueryProbe(project), Token));
        Assert.Equal(["access", "load", "beside-read"], project.Database.Calls);
    }
}
