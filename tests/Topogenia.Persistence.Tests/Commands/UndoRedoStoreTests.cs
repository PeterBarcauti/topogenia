// UndoRedoStoreTests.cs
// Undo and redo through the template on both databases: entries, chains, refusal that clears the stacks (spec 4.6, AR-05).

using Topogenia.Application.Commands;
using Topogenia.Application.Maps;
using Topogenia.Application.OperationLog;
using Topogenia.Application.Ports;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;
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
            new SessionUser(new CurrentUser(user.Id, user.Login, user.DisplayName, "fr", user.IsAdmin)), new FixedClock(), AppSettings.Default, new VersionsRead());
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
        await RunAsync(scenario, new RenameLane(lane, "Ventes"), other);

        var refusal = await Assert.ThrowsAsync<DomainException>(() => RunAsync(scenario, new UndoCommand(map), scenario.Actor));

        Assert.Equal(ErrorCodes.OperationOutdated, refusal.Code);
        Assert.Equal(other.Id, refusal.Details["updated_by"]);
        Assert.Equal(["Ventes"], await LiveLanesAsync(scenario));
        Assert.Equal([other.Id], (await EntriesAsync(scenario)).Select(entry => entry.UserId));
    }
}
