// QueryCountTests.cs
// The number of queries of a loading or of a command does not grow with the size of what it handles (annex A 6-6, debts N+1 of the 1.x).

using Microsoft.EntityFrameworkCore;
using Topogenia.Application.Ports;
using Topogenia.Domain.Flows;
using Topogenia.Domain.Model;
using Topogenia.Domain.Projects;
using Topogenia.Domain.Selection;
using Topogenia.Domain.Steps;
using Topogenia.Domain.Structure;
using Topogenia.Domain.SubMaps;
using Topogenia.Domain.Workspace;
using Topogenia.Persistence.Loading;
using Topogenia.Persistence.Tests.Infrastructure;
using Topogenia.Persistence.Writing;

namespace Topogenia.Persistence.Tests.Queries;

/// <summary>
/// The 1.x read one level of the breadcrumb per query, one or two queries per step placed, three per flow, one per member
/// and one per step restored (annex A 6-6). In 2.0 an operation loads its workspace in a fixed number of queries (joins on
/// the project, ADR 015), applies the rule in memory and writes its change set: every case below is run on a small and a
/// large size, on both engines, and reads exactly as much; its writes are one statement per row it changes (SQLite) or
/// fewer, grouped in batches (SQL Server), never a query per element. No command of a request reads the migrations.
/// </summary>
public sealed class QueryCountTests(MigratedDatabases databases) : IClassFixture<MigratedDatabases>
{
    private const int Large = 200;

    private async Task<StoreScenario> ScenarioAsync(EngineKind kind) => await StoreScenario.CreateAsync(await databases.OfAsync(kind));

    /// <summary>A scenario whose first lane of the root map holds <paramref name="count"/> steps in the row 0, columns 0 to count - 1.</summary>
    private async Task<(StoreScenario Scenario, Guid[] Steps)> PostedAsync(EngineKind kind, int count)
    {
        var scenario = await ScenarioAsync(kind);
        var steps = new Guid[count];
        await scenario.RunAsync((workspace, context) =>
        {
            var (map, lane) = FirstLane(workspace);
            for (var i = 0; i < count; i++)
                steps[i] = StepOperations.Create(workspace, context, new CreateStepRequest(map, lane, i, $"Step {i}")).Id;
        });
        return (scenario, steps);
    }

    private static (Guid Map, Guid Lane) FirstLane(ProjectWorkspace workspace) =>
        (workspace.RootMap.Id, workspace.LanesOf(workspace.RootMap.Id)[0].Id);

    /// <summary>Depth 1 against depth 50: the root map and a chain of sub-maps, each detailed from the step of the map above.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_breadcrumb_is_read_in_the_same_number_of_queries_at_depth_one_and_at_depth_fifty(EngineKind kind)
    {
        var shallow = await ChainAsync(kind, 0);
        var deep = await ChainAsync(kind, ProjectWorkspace.MaxDepth - 1);

        var (shallowLog, shallowWorkspace) = await Counted.LoadAsync(shallow.Scenario, WorkspaceContent.None);
        var (deepLog, deepWorkspace) = await Counted.LoadAsync(deep.Scenario, WorkspaceContent.None);

        Assert.Equal(shallowLog.Reads, deepLog.Reads);
        Assert.Equal(0, shallowLog.Writes + deepLog.Writes);
        Assert.Single(SubMapRules.Breadcrumb(shallowWorkspace, shallow.Maps[^1]));
        Assert.Equal(deep.Maps, SubMapRules.Breadcrumb(deepWorkspace, deep.Maps[^1]).Select(map => map.Id));
    }

    /// <summary>The root map, then <paramref name="levels"/> sub-maps, each detailed from the only step of the map above.</summary>
    internal async Task<(StoreScenario Scenario, List<Guid> Maps)> ChainAsync(EngineKind kind, int levels)
    {
        var scenario = await ScenarioAsync(kind);
        var maps = new List<Guid>();
        await scenario.RunAsync((workspace, context) => maps.AddRange(Chains.Build(workspace, context, levels)));
        return (scenario, maps);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Placing_a_step_in_a_free_cell_sends_the_same_commands_beside_one_or_two_hundred_steps(EngineKind kind)
    {
        var runs = new List<CountedRun>();
        foreach (var count in new[] { 1, Large })
        {
            var (scenario, _) = await PostedAsync(kind, count);
            runs.Add(await Counted.RunAsync(scenario, (workspace, context) =>
            {
                var (map, lane) = FirstLane(workspace);
                StepOperations.Create(workspace, context, new CreateStepRequest(map, lane, 0, "Below", Row: 1));
            }));
        }

        Assert.Equal(runs[0].Log.Commands, runs[1].Log.Commands);
        Counted.AssertIndependentReads(runs[0], runs[1]);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Freeing_a_cell_reads_the_same_whatever_the_number_of_steps_it_shifts(EngineKind kind)
    {
        var runs = new List<CountedRun>();
        foreach (var count in new[] { 1, Large })
        {
            var (scenario, steps) = await PostedAsync(kind, count);
            var run = await Counted.RunAsync(scenario, (workspace, context) =>
            {
                var (map, lane) = FirstLane(workspace);
                StepOperations.Create(workspace, context, new CreateStepRequest(map, lane, 0, "First"));
            });
            var stored = await scenario.AssertStoredAsync(run.Outcome);
            Assert.Equal(Enumerable.Range(1, count), steps.Select(id => stored.LiveStep(id).Column));
            runs.Add(run);
        }

        Counted.AssertIndependentReads(runs[0], runs[1]);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Creating_flows_reads_the_same_for_one_or_two_hundred_flows(EngineKind kind)
    {
        var runs = new List<CountedRun>();
        foreach (var count in new[] { 1, Large })
        {
            var (scenario, steps) = await PostedAsync(kind, count + 1);
            var run = await Counted.RunAsync(scenario, (workspace, context) =>
            {
                for (var i = 0; i < count; i++)
                    FlowRules.Create(workspace, context, new CreateFlowRequest(workspace.RootMap.Id, steps[i], steps[i + 1], null));
            });
            Assert.Equal(count, (await scenario.AssertStoredAsync(run.Outcome)).FlowsOf(run.Outcome.Workspace.RootMap.Id).Count);
            runs.Add(run);
        }

        Counted.AssertIndependentReads(runs[0], runs[1]);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Replacing_the_members_reads_the_same_for_one_or_fifty_members(EngineKind kind)
    {
        var runs = new List<CountedRun>();
        foreach (var count in new[] { 1, 50 })
        {
            var scenario = await ScenarioAsync(kind);
            var users = Enumerable.Range(0, count)
                .Select(i => UserRules.NewUser(scenario.Context(), $@"FICTITIOUS\Member{i}_{Guid.CreateVersion7():N}", null, [])).ToList();
            await scenario.WriteAsync(new ChangeSet(users.Select(user => StoreScenario.Created(user)).ToList()));
            var entries = users.Select(user => new MemberEntry(user.Login, "contributor", false)).Prepend(new MemberEntry(scenario.Actor.Login, "facilitator", true)).ToList();
            var people = new PeopleLookup([scenario.Actor, .. users], new Dictionary<string, DirectoryEntry>(), []);
            var run = await Counted.RunAsync(scenario, (workspace, context) => MemberRules.Replace(workspace, context, entries, people));
            // The workspace in memory holds the users of the former members only; the members themselves must match.
            var stored = (await scenario.LoadAsync()).All<ProjectMember>().OrderBy(member => member.DisplayOrder).ToList();
            Assert.Equal(run.Outcome.Workspace.All<ProjectMember>().OrderBy(member => member.DisplayOrder), stored);
            Assert.Equal(count + 1, stored.Count);
            runs.Add(run);
        }

        Counted.AssertIndependentReads(runs[0], runs[1]);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Restoring_a_batch_by_the_undo_reads_the_same_for_one_or_two_hundred_steps(EngineKind kind)
    {
        var runs = new List<CountedRun>();
        foreach (var count in new[] { 1, Large })
        {
            var (scenario, steps) = await PostedAsync(kind, count);
            var deletion = await scenario.RunAsync((workspace, context) =>
                GroupDeletion.Delete(workspace, context, workspace.RootMap.Id, new GroupDeletionRequest(steps, null)));
            var run = await Counted.RunAsync(scenario, (workspace, context) => UndoReplay.Undo(workspace, context, deletion.Changes));
            var stored = await scenario.AssertStoredAsync(run.Outcome);
            Assert.All(steps, id => Assert.Null(stored.Step(id)!.DeletedAt));
            Assert.NotNull(Assert.Single(stored.All<DeletionBatch>()).RestoredAt);
            runs.Add(run);
        }

        Counted.AssertIndependentReads(runs[0], runs[1]);
    }

    /// <summary>
    /// A-6-25, part of the opening of a `.pmap` (P5-T04): the project is built in memory by the rules of the domain, as
    /// `PmapBuild` builds it (creation, then its records put in a workspace and committed), and written in one change set,
    /// the project first. Of 1 or 200 steps, the writing reads nothing and sends one statement per row at most; the
    /// application reads the codes of the project once before (`PmapCreation.WriteAsync`), whatever the size of the file.
    /// </summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Writing_a_project_opened_from_a_file_reads_nothing_for_one_or_two_hundred_steps(EngineKind kind)
    {
        foreach (var (count, code) in new[] { (1, "QCUN"), (Large, "QCDEUX") })
        {
            var scenario = await ScenarioAsync(kind);
            var context = scenario.Context();
            var created = ProjectCreation.Create(context, new CreateProjectRequest(code, "Ouverture"),
                new ProjectCreationInputs([], StoreScenario.Languages, "fr", new PeopleLookup([scenario.Actor], new Dictionary<string, DirectoryEntry>(), [])));
            var workspace = new ProjectWorkspace(created.Project.Id, [created.Project]);
            foreach (var record in created.Records.Skip(1))
                workspace.Put(record);
            for (var i = 0; i < count; i++)
                StepOperations.Create(workspace, context, new CreateStepRequest(created.RootMap.Id, created.FirstLane.Id, i, $"Step {i}"));
            var changes = new ChangeSet([new RecordChange(ChangeKind.Created, null, created.Project), .. workspace.Commit(context).Changes]);
            var log = new CommandLog();

            await using (var session = scenario.Database.Session(null, log))
            {
                await new UnitOfWork(session).RunAsync(async token =>
                {
                    await new WorkspaceStore(session).SaveAsync(changes, token);
                    return true;
                }, TestContext.Current.CancellationToken);
            }

            Assert.Equal((0, 0), (log.Reads, log.Migrations));
            Assert.InRange(log.Writes, 1, CountedRun.Statements(changes));
            await using var reading = scenario.Database.Session();
            var stored = await new WorkspaceStore(reading).LoadAsync(created.Project.Id, WorkspaceContent.None, TestContext.Current.CancellationToken);
            Assert.Equal(count, stored!.All<Step>().Count());
        }
    }

    /// <summary>
    /// The expected schema is the list of migrations compiled with the application, checked once at the start
    /// (`ISchemaMigrations.CheckAsync`, by the host): no loading and no writing of a request reads the migrations or their
    /// history again (the 1.x `VersionAttendue` read its resources at each call). The counter does see such a read.
    /// </summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task No_command_of_a_request_reads_the_migrations_or_their_history(EngineKind kind)
    {
        var (scenario, steps) = await PostedAsync(kind, 2);

        var run = await Counted.RunAsync(scenario, (workspace, context) =>
            FlowRules.Create(workspace, context, new CreateFlowRequest(workspace.RootMap.Id, steps[0], steps[1], null)));
        var (loading, _) = await Counted.LoadAsync(scenario, WorkspaceContent.All);

        Assert.Equal(0, run.Log.Migrations + loading.Migrations);
        Assert.True(run.Log.Reads > 0 && run.Log.Writes > 0 && loading.Reads > 0);
        var control = new CommandLog();
        await using var context = scenario.Database.Engine.CreateContext(control);
        await context.Database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken);
        Assert.True(control.Migrations > 0);
    }
}
