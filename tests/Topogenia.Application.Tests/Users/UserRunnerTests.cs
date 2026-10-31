// UserRunnerTests.cs
// The template of the user requests: authentication, declared right, target, unit of work and retry, and the sign-in (spec 3.4, annex A 2.1).

using Topogenia.Application.Authorization;
using Topogenia.Application.Commands;
using Topogenia.Application.Ports;
using Topogenia.Application.Profile;
using Topogenia.Application.Projects;
using Topogenia.Application.Tests.Commands;
using Topogenia.Application.Tests.Support;
using Topogenia.Application.Trash;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;
using Topogenia.Domain.Projects;
using Topogenia.Domain.Values;

namespace Topogenia.Application.Tests.Users;

public sealed class UndeclaredRequest : UserRequest<bool>
{
    public override Task<bool> RunAsync(UserScope scope, CancellationToken cancellationToken) => Task.FromResult(true);
}

[RequiresRole(MemberRole.Reader)]
public sealed class RoleWithoutTargetRequest : UserRequest<bool>
{
    public override Task<bool> RunAsync(UserScope scope, CancellationToken cancellationToken) => Task.FromResult(true);
}

public sealed class UserRunnerTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string Describe(AccessRequirementAttribute requirement) => requirement switch
    {
        RequiresRoleAttribute { Minimum: var minimum } => Codes.Of(minimum),
        RequiresAdminAttribute => "admin",
        RequiresAuthenticatedAttribute => "authenticated",
        _ => requirement.GetType().Name,
    };

    private static bool IsUserRequest(Type? type)
    {
        for (; type is not null; type = type.BaseType)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(UserRequest<>))
                return true;
        }
        return false;
    }

    [Fact]
    public void Every_user_request_is_in_the_catalog_with_the_right_of_its_route()
    {
        var requests = typeof(UserRunner).Assembly.GetTypes().Where(type => !type.IsAbstract && IsUserRequest(type)).ToList();
        Assert.Equal(CommandCatalog.UserRequests.Keys.Select(type => type.FullName).Order(StringComparer.Ordinal),
            requests.Select(type => type.FullName).Order(StringComparer.Ordinal));
        Assert.All(requests, type => Assert.Equal(CommandCatalog.UserRequests[type], Describe(AccessPolicy.RequirementOf(type))));
    }

    [Fact]
    public async Task An_anonymous_request_is_refused_as_not_authenticated()
    {
        var fixture = new UsersFixture();
        fixture.Project.CurrentUser.User = null;
        var refusal = await Assert.ThrowsAsync<DomainException>(() => fixture.Runner.RunAsync(new ListProjectsQuery(), Token));
        Assert.Equal(ErrorCodes.NotAuthenticated, refusal.Code);
    }

    [Fact]
    public async Task An_administrator_request_is_refused_to_anyone_else_before_anything_is_read()
    {
        var fixture = new UsersFixture();
        fixture.ActingAs(fixture.Project.Facilitator);
        var refusal = await Assert.ThrowsAsync<DomainException>(() => fixture.Runner.RunAsync(new PurgeProjectCommand(fixture.Project.ProjectId), Token));
        Assert.Equal(ErrorCodes.AccessDenied, refusal.Code);
        Assert.Empty(fixture.Database.Calls);
        Assert.Empty(fixture.Purge.Purged);
    }

    [Fact]
    public async Task A_request_without_a_declared_right_or_with_a_role_and_no_project_is_never_run()
    {
        var fixture = new UsersFixture();
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Runner.RunAsync(new UndeclaredRequest(), Token));
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Runner.RunAsync(new RoleWithoutTargetRequest(), Token));
        Assert.Equal(0, fixture.Database.Commits);
    }

    [Fact]
    public async Task A_request_on_a_project_finds_it_in_the_expected_state_and_with_the_declared_role()
    {
        var fixture = new UsersFixture();
        fixture.ActingAs(fixture.Project.Stranger);
        Assert.Equal(ErrorCodes.NotFound, (await Assert.ThrowsAsync<DomainException>(() =>
            fixture.Runner.RunAsync(new RememberOpenedProjectCommand(fixture.Project.ProjectId), Token))).Code);
        fixture.ActingAs(fixture.Project.Admin);
        Assert.Equal(ErrorCodes.NotFound, (await Assert.ThrowsAsync<DomainException>(() =>
            fixture.Runner.RunAsync(new PurgeProjectCommand(fixture.Project.ProjectId), Token))).Code);
        Assert.Equal(fixture.Project.ProjectId, await fixture.Runner.RunAsync(new RememberOpenedProjectCommand(fixture.Project.ProjectId), Token));
        Assert.Equal(fixture.Project.ProjectId, fixture.UserRecord(fixture.Project.Admin.Id).LastProjectId);
    }

    [Fact]
    public async Task A_writing_request_runs_whole_again_after_a_transient_failure_and_writes_once()
    {
        var fixture = new UsersFixture();
        fixture.ActingAs(fixture.Project.Admin);
        fixture.Database.CommitFailures.Enqueue(new TimeoutException());
        var created = await fixture.Runner.RunAsync(new CreateProjectCommand(new CreateProjectRequest("VTE", "Ventes")), Token);
        Assert.Equal(2, fixture.Project.Unit.Attempts);
        var project = Assert.Single(fixture.Database.All<Project>(), project => project.Code == "VTE");
        Assert.Equal(created.Id, project.Id);
        Assert.Single(fixture.Database.All<ProcessMap>(), map => map.ProjectId == created.Id);
    }

    [Fact]
    public async Task A_read_request_takes_no_unit_of_work()
    {
        var fixture = new UsersFixture();
        await fixture.Runner.RunAsync(new ListTrashQuery(), Token);
        await fixture.Runner.RunAsync(new GetProfileQuery(), Token);
        Assert.Equal((0, 0), (fixture.Database.Commits, fixture.Project.Unit.Attempts));
    }

    [Fact]
    public async Task The_first_request_of_a_login_creates_the_user_from_the_directory_and_the_next_ones_only_read_it()
    {
        var fixture = new UsersFixture();
        fixture.Directory.Entries[@"FICTIF\nouveau"] = new DirectoryEntry("  Nina Nouvelle ", "nina@exemple.test");
        var first = await fixture.Runner.SignInAsync(@"FICTIF\nouveau", "fr-FR,fr;q=0.9", Token);
        var user = fixture.UserRecord(first.Id);
        Assert.Equal((@"FICTIF\nouveau", @"FICTIF\NOUVEAU", "Nina Nouvelle", "nina@exemple.test", false, (string?)null),
            (user.Login, user.LoginNormalized, user.DisplayName, user.Email, user.IsAdmin, user.Language));
        Assert.Equal(new CurrentUser(user.Id, user.Login, "Nina Nouvelle", "fr", false), first);
        Assert.Equal(1, fixture.Database.Commits);
        var again = await fixture.Runner.SignInAsync(@"fictif\NOUVEAU", null, Token);
        Assert.Equal((first.Id, "en"), (again.Id, again.Language));
        Assert.Equal(1, fixture.Database.Commits);
        Assert.Equal([@"FICTIF\nouveau"], fixture.Directory.Asked);
        Assert.Single(fixture.Database.All<AppUser>(), row => row.LoginNormalized == @"FICTIF\NOUVEAU");
    }

    [Fact]
    public async Task A_user_signed_in_is_kept_thirty_seconds_without_reading_and_forgotten_at_once_by_his_own_write()
    {
        var fixture = new UsersFixture();
        var reader = fixture.Project.Reader;
        await fixture.Runner.SignInAsync(reader.Login, null, Token);
        fixture.Database.Add(fixture.UserRecord(reader.Id) with { DisplayName = "Changé ailleurs", Version = 2 });
        Assert.Equal("Lea Lectrice", (await fixture.Runner.SignInAsync(reader.Login, null, Token)).DisplayName);
        fixture.Project.Clock.UtcNow = TestProject.Now + SignInCache.Duration;
        Assert.Equal("Changé ailleurs", (await fixture.Runner.SignInAsync(reader.Login, null, Token)).DisplayName);
        fixture.ActingAs(await fixture.Runner.SignInAsync(reader.Login, null, Token));
        await fixture.Runner.RunAsync(ChangeProfileCommand.WithVersion(new ProfileChange(DisplayName: "Léa L."), 2), Token);
        Assert.Equal("Léa L.", (await fixture.Runner.SignInAsync(reader.Login, null, Token)).DisplayName);
    }

    [Fact]
    public async Task A_change_of_the_administrators_is_seen_at_the_next_sign_in_even_while_the_user_is_kept()
    {
        var fixture = new UsersFixture();
        var reader = fixture.Project.Reader;
        Assert.False((await fixture.Runner.SignInAsync(reader.Login, null, Token)).IsAdmin);
        fixture.Administrators.List.Add(reader.Login);
        Assert.True((await fixture.Runner.SignInAsync(reader.Login, null, Token)).IsAdmin);
        fixture.Administrators.List.Remove(reader.Login);
        Assert.False((await fixture.Runner.SignInAsync(reader.Login, null, Token)).IsAdmin);
    }

    [Fact]
    public async Task A_login_the_directory_does_not_know_takes_its_short_name_and_an_invalid_login_is_refused()
    {
        var fixture = new UsersFixture();
        var user = await fixture.Runner.SignInAsync(@"FICTIF\inconnu2", null, Token);
        Assert.Equal(("inconnu2", "en"), (user.DisplayName, user.Language));
        var refusal = await Assert.ThrowsAsync<DomainException>(() => fixture.Runner.SignInAsync("inconnu3@fictif", null, Token));
        Assert.Equal(ErrorCodes.AccessDenied, refusal.Code);
        Assert.DoesNotContain(fixture.Database.All<AppUser>(), row => row.Login == "inconnu3@fictif");
    }

    [Fact]
    public async Task The_administrator_flag_follows_the_configuration_both_ways_at_each_sign_in()
    {
        var fixture = new UsersFixture();
        var reader = fixture.Project.Reader;
        fixture.Administrators.List.Add(@"fictif\LECTEUR");
        fixture.Administrators.List.Remove(fixture.Project.Admin.Login);
        Assert.True((await fixture.Runner.SignInAsync(reader.Login, null, Token)).IsAdmin);
        Assert.False((await fixture.Runner.SignInAsync(fixture.Project.Admin.Login, null, Token)).IsAdmin);
        var promoted = fixture.UserRecord(reader.Id);
        Assert.Equal((true, 2, (Guid?)reader.Id, (DateTime?)TestProject.Now), (promoted.IsAdmin, promoted.Version, promoted.UpdatedBy, promoted.UpdatedAt));
        Assert.False(fixture.UserRecord(fixture.Project.Admin.Id).IsAdmin);
        fixture.Administrators.List.Clear();
        Assert.False((await fixture.Runner.SignInAsync(reader.Login, null, Token)).IsAdmin);
        Assert.Equal(3, fixture.UserRecord(reader.Id).Version);
    }

    [Fact]
    public async Task The_language_of_the_profile_wins_over_the_browser_and_the_browser_over_the_default()
    {
        var fixture = new UsersFixture();
        Assert.Equal("fr", (await fixture.Runner.SignInAsync(fixture.Project.Reader.Login, "en-GB,en;q=0.8", Token)).Language);
        var fresh = await fixture.Runner.SignInAsync(@"FICTIF\autre", "de-DE,fr;q=0.5,en;q=0.7", Token);
        Assert.Equal("en", fresh.Language);
        Assert.Equal("fr", (await fixture.Runner.SignInAsync(@"FICTIF\autre", "de-DE,fr;q=0.5", Token)).Language);
    }
}
