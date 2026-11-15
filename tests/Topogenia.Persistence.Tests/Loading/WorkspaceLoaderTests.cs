// WorkspaceLoaderTests.cs
// The loading of a workspace: every record of the project and nothing of another, the structure and every counter always, groups on demand.

using Topogenia.Application.Ports;
using Topogenia.Domain.Model;
using Topogenia.Domain.Steps;
using Topogenia.Domain.Values;
using Topogenia.Persistence.Loading;
using Topogenia.Persistence.Tests.Infrastructure;
using Topogenia.Persistence.Writing;

namespace Topogenia.Persistence.Tests.Loading;

/// <summary>
/// ADR 015, spec 3.4, architecture.md 3: the workspace of a project holds its structure (live and deleted), every counter,
/// and the groups of <see cref="WorkspaceContent"/> asked; nothing of another project, neither the operation log nor the
/// audit. Two complete fictitious projects share the database.
/// </summary>
public sealed class WorkspaceLoaderTests(MigratedDatabases databases) : IClassFixture<MigratedDatabases>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static readonly Type[] Structure =
    [
        typeof(Project), typeof(ProjectCounter), typeof(DeletionBatch), typeof(ProcessMap), typeof(Lane), typeof(Step), typeof(Flow),
        typeof(StepRaci), typeof(Dissolution), typeof(DissolutionStep), typeof(DissolutionObject),
    ];

    private static readonly Dictionary<WorkspaceContent, Type[]> Groups = new()
    {
        [WorkspaceContent.Sheets] = [typeof(StepSheet), typeof(SheetOperation), typeof(StepSheetVersion)],
        [WorkspaceContent.Documents] = [typeof(Document), typeof(DocumentUse), typeof(PainPoint), typeof(PainPointStatusChange)],
        [WorkspaceContent.Translations] = [typeof(Translation)],
        [WorkspaceContent.Members] = [typeof(ProjectMember), typeof(AppUser)],
    };

    public static TheoryData<EngineKind, WorkspaceContent> EveryContent()
    {
        var data = new TheoryData<EngineKind, WorkspaceContent>();
        foreach (var kind in Enum.GetValues<EngineKind>())
        {
            foreach (var content in new[] { WorkspaceContent.None, WorkspaceContent.Sheets, WorkspaceContent.Documents, WorkspaceContent.Translations,
                WorkspaceContent.Members, WorkspaceContent.Sheets | WorkspaceContent.Members, WorkspaceContent.All })
                data.Add(kind, content);
        }
        return data;
    }

    /// <summary>Two complete projects written in the database; the first is the one loaded.</summary>
    private async Task<(TestDatabase Database, ProjectGraph Graph)> TwoProjectsAsync(EngineKind kind)
    {
        var database = await databases.OfAsync(kind);
        var (graph, neighbour) = (ProjectGraph.New(), ProjectGraph.New());
        await Refusals.SaveAsync(database, [.. graph.Records, .. neighbour.Records]);
        return (database, graph);
    }

    private static async Task<Topogenia.Domain.Workspace.ProjectWorkspace?> LoadAsync(TestDatabase database, Guid projectId, WorkspaceContent content)
    {
        await using var session = database.Session();
        return await new WorkspaceStore(session).LoadAsync(projectId, content, Token);
    }

    [Theory]
    [MemberData(nameof(EveryContent))]
    public async Task The_workspace_holds_the_records_of_the_project_of_the_asked_groups_and_nothing_else(EngineKind kind, WorkspaceContent content)
    {
        var (database, graph) = await TwoProjectsAsync(kind);

        var workspace = await LoadAsync(database, graph.Project.Id, content);

        var types = Structure.Concat(Groups.Where(group => content.HasFlag(group.Key)).SelectMany(group => group.Value)).ToHashSet();
        var expected = graph.Records.OfType<IRecord>().Where(record => types.Contains(record.GetType())).ToDictionary(record => record.Key);
        var loaded = WorkspaceRecords.Of(workspace!);
        Assert.Equal(WorkspaceRecords.Sorted(expected.Keys), WorkspaceRecords.Sorted(loaded.Keys));
        Assert.All(expected, pair => Assert.Equal(pair.Value, loaded[pair.Key]));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_project_that_does_not_exist_gives_no_workspace(EngineKind kind)
    {
        var (database, _) = await TwoProjectsAsync(kind);

        Assert.Null(await LoadAsync(database, Guid.CreateVersion7(), WorkspaceContent.All));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_workspace_loads_every_counter_of_the_project_and_only_them(EngineKind kind)
    {
        var (database, graph) = await TwoProjectsAsync(kind);

        var counters = (await LoadAsync(database, graph.Project.Id, WorkspaceContent.None))!.All<ProjectCounter>().ToList();

        Assert.Equal(graph.Of<ProjectCounter>().OrderBy(counter => counter.Counter), counters.OrderBy(counter => counter.Counter));
        Assert.Equal(Enum.GetValues<CounterKind>().AsEnumerable(), counters.Select(counter => counter.Counter).Order());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Inside_a_unit_of_work_the_loading_and_the_counters_see_what_the_unit_wrote(EngineKind kind)
    {
        var scenario = await StoreScenario.CreateAsync(await databases.OfAsync(kind));
        await using var session = scenario.Database.Session();
        var unit = new UnitOfWork(session);
        var store = new WorkspaceStore(session);
        await unit.BeginAsync(Token);
        await unit.LockProjectAsync(scenario.ProjectId, Token);
        var workspace = (await store.LoadAsync(scenario.ProjectId, WorkspaceContent.None, Token))!;
        var context = scenario.Context();
        StepOperations.Create(workspace, context, new CreateStepRequest(workspace.RootMap.Id, workspace.LanesOf(workspace.RootMap.Id)[0].Id, 0, "Seen"));
        await store.SaveAsync(workspace.Commit(context), Token);

        var again = (await store.LoadAsync(scenario.ProjectId, WorkspaceContent.None, Token))!;
        var counters = again.All<ProjectCounter>().ToList();
        await unit.DisposeAsync();

        Assert.Equal("Seen", Assert.Single(again.All<Step>()).Label);
        Assert.Equal(1, again.Project.StructureVersion);
        Assert.Equal(1, counters.Single(counter => counter.Counter == CounterKind.Step).LastValue);
        Assert.Empty((await scenario.LoadAsync()).All<Step>());
    }
}
