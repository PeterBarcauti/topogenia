// UndoRedoStoreTests.cs
// Undo and redo through the template on both databases: entries, chains, refusal that clears the stacks, removal of the entries of a project under its lock (spec 4.6, AR-05).

using Topogenia.Application.Commands;
using Topogenia.Application.Maps;
using Topogenia.Application.OperationLog;
using Topogenia.Application.Ports;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;
using Topogenia.Persistence.Errors;
using Topogenia.Persistence.Loading;
using Topogenia.Persistence.Tests.Infrastructure;
using Topogenia.Persistence.Writing;

namespace Topogenia.Persistence.Tests.Commands;

public sealed class UndoRedoStoreTests(MigratedDatabases databases) : IClassFixture<MigratedDatabases>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<CommandOutcome<T>> RunAsync<T>(StoreScenario scenario, ProjectCommand<T> command, AppUser user)
    {
        await using var session = scenario.Database.Session();
        var runner = new CommandRunner(new UnitOfWork(session), new AccessReader(session), new WorkspaceStore(session),
            new OperationLogStore(session), new MapViewBuilder(AppSettings.Default),
            new SessionUser(new CurrentUser(user.Id, user.Login, user.DisplayName, "fr", user.IsAdmin)), new FixedClock(), AppSettings.Default, new VersionsRead(),
            new Topogenia.Application.Presentation.BesidePorts(new Topogenia.Persistence.Presentation.PresentationCommentStore(session),
                new Topogenia.Persistence.Presentation.FlowPresentationCommentStore(session)));
        return await runner.RunAsync(command, Token);
    }

    private static async Task<List<OperationLogEntry>> EntriesAsync(StoreScenario scenario)
    {
        await using var context = scenario.Database.Context();
        return [.. context.Set<OperationLogEntry>().Where(entry => entry.ProjectId == scenario.ProjectId).OrderBy(entry => entry.Sequence)];
    }

    private static async Task<List<string>> LiveLanesAsync(StoreScenario scenario)
    {
        var workspace = await scenario.LoadAsync();
        return [.. workspace.LanesOf(workspace.RootMap.Id).Select(lane => lane.Name)];
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task An_operation_is_undone_redone_and_undone_again_through_its_chain(EngineKind kind)
    {
        var scenario = await StoreScenario.CreateAsync(await databases.OfAsync(kind));
        var map = (await scenario.LoadAsync()).RootMap.Id;
        var first = (await LiveLanesAsync(scenario)).Single();
        var done = await RunAsync(scenario, new AddLane(map, "Achats"), scenario.Actor);

        await RunAsync(scenario, new UndoCommand(map), scenario.Actor);
        Assert.Equal([first], await LiveLanesAsync(scenario));
        await RunAsync(scenario, new RedoCommand(map), scenario.Actor);
        Assert.Equal([first, "Achats"], await LiveLanesAsync(scenario));
        await RunAsync(scenario, new UndoCommand(map), scenario.Actor);
        Assert.Equal([first], await LiveLanesAsync(scenario));

        var entries = await EntriesAsync(scenario);
        Assert.Equal([done.OperationId!.Value, entries[1].Id], entries.Select(entry => entry.Id));
        Assert.Equal([null, done.OperationId], entries.Select(entry => entry.RedoneFromId));
        Assert.All(entries, entry => Assert.NotNull(entry.UndoneAt));
        Assert.All(entries, entry => Assert.NotEqual(OperationRecorder.NotUndoneYet, entry.InverseJson));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_refused_undo_writes_nothing_of_the_replay_and_clears_the_whole_chain_of_the_user(EngineKind kind)
    {
        var scenario = await StoreScenario.CreateAsync(await databases.OfAsync(kind));
        var other = await scenario.NewUserAsync("Second");
        await scenario.SetMembersAsync(other);
        await scenario.RunAsync((current, _) =>
        {
            var member = current.All<ProjectMember>().Single(row => row.UserId == other.Id);
            current.Put(member with { Role = Domain.Values.MemberRole.Facilitator });
        });
        var workspace = await scenario.LoadAsync();
        var map = workspace.RootMap.Id;
        var lane = workspace.LanesOf(map)[0].Id;
        await RunAsync(scenario, new RenameLane(lane, "Achats"), scenario.Actor);
        await RunAsync(scenario, new UndoCommand(map), scenario.Actor);
        await RunAsync(scenario, new RedoCommand(map), scenario.Actor);
        await RunAsync(scenario, new RenameLane(lane, "Visites"), other);

        var refusal = await Assert.ThrowsAsync<DomainException>(() => RunAsync(scenario, new UndoCommand(map), scenario.Actor));

        Assert.Equal(ErrorCodes.OperationOutdated, refusal.Code);
        Assert.Equal(other.Id, refusal.Details["updated_by"]);
        Assert.Equal(["Visites"], await LiveLanesAsync(scenario));
        Assert.Equal([other.Id], (await EntriesAsync(scenario)).Select(entry => entry.UserId));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_entries_of_a_project_are_removed_together_with_their_chains_and_those_of_another_project_stay(EngineKind kind)
    {
        var database = await databases.OfAsync(kind);
        var scenario = await StoreScenario.CreateAsync(database);
        var other = await StoreScenario.CreateAsync(database);
        var map = (await scenario.LoadAsync()).RootMap.Id;
        // An operation undone then redone: two entries, the second redone from the first.
        await RunAsync(scenario, new AddLane(map, "Accueil"), scenario.Actor);
        await RunAsync(scenario, new UndoCommand(map), scenario.Actor);
        await RunAsync(scenario, new RedoCommand(map), scenario.Actor);
        await RunAsync(other, new AddLane((await other.LoadAsync()).RootMap.Id, "Accueil"), other.Actor);
        Assert.Equal((2, 1), ((await EntriesAsync(scenario)).Count, (await EntriesAsync(other)).Count));

        // As the template does: the entries of a project are written under its lock.
        var commands = new CommandLog();
        var removed = await RemoveAsync(database, scenario.ProjectId, scenario.ProjectId, commands);

        Assert.Equal(2, removed);
        // One statement, nothing read before it: the log of a large project is never loaded to be removed (audit 3.2.0).
        Assert.Equal((0, 1), (commands.Reads, commands.Commands.Count));
        Assert.Equal((0, 1), ((await EntriesAsync(scenario)).Count, (await EntriesAsync(other)).Count));
        // Nothing is left to undo on the map of the first project; the lane redone stays.
        Assert.Equal(ErrorCodes.NotFound, (await Assert.ThrowsAsync<DomainException>(() => RunAsync(scenario, new UndoCommand(map), scenario.Actor))).Code);
        Assert.Contains("Accueil", await LiveLanesAsync(scenario));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_entries_of_a_project_are_removed_only_under_its_lock_and_otherwise_nothing_is_removed(EngineKind kind)
    {
        var database = await databases.OfAsync(kind);
        var scenario = await StoreScenario.CreateAsync(database);
        var other = await StoreScenario.CreateAsync(database);
        var empty = await StoreScenario.CreateAsync(database);
        await RunAsync(scenario, new AddLane((await scenario.LoadAsync()).RootMap.Id, "Accueil"), scenario.Actor);

        // Without any lock, under the lock of another project only, and even for a project without any entry.
        await Assert.ThrowsAsync<UnlockedWriteException>(() => RemoveAsync(database, null, scenario.ProjectId));
        await Assert.ThrowsAsync<UnlockedWriteException>(() => RemoveAsync(database, other.ProjectId, scenario.ProjectId));
        await Assert.ThrowsAsync<UnlockedWriteException>(() => RemoveAsync(database, null, empty.ProjectId));

        Assert.Single(await EntriesAsync(scenario));
    }

    /// <summary>Removes the entries of a project in a unit that holds the lock of <paramref name="locked"/>, or no lock.</summary>
    private static async Task<int> RemoveAsync(TestDatabase database, Guid? locked, Guid projectId, CommandLog? commands = null)
    {
        await using var session = commands is null ? database.Session() : database.Session(null, commands);
        var unit = new UnitOfWork(session);
        var operations = new OperationLogStore(session);
        return await unit.RunAsync(async token =>
        {
            if (locked is { } id)
                await unit.LockProjectAsync(id, token);
            commands?.Clear();
            return await operations.RemoveOfProjectAsync(projectId, token);
        }, Token);
    }
}
