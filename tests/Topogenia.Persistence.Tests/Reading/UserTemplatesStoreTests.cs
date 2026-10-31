// UserTemplatesStoreTests.cs
// The handlers of lot C on both databases: creation, members, profile, trash and purge, sign-in and the races of a new login (ADR 033).

using System.Collections.Concurrent;
using Topogenia.Application.Maintenance;
using Topogenia.Application.Members;
using Topogenia.Application.Ports;
using Topogenia.Application.Profile;
using Topogenia.Application.Projects;
using Topogenia.Application.Trash;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;
using Topogenia.Domain.Projects;
using Topogenia.Domain.Trash;
using Topogenia.Domain.Values;
using Topogenia.Persistence.Errors;
using Topogenia.Persistence.Loading;
using Topogenia.Persistence.Purge;
using Topogenia.Persistence.Reading;
using Topogenia.Persistence.Tests.Infrastructure;
using Topogenia.Persistence.Writing;

namespace Topogenia.Persistence.Tests.Reading;

public sealed class UserTemplatesStoreTests(MigratedDatabases databases) : IClassFixture<MigratedDatabases>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string NewLogin(string name) => $@"FICTITIOUS\{name}_{Guid.CreateVersion7():N}";

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task An_administrator_creates_a_project_with_an_invited_user_in_one_unit_and_its_code_stays_taken(EngineKind kind)
    {
        var scenario = await StoreScenario.CreateAsync(await databases.OfAsync(kind));
        var admin = Runners.UserOf(scenario.Actor, isAdmin: true);
        var guest = NewLogin("Guest");
        var request = new CreateProjectRequest(ScenarioSteps.NewProjectCode(), "Ventes",
            Members: [new MemberEntry(guest, "reader", false), new MemberEntry(scenario.Other.Login, "contributor", true)]);
        var created = await Runners.RunAsync(scenario.Database, admin, new CreateProjectCommand(request));

        var invited = Assert.Single(await scenario.Database.UsersAsync(Login.Normalize(guest)));
        Assert.Equal([invited.Id], created.InvitedUserIds);
        var detail = await Runners.RunAsync(scenario.Database, Runners.UserOf(scenario.Other), new GetProjectQuery(created.Id));
        Assert.Equal((created.RootMapId, MemberRole.Contributor), (detail.RootMapId, detail.Role));
        Assert.Equal([scenario.Other.Id, scenario.Actor.Id, invited.Id], detail.Members.Select(member => member.UserId));
        var listed = await Runners.RunAsync(scenario.Database, Runners.UserOf(scenario.Actor), new ListProjectsQuery());
        Assert.Equal((MemberRole.Facilitator, 0, 1), listed.Where(item => item.Id == created.Id).Select(item => (item.Role, item.StepCount, item.MapCount)).Single());

        var again = await Assert.ThrowsAsync<DomainException>(() => Runners.RunAsync(scenario.Database, admin, new CreateProjectCommand(request)));
        Assert.Equal(ErrorCodes.ProjectCodeTaken, again.Code);
        await Runners.RunAsync(scenario.Database, Runners.UserOf(scenario.Actor), new MoveToTrashCommand(created.Id, request.Code));
        var trashed = await Assert.ThrowsAsync<DomainException>(() => Runners.RunAsync(scenario.Database, admin, new CreateProjectCommand(request)));
        Assert.Equal(ErrorCodes.ProjectCodeInTrash, trashed.Code);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_members_are_replaced_with_their_invited_users_and_a_refused_list_creates_nobody(EngineKind kind)
    {
        var scenario = await StoreScenario.CreateAsync(await databases.OfAsync(kind));
        var facilitator = Runners.UserOf(scenario.Actor);
        var guest = NewLogin("Guest");
        var refused = NewLogin("Refused");
        await using (var session = scenario.Database.Session())
        {
            var people = PeopleSource.Of(Runners.Ports(session));
            var refusal = await Assert.ThrowsAsync<DomainException>(() => Runners.Commands(session, facilitator).RunAsync(new ReplaceMembersCommand(
                scenario.ProjectId, [new MemberEntry(scenario.Actor.Login, "reader", false), new MemberEntry(refused, "reader", false)], people), Token));
            Assert.Equal(ErrorCodes.ProjectWithoutFacilitator, refusal.Code);
            var outcome = await Runners.Commands(session, facilitator).RunAsync(new ReplaceMembersCommand(scenario.ProjectId,
                [new MemberEntry(scenario.Actor.Login, "facilitator", false), new MemberEntry(guest, "contributor", false),
                    new MemberEntry(scenario.Other.Login, "reader", true)], people), Token);
            Assert.Null(outcome.OperationId);
        }
        Assert.Empty(await scenario.Database.UsersAsync(Login.Normalize(refused)));
        var invited = Assert.Single(await scenario.Database.UsersAsync(Login.Normalize(guest)));
        var participants = await Runners.RunAsync(scenario.Database, Runners.UserOf(scenario.Other), new GetParticipantsQuery(scenario.ProjectId));
        Assert.Equal([(scenario.Other.Id, MemberRole.Reader), (scenario.Actor.Id, MemberRole.Facilitator), (invited.Id, MemberRole.Contributor)],
            participants.Select(member => (member.UserId, member.Role)));
        await using var context = scenario.Database.Context();
        Assert.Empty(context.Set<OperationLogEntry>().Where(entry => entry.ProjectId == scenario.ProjectId));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_profile_is_changed_with_its_version_and_a_stale_version_is_refused(EngineKind kind)
    {
        var scenario = await StoreScenario.CreateAsync(await databases.OfAsync(kind));
        var user = Runners.UserOf(scenario.Actor);
        var changed = await Runners.RunAsync(scenario.Database, user, ChangeProfileCommand.WithVersion(new ProfileChange("en", "Renamed actor"), 1));
        Assert.Equal(("en", "Renamed actor", 2), (changed.Language, changed.DisplayName, changed.Version));
        var refusal = await Assert.ThrowsAsync<DomainException>(() =>
            Runners.RunAsync(scenario.Database, user, ChangeProfileCommand.WithVersion(new ProfileChange(DisplayName: "Again"), 1)));
        Assert.Equal((ErrorCodes.VersionConflict, (object?)scenario.Actor.Id), (refusal.Code, refusal.Details["updated_by"]));
        await Runners.RunAsync(scenario.Database, user, new RememberOpenedProjectCommand(scenario.ProjectId));
        var profile = await Runners.RunAsync(scenario.Database, user, new GetProfileQuery());
        Assert.Equal(("Renamed actor", (Guid?)scenario.ProjectId, 3), (profile.DisplayName, profile.LastProjectId, profile.Version));
        Assert.Equal([scenario.ProjectId], profile.Roles.Select(role => role.ProjectId));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_project_goes_to_the_trash_comes_back_and_is_purged_at_once_by_an_administrator(EngineKind kind)
    {
        var scenario = await StoreScenario.CreateAsync(await databases.OfAsync(kind));
        await scenario.LinkedStepsAsync(2);
        var facilitator = Runners.UserOf(scenario.Actor);
        var code = (await scenario.LoadAsync()).Project.Code;
        await Runners.RunAsync(scenario.Database, facilitator, new MoveToTrashCommand(scenario.ProjectId, code));
        var item = (await Runners.RunAsync(scenario.Database, facilitator, new ListTrashQuery())).Projects.Single(project => project.Id == scenario.ProjectId);
        Assert.Equal((scenario.Actor.DisplayName, 2, item.DeletedAt.AddDays(30)), (item.DeletedBy, item.StepCount, item.PurgeAt));
        await Runners.RunAsync(scenario.Database, facilitator, new RestoreProjectCommand(scenario.ProjectId));
        Assert.False((await scenario.LoadAsync()).Project.IsInTrash);
        await Runners.RunAsync(scenario.Database, facilitator, new MoveToTrashCommand(scenario.ProjectId, code));

        var refusal = await Assert.ThrowsAsync<DomainException>(() => Runners.RunAsync(scenario.Database, facilitator, new PurgeProjectCommand(scenario.ProjectId)));
        Assert.Equal(ErrorCodes.AccessDenied, refusal.Code);
        var outcome = await Runners.RunAsync(scenario.Database, Runners.UserOf(scenario.Other, isAdmin: true), new PurgeProjectCommand(scenario.ProjectId));
        Assert.True(outcome.Records > 0);
        await using var session = scenario.Database.Session();
        Assert.Null(await new WorkspaceStore(session).LoadAsync(scenario.ProjectId, WorkspaceContent.None, Token));
        Assert.Empty(await scenario.Database.ProjectCodesAsync(code));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_daily_purge_takes_a_project_of_the_trash_past_the_retention(EngineKind kind)
    {
        var scenario = await StoreScenario.CreateAsync(await databases.OfAsync(kind));
        await scenario.RunAsync((workspace, context) => TrashRules.MoveToTrash(workspace, context, workspace.Project.Code));
        await using var session = scenario.Database.Session();
        var job = new ExpiredPurge(new ShiftedClock(TimeSpan.FromDays(31)), new TrashPurge(session), new WorkspaceStore(session), AppSettings.Default,
            new AuditStore(session));
        var report = await job.RunAsync(Token);
        Assert.Contains(report.Purged, done => done.ProjectId == scenario.ProjectId && done.Whole);
        Assert.DoesNotContain(report.Failures, failure => failure.ProjectId == scenario.ProjectId);
        Assert.Null(await new WorkspaceStore(session).LoadAsync(scenario.ProjectId, WorkspaceContent.None, Token));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_first_sign_in_creates_the_user_the_next_ones_only_read_it_and_the_administrator_flag_follows_the_configuration(EngineKind kind)
    {
        var database = await databases.OfAsync(kind);
        var login = NewLogin("Newcomer");
        await using var session = database.Session();
        var first = await Runners.Users(session, null).SignInAsync(login, "en", Token);
        Assert.Equal(("en", false), (first.Language, first.IsAdmin));
        var admin = await Runners.Users(session, null, null, login.ToLowerInvariant()).SignInAsync(login, null, Token);
        Assert.Equal((first.Id, true), (admin.Id, admin.IsAdmin));
        var stored = Assert.Single(await database.UsersAsync(Login.Normalize(login)));
        Assert.Equal((true, 2, (Guid?)first.Id, login[(login.IndexOf('\\', StringComparison.Ordinal) + 1)..]), (stored.IsAdmin, stored.Version, stored.UpdatedBy, stored.DisplayName));
        Assert.False((await Runners.Users(session, null).SignInAsync(login, null, Token)).IsAdmin);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Two_first_requests_of_one_login_at_the_same_time_both_sign_in_on_one_user(EngineKind kind)
    {
        var database = await databases.OfAsync(kind);
        var login = NewLogin("Twin");
        var meeting = new Rendezvous(kind, 2);
        var retries = new ConcurrentQueue<StoreError>();
        var users = await Concurrently.RunAsync(2, async _ =>
        {
            await using var session = database.Session(RetryPolicy.Standard(retrying: attempt => retries.Enqueue(attempt.Error)));
            return await Runners.Users(session, null, new MeetingDirectory(meeting)).SignInAsync(login, null, Token);
        });
        Assert.All(users, user => Assert.True(user.Succeeded));
        var stored = Assert.Single(await database.UsersAsync(Login.Normalize(login)));
        Assert.All(users, user => Assert.Equal(stored.Id, user.Result!.Id));
        Assert.True(kind == EngineKind.Sqlite || meeting.AllMet, meeting.Missed);
        if (meeting.AllMet)
            Assert.Equal(new StoreError(StoreErrorKind.CreationRace, "UQ_app_user_login_normalized"), Assert.Single(retries));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Two_projects_that_invite_one_new_login_through_their_members_at_once_name_one_user(EngineKind kind)
    {
        var database = await databases.OfAsync(kind);
        var scenarios = new[] { await StoreScenario.CreateAsync(database), await StoreScenario.CreateAsync(database) };
        var guest = NewLogin("Guest");
        var meeting = new Rendezvous(kind, 2);
        var outcomes = await Concurrently.RunAsync(2, async index =>
        {
            var scenario = scenarios[index];
            await using var session = database.Session();
            var people = PeopleSource.Of(Runners.Ports(session, new MeetingDirectory(meeting)));
            return await Runners.Commands(session, Runners.UserOf(scenario.Actor)).RunAsync(new ReplaceMembersCommand(scenario.ProjectId,
                [new MemberEntry(scenario.Actor.Login, "facilitator", false), new MemberEntry(guest, "reader", false)], people), Token);
        });
        Assert.All(outcomes, outcome => Assert.True(outcome.Succeeded));
        var user = Assert.Single(await database.UsersAsync(Login.Normalize(guest)));
        foreach (var scenario in scenarios)
            Assert.Contains((await scenario.LoadAsync(WorkspaceContent.Members)).All<ProjectMember>(), member => member.UserId == user.Id);
        Assert.True(kind == EngineKind.Sqlite || meeting.AllMet, meeting.Missed);
    }
}
