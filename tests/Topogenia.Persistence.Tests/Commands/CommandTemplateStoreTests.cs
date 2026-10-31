// CommandTemplateStoreTests.cs
// The command template on both databases: access, lock, If-Match, writing, operation log and its stack (spec 3.4, 4.6, 4.7).

using Topogenia.Application.Authorization;
using Topogenia.Application.Commands;
using Topogenia.Application.Maps;
using Topogenia.Application.OperationLog;
using Topogenia.Application.Ports;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Lanes;
using Topogenia.Domain.Model;
using Topogenia.Domain.Structure;
using Topogenia.Domain.Trash;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;
using Topogenia.Persistence.Loading;
using Topogenia.Persistence.Tests.Infrastructure;
using Topogenia.Persistence.Writing;

namespace Topogenia.Persistence.Tests.Commands;

[RequiresRole(MemberRole.Facilitator)]
internal sealed class AddLane(Guid mapId, string name) : ProjectCommand<LaneResult>(AccessTarget.Map(mapId))
{
    public override string? LogKind => "lane.add";

    public override Guid? MapOf(ProjectWorkspace workspace) => Target.Id;

    public override LaneResult Execute(ProjectWorkspace workspace, OperationContext context) =>
        LaneOperations.Add(workspace, context, new AddLaneRequest(Target.Id, name));
}

[RequiresRole(MemberRole.Facilitator)]
internal sealed class RenameLane(Guid laneId, string name) : ProjectCommand<LaneResult>(AccessTarget.Lane(laneId))
{
    public override string? LogKind => "lane.update";

    public override Guid? MapOf(ProjectWorkspace workspace) => workspace.LiveLane(Target.Id).MapId;

    public override LaneResult Execute(ProjectWorkspace workspace, OperationContext context) =>
        LaneOperations.Update(workspace, context, new UpdateLaneRequest(Target.Id) { Name = new FieldChange<string?>(name) });
}

internal sealed class SessionUser(CurrentUser user) : ICurrentUser
{
    public CurrentUser? User { get; } = user;
}

public sealed class CommandTemplateStoreTests(MigratedDatabases databases) : IClassFixture<MigratedDatabases>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static CurrentUser UserOf(AppUser user) => new(user.Id, user.Login, user.DisplayName, "fr", user.IsAdmin);

    /// <summary>Runs a command through the template with the adapters of the persistence, in a session of its own.</summary>
    private static async Task<CommandOutcome<T>> RunAsync<T>(StoreScenario scenario, ProjectCommand<T> command, AppUser? user = null)
    {
        await using var session = scenario.Database.Session();
        var runner = new CommandRunner(new UnitOfWork(session), new AccessReader(session), new WorkspaceStore(session),
            new OperationLogStore(session), new MapViewBuilder(AppSettings.Default), new SessionUser(UserOf(user ?? scenario.Actor)),
            new FixedClock(), AppSettings.Default, new VersionsRead());
        return await runner.RunAsync(command, Token);
    }

    private static async Task<List<OperationLogEntry>> EntriesAsync(StoreScenario scenario)
    {
        await using var context = scenario.Database.Context();
        return [.. context.Set<OperationLogEntry>().Where(entry => entry.ProjectId == scenario.ProjectId).OrderBy(entry => entry.Sequence)];
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_command_is_written_logged_and_answered_with_its_view(EngineKind kind)
    {
        var scenario = await StoreScenario.CreateAsync(await databases.OfAsync(kind));
        var map = (await scenario.LoadAsync()).RootMap.Id;
        var outcome = await RunAsync(scenario, new AddLane(map, "Achats"));
        var workspace = await scenario.LoadAsync();
        Assert.Equal("Achats", workspace.Lane(outcome.Result.Id)!.Name);
        Assert.Equal(1, workspace.Project.StructureVersion);
        Assert.Equal((1, 2), (outcome.StructureVersion, outcome.View!.Lanes.Count));
        var entry = Assert.Single(await EntriesAsync(scenario));
        Assert.Equal((outcome.OperationId, map, scenario.Actor.Id, "lane.add", 0), ((Guid?)entry.Id, entry.MapId, entry.UserId, entry.Kind, entry.Sequence));
        Assert.Equal(workspace.Lane(outcome.Result.Id), ChangeSetJson.Read(entry.ForwardJson).Created<Lane>().Single());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_non_member_is_not_found_and_nothing_is_locked_or_written(EngineKind kind)
    {
        var scenario = await StoreScenario.CreateAsync(await databases.OfAsync(kind));
        var map = (await scenario.LoadAsync()).RootMap.Id;
        var refusal = await Assert.ThrowsAsync<DomainException>(() => RunAsync(scenario, new AddLane(map, "Achats"), scenario.Other));
        Assert.Equal(ErrorCodes.NotFound, refusal.Code);
        var workspace = await scenario.LoadAsync();
        Assert.Equal(0, workspace.Project.StructureVersion);
        Assert.Single(workspace.LanesOf(map));
        Assert.Empty(await EntriesAsync(scenario));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_stale_if_match_writes_nothing_and_names_the_author_of_the_change(EngineKind kind)
    {
        var scenario = await StoreScenario.CreateAsync(await databases.OfAsync(kind));
        var lane = (await scenario.LoadAsync()).LanesOf((await scenario.LoadAsync()).RootMap.Id)[0];
        await RunAsync(scenario, new RenameLane(lane.Id, "Achats"));
        var stale = new RenameLane(lane.Id, "Ventes") { Versions = [VersionCheck.Of<Lane>(Lane.KeyOf(lane.Id), lane.Version)] };
        var refusal = await Assert.ThrowsAsync<DomainException>(() => RunAsync(scenario, stale));
        Assert.Equal(ErrorCodes.VersionConflict, refusal.Code);
        Assert.Equal(scenario.Actor.Id, refusal.Details["updated_by"]);
        Assert.Equal("Achats", (await scenario.LoadAsync()).Lane(lane.Id)!.Name);
        Assert.Single(await EntriesAsync(scenario));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_redo_stack_and_the_chains_beyond_the_depth_are_removed_under_the_foreign_key(EngineKind kind)
    {
        var scenario = await StoreScenario.CreateAsync(await databases.OfAsync(kind));
        var map = (await scenario.LoadAsync()).RootMap.Id;
        OperationLogEntry Entry(Guid id, int sequence, bool undone, Guid? redoneFrom = null) =>
            new(id, scenario.ProjectId, map, scenario.Actor.Id, "lane.add", sequence, "[]", "[]", "[]", FixedClock.Time, undone ? FixedClock.Time : null, redoneFrom);
        var oldest = Guid.CreateVersion7();
        var redoOfOldest = Guid.CreateVersion7();
        var undone = Guid.CreateVersion7();
        var redoOfUndone = Guid.CreateVersion7();
        var entries = new List<OperationLogEntry> { Entry(oldest, 0, undone: true), Entry(redoOfOldest, 1, undone: false, redoneFrom: oldest) };
        entries.AddRange(Enumerable.Range(0, OperationStack.Depth - 1).Select(index => Entry(Guid.CreateVersion7(), index + 2, undone: false)));
        entries.Add(Entry(undone, 60, undone: true));
        entries.Add(Entry(redoOfUndone, 61, undone: true, redoneFrom: undone));
        await scenario.WriteAsync(new ChangeSet(entries.Select(StoreScenario.Created).ToList()));

        var outcome = await RunAsync(scenario, new AddLane(map, "Achats"));

        var left = await EntriesAsync(scenario);
        Assert.Equal(OperationStack.Depth, left.Count);
        Assert.DoesNotContain(left, entry => new[] { oldest, redoOfOldest, undone, redoOfUndone }.Contains(entry.Id));
        // The sequence is taken after the removals: one more than the highest sequence left on the map.
        Assert.Equal((outcome.OperationId, left[^2].Sequence + 1), ((Guid?)left[^1].Id, left[^1].Sequence));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_access_of_a_deleted_object_names_its_project_and_the_trash_is_reported(EngineKind kind)
    {
        var scenario = await StoreScenario.CreateAsync(await databases.OfAsync(kind));
        var workspace = await scenario.LoadAsync();
        var lane = (await RunAsync(scenario, new AddLane(workspace.RootMap.Id, "Achats"))).Result.Id;
        await scenario.RunAsync((current, context) => LaneOperations.Delete(current, context, new DeleteLaneRequest(lane)));
        await using var session = scenario.Database.Session();
        var reader = new AccessReader(session);
        Assert.Equal(new ProjectAccess(scenario.ProjectId, false, MemberRole.Facilitator),
            await reader.FindAsync(AccessTarget.Lane(lane), scenario.Actor.Id, Token));
        Assert.Equal(new ProjectAccess(scenario.ProjectId, false, null), await reader.FindAsync(AccessTarget.Map(workspace.RootMap.Id), scenario.Other.Id, Token));
        Assert.Null(await reader.FindAsync(AccessTarget.Step(Guid.NewGuid()), scenario.Actor.Id, Token));
        await scenario.RunAsync((current, context) => TrashRules.MoveToTrash(current, context, current.Project.Code));
        Assert.True((await reader.FindAsync(AccessTarget.Project(scenario.ProjectId), scenario.Actor.Id, Token))!.InTrash);
    }
}
