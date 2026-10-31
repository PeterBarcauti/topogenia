// ReadingStoreTests.cs
// The reads across the projects and the reads of the users on both databases: visibility, counts, trash, codes, logins, profile (spec 3.4).

using Topogenia.Application.Ports;
using Topogenia.Domain.Projects;
using Topogenia.Domain.Translations;
using Topogenia.Domain.Trash;
using Topogenia.Domain.Values;
using Topogenia.Persistence.Reading;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Persistence.Tests.Reading;

public sealed class ReadingStoreTests(MigratedDatabases databases) : IClassFixture<MigratedDatabases>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// Two projects of two scenarios: the first with two live steps out of three and an English name, the second in the trash,
    /// where the actor of the first is a contributor.
    /// </summary>
    private async Task<(StoreScenario First, StoreScenario Second)> TwoProjectsAsync(EngineKind kind)
    {
        var database = await databases.OfAsync(kind);
        var first = await StoreScenario.CreateAsync(database);
        var second = await StoreScenario.CreateAsync(database);
        var steps = await first.LinkedStepsAsync(3);
        await first.RunAsync((workspace, context) =>
        {
            workspace.SoftDelete(workspace.Step(steps[2])!, context);
            TranslationRules.WriteManual(workspace, context, TranslationObject.Project, workspace.ProjectId,
                new Dictionary<string, string?> { ["en"] = "Sales" }, StoreScenario.Languages);
        });
        await second.LinkedStepsAsync(1);
        await second.SetMembersAsync(first.Actor);
        await second.RunAsync((workspace, context) => TrashRules.MoveToTrash(workspace, context, workspace.Project.Code), second.Other);
        return (first, second);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_member_sees_his_live_projects_with_his_role_the_counts_and_the_translations_and_an_administrator_every_one(EngineKind kind)
    {
        var (first, second) = await TwoProjectsAsync(kind);
        await using var session = first.Database.Session();
        var reader = new ProjectReader(session);
        var mine = (await reader.LiveProjectsAsync(new ProjectVisibility(first.Actor.Id, false), Token))
            .Where(row => row.Project.Id == first.ProjectId || row.Project.Id == second.ProjectId).ToList();
        var row = Assert.Single(mine);
        var root = (await first.LoadAsync()).RootMap.Id;
        Assert.Equal((first.ProjectId, root, (MemberRole?)MemberRole.Facilitator, 2, 1), (row.Project.Id, row.RootMapId, row.Role, row.StepCount, row.MapCount));
        Assert.Equal(new Dictionary<string, string> { ["en"] = "Sales" }, row.NameTranslations);
        var every = await reader.LiveProjectsAsync(new ProjectVisibility(second.Actor.Id, true), Token);
        Assert.Contains(every, other => other.Project.Id == first.ProjectId && other.Role is null);
        Assert.DoesNotContain(every, other => other.Project.Id == second.ProjectId);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_trash_shows_the_projects_the_user_facilitated_with_who_deleted_them_and_every_one_to_an_administrator(EngineKind kind)
    {
        var (first, second) = await TwoProjectsAsync(kind);
        await using var session = first.Database.Session();
        var reader = new ProjectReader(session);
        var trashed = Assert.Single(await reader.TrashAsync(new ProjectVisibility(second.Actor.Id, false), Token));
        Assert.Equal((second.ProjectId, second.Other.DisplayName, 1), (trashed.Project.Id, trashed.DeletedByName, trashed.StepCount));
        Assert.Equal(second.Other.Id, trashed.Project.DeletedBy);
        Assert.Empty(await reader.TrashAsync(new ProjectVisibility(first.Actor.Id, false), Token));
        var every = await reader.TrashAsync(new ProjectVisibility(first.Actor.Id, true), Token);
        Assert.Contains(every, row => row.Project.Id == second.ProjectId);
        Assert.DoesNotContain(every, row => row.Project.Id == first.ProjectId);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_codes_in_use_name_the_live_projects_and_those_of_the_trash(EngineKind kind)
    {
        var (first, second) = await TwoProjectsAsync(kind);
        await using var session = first.Database.Session();
        var reader = new ProjectReader(session);
        var firstCode = (await first.LoadAsync()).Project.Code;
        var secondCode = (await second.LoadAsync()).Project.Code;
        Assert.Equal([new ExistingProjectCode(firstCode, false)], await reader.CodesAsync(firstCode, Token));
        Assert.Equal([new ExistingProjectCode(secondCode, true)], await reader.CodesAsync(secondCode, Token));
        Assert.Empty(await reader.CodesAsync(ScenarioSteps.NewProjectCode(), Token));
        Assert.Empty(await reader.CodesAsync(firstCode.ToLowerInvariant(), Token));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Users_are_found_by_their_login_whatever_its_case_also_in_packets_beyond_the_limit_of_parameters(EngineKind kind)
    {
        var scenario = await StoreScenario.CreateAsync(await databases.OfAsync(kind));
        await using var session = scenario.Database.Session();
        var reader = new UserReader(session);
        Assert.Equal(scenario.Actor, await reader.FindByLoginAsync(scenario.Actor.Login.ToLowerInvariant(), Token));
        Assert.Equal(scenario.Other, await reader.FindAsync(scenario.Other.Id, Token));
        Assert.Null(await reader.FindByLoginAsync(@"FICTITIOUS\Nobody_" + Guid.CreateVersion7().ToString("N"), Token));
        var logins = Enumerable.Range(0, 2_300).Select(index => $@"FICTITIOUS\Absent{index}_{Guid.CreateVersion7():N}")
            .Append(scenario.Actor.Login.ToUpperInvariant()).Append(scenario.Other.Login).ToList();
        var found = await reader.FindByLoginsAsync(logins, Token);
        Assert.Equal(new[] { scenario.Actor.Id, scenario.Other.Id }.Order(), found.Select(user => user.Id).Order());
    }

    /// <summary>The users named by an export (P5-T01b): by identifier, in packets beyond the limit of parameters, absent ones left out.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Users_are_found_by_their_identifiers_also_in_packets_beyond_the_limit_of_parameters(EngineKind kind)
    {
        var scenario = await StoreScenario.CreateAsync(await databases.OfAsync(kind));
        await using var session = scenario.Database.Session();
        var reader = new UserReader(session);
        var ids = Enumerable.Range(0, 2_300).Select(_ => Guid.CreateVersion7()).Append(scenario.Actor.Id).Append(scenario.Other.Id)
            .Append(scenario.Actor.Id).ToList();

        var found = await reader.FindManyAsync(ids, Token);

        Assert.Equal(new[] { scenario.Actor, scenario.Other }.OrderBy(user => user.Id), found.OrderBy(user => user.Id));
        Assert.Empty(await reader.FindManyAsync([], Token));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_profile_holds_the_memberships_of_the_live_projects_only(EngineKind kind)
    {
        var (first, second) = await TwoProjectsAsync(kind);
        await using var session = first.Database.Session();
        var reader = new UserReader(session);
        var profile = await reader.ProfileAsync(second.Actor.Id, Token);
        Assert.Equal(second.Actor, profile!.User);
        Assert.Empty(profile.Roles);
        Assert.Equal([new ProjectRole(first.ProjectId, MemberRole.Facilitator, false)], (await reader.ProfileAsync(first.Actor.Id, Token))!.Roles);
        Assert.Null(await reader.ProfileAsync(Guid.CreateVersion7(), Token));
    }
}
