// ProjectRoundTripTests.cs
// Load, operate, save, load again for the project, its members and its audit, on both engines.

using Topogenia.Application.Ports;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Lanes;
using Topogenia.Domain.Model;
using Topogenia.Domain.Projects;
using Topogenia.Domain.Steps;
using Topogenia.Domain.SubMaps;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;
using Topogenia.Persistence.Loading;
using Topogenia.Persistence.Tests.Infrastructure;
using Topogenia.Persistence.Writing;

namespace Topogenia.Persistence.Tests.RoundTrips;

/// <summary>
/// The part of the persistence of rules of the domain on the project (annex A 2.1, 6, spec 7.1, D06): what the operation
/// left in memory is what the database holds once read again, or nothing at all when the unit fails.
/// </summary>
public sealed class ProjectRoundTripTests(MigratedDatabases databases) : IClassFixture<MigratedDatabases>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<StoreScenario> ScenarioAsync(EngineKind kind) => await StoreScenario.CreateAsync(await databases.OfAsync(kind));

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_person_who_leaves_the_project_holds_no_lane_nor_step_anywhere_in_the_database(EngineKind kind)
    {
        var scenario = await ScenarioAsync(kind);
        await scenario.SetMembersAsync(scenario.Other);
        var steps = await scenario.LinkedStepsAsync(2);
        await scenario.RunAsync((workspace, context) =>
        {
            var root = workspace.RootMap.Id;
            LaneOperations.Add(workspace, context, new AddLaneRequest(root, "Buyer", "person", ActorUserId: scenario.Other.Id));
            StepOperations.Update(workspace, context, new UpdateStepRequest(steps[0]) { AssigneeUserId = new(scenario.Other.Id) });
            var detail = SubMapRules.Detail(workspace, context, steps[1]);
            LaneOperations.Add(workspace, context, new AddLaneRequest(detail.MapId, "Detail buyer", "person", ActorUserId: scenario.Other.Id));
            var inner = StepOperations.Create(workspace, context, new CreateStepRequest(detail.MapId, detail.FirstLaneId!.Value, 0, "Inner"));
            StepOperations.Update(workspace, context, new UpdateStepRequest(inner.Id) { AssigneeUserId = new(scenario.Other.Id) });
        });

        var leaving = await scenario.SetMembersAsync();

        // The loading reads the users of the members only: the leaver is no longer among them.
        var stored = await scenario.LoadAsync();
        Assert.Null(stored.FirstOutdated(leaving.Changes.Touched));
        Assert.DoesNotContain(stored.All<AppUser>(), user => user.Id == scenario.Other.Id);
        Assert.DoesNotContain(stored.All<ProjectMember>(), member => member.UserId == scenario.Other.Id);
        Assert.DoesNotContain(stored.All<Lane>(), lane => lane.ActorUserId == scenario.Other.Id);
        Assert.DoesNotContain(stored.All<Step>(), step => step.AssigneeUserId == scenario.Other.Id);
        Assert.Equal(2, stored.All<ProcessMap>().Count());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task An_invited_user_is_created_in_the_unit_of_the_members_and_not_at_all_when_the_unit_fails(EngineKind kind)
    {
        var scenario = await ScenarioAsync(kind);
        var taken = (await scenario.LoadAsync(WorkspaceContent.None)).Project.Code;
        var guest = $@"FICTITIOUS\Invited_{Guid.CreateVersion7():N}";

        // The code is taken since the handler read the codes: the database refuses the project, the guest goes with it.
        var refusal = await Assert.ThrowsAsync<DomainException>(() => CreateWithGuestAsync(scenario, taken, guest));
        Assert.Equal((ErrorCodes.UniqueConflict, (object?)"UQ_project_code"), (refusal.Code, refusal.Details["constraint"]));
        Assert.Empty(await scenario.Database.UsersAsync(Login.Normalize(guest)));

        var project = await CreateWithGuestAsync(scenario, ScenarioSteps.NewProjectCode(), guest);
        var user = Assert.Single(await scenario.Database.UsersAsync(Login.Normalize(guest)));
        await using var session = scenario.Database.Session();
        var created = (await new WorkspaceStore(session).LoadAsync(project, WorkspaceContent.Members, Token))!;
        Assert.Contains(created.All<ProjectMember>(), member => member.UserId == user.Id);
    }

    private static async Task<Guid> CreateWithGuestAsync(StoreScenario scenario, string code, string guest)
    {
        await using var session = scenario.Database.Session();
        var unit = new UnitOfWork(session);
        var store = new WorkspaceStore(session);
        return await unit.RunAsync(async token =>
        {
            var created = ProjectCreation.Create(scenario.Context(),
                new CreateProjectRequest(code, "With a guest", Members: [new MemberEntry(guest, "contributor", false)]),
                new ProjectCreationInputs([], StoreScenario.Languages, "fr", new PeopleLookup([scenario.Actor], new Dictionary<string, DirectoryEntry>(), [])));
            Assert.Single(created.NewUsers);
            await store.SaveAsync(new ChangeSet([.. created.NewUsers.Select(StoreScenario.Created), .. created.Records.Select(StoreScenario.Created)]), token);
            return created.Project.Id;
        }, Token);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Changing_only_the_assignee_of_a_step_writes_its_update_audit_and_its_next_version(EngineKind kind)
    {
        var scenario = await ScenarioAsync(kind);
        await scenario.SetMembersAsync(scenario.Other);
        var step = (await scenario.LinkedStepsAsync(1))[0];
        var before = (await scenario.LoadAsync()).LiveStep(step);

        var assigned = await scenario.RunAsync((workspace, context) =>
            StepOperations.Update(workspace, context, new UpdateStepRequest(step) { AssigneeUserId = new(scenario.Other.Id) }), scenario.Other);

        var stored = (await scenario.AssertStoredAsync(assigned)).LiveStep(step);
        Assert.Equal((scenario.Other.Id, before.Version + 1, (Guid?)scenario.Other.Id), (stored.AssigneeUserId!.Value, stored.Version, stored.UpdatedBy));
        Assert.True(stored.UpdatedAt > before.CreatedAt);
        Assert.Equal(before with { AssigneeUserId = scenario.Other.Id }, stored with { Version = before.Version, UpdatedAt = null, UpdatedBy = null });
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_version_is_an_integer_moved_on_by_one_at_each_change_and_read_back_as_written(EngineKind kind)
    {
        var scenario = await ScenarioAsync(kind);
        var step = (await scenario.LinkedStepsAsync(1))[0];
        var versions = new List<int> { (await scenario.LoadAsync()).LiveStep(step).Version };

        for (var i = 1; i <= 3; i++)
        {
            var label = $"Label {i}";
            await scenario.RunAsync((workspace, context) => StepOperations.Update(workspace, context, new UpdateStepRequest(step) { Label = new(label) }));
            versions.Add((await scenario.LoadAsync()).LiveStep(step).Version);
        }

        Assert.Equal(new[] { 1, 2, 3, 4 }, versions);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task One_database_holds_several_projects_and_each_loading_reads_only_its_own(EngineKind kind)
    {
        var database = await databases.OfAsync(kind);
        var scenarios = new[] { await StoreScenario.CreateAsync(database), await StoreScenario.CreateAsync(database), await StoreScenario.CreateAsync(database) };
        foreach (var scenario in scenarios)
            await scenario.LinkedStepsAsync(2);

        foreach (var scenario in scenarios)
        {
            var stored = await scenario.LoadAsync();
            var code = stored.Project.Code;
            Assert.All(stored.All<ProcessMap>(), map => Assert.Equal(scenario.ProjectId, map.ProjectId));
            Assert.All(stored.All<ProjectCounter>(), counter => Assert.Equal(scenario.ProjectId, counter.ProjectId));
            Assert.Equal(new[] { $"{code}-E001", $"{code}-E002" }, stored.All<Step>().Select(step => step.Code).Order());
            Assert.Single(stored.All<Flow>());
            Assert.Equal(scenario.Actor.Id, Assert.Single(stored.All<ProjectMember>()).UserId);
        }
        Assert.Equal(3, scenarios.Select(scenario => scenario.ProjectId).Distinct().Count());
    }
}
