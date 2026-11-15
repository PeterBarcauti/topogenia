// ProfileCommandsTests.cs
// The profile through the template of the user requests: read with the roles, changed and versioned, the last project opened (annex A 1, AR-07, AR-49).

using Topogenia.Application.Ports;
using Topogenia.Application.Profile;
using Topogenia.Application.Tests.Support;
using Topogenia.Application.Tests.Users;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Projects;
using Topogenia.Domain.Values;

namespace Topogenia.Application.Tests.Profile;

public sealed class ProfileCommandsTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_profile_holds_the_roles_of_the_user_in_the_live_projects_only()
    {
        var fixture = new UsersFixture();
        var other = fixture.OtherProject("ACH", fixture.Project.Contributor, MemberRole.Reader);
        fixture.OtherProject("ZZZ", fixture.Project.Contributor, MemberRole.Facilitator, inTrash: true);
        fixture.ActingAs(fixture.Project.Contributor with { Language = "en" });
        var profile = await fixture.Runner.RunAsync(new GetProfileQuery(), Token);
        Assert.Equal((fixture.Project.Contributor.Id, @"FICTIF\contributeur", "Carl Contributeur", "fr", "en", false, true, 1),
            (profile.Id, profile.Login, profile.DisplayName, profile.Language, profile.EffectiveLanguage, profile.IsAdmin, profile.FirstVisitDone, profile.Version));
        Assert.Equal([new ProjectRole(fixture.Project.ProjectId, MemberRole.Contributor, false), new ProjectRole(other, MemberRole.Reader, false)],
            profile.Roles.OrderBy(role => role.ProjectId));
    }

    /// <summary>AR-29, NF12, ADR 057: the interface builds its language switch from the languages of the configuration.</summary>
    [Fact]
    public async Task The_profile_lists_the_languages_of_the_configuration_in_their_order()
    {
        var fixture = new UsersFixture();
        var profile = await fixture.Runner.RunAsync(new GetProfileQuery(), Token);
        Assert.Equal(["fr", "en"], profile.Languages);
        var changed = await fixture.Runner.RunAsync(ChangeProfileCommand.WithVersion(new ProfileChange(Language: "en", DisplayName: null, FirstVisitDone: null), 1), Token);
        Assert.Equal(["fr", "en"], changed.Languages);
    }

    [Fact]
    public async Task A_change_of_the_profile_is_written_with_the_next_version_and_the_new_effective_language()
    {
        var fixture = new UsersFixture();
        var profile = await fixture.Runner.RunAsync(ChangeProfileCommand.WithVersion(
            new ProfileChange(Language: "en", DisplayName: "  Anne A. ", FirstVisitDone: false), 1), Token);
        Assert.Equal(("en", "en", "Anne A.", false, 2), (profile.Language, profile.EffectiveLanguage, profile.DisplayName, profile.FirstVisitDone, profile.Version));
        var stored = fixture.UserRecord(fixture.Project.Facilitator.Id);
        Assert.Equal(("Anne A.", 2, (Guid?)fixture.Project.Facilitator.Id), (stored.DisplayName, stored.Version, stored.UpdatedBy));
        Assert.Equal(1, fixture.Database.Commits);
    }

    [Fact]
    public async Task A_profile_changed_since_the_version_read_is_refused_and_a_change_without_version_is_not_checked()
    {
        var fixture = new UsersFixture();
        await fixture.Runner.RunAsync(new ChangeProfileCommand(new ProfileChange(DisplayName: "Anne B.")), Token);
        var refusal = await Assert.ThrowsAsync<DomainException>(() =>
            fixture.Runner.RunAsync(ChangeProfileCommand.WithVersion(new ProfileChange(DisplayName: "Anne C."), 1), Token));
        Assert.Equal((ErrorCodes.VersionConflict, "user", (object?)fixture.Project.Facilitator.Id), (refusal.Code, refusal.Details["object"], refusal.Details["updated_by"]));
        Assert.Equal("Anne B.", fixture.UserRecord(fixture.Project.Facilitator.Id).DisplayName);
    }

    [Fact]
    public async Task A_change_of_the_profile_without_the_version_its_mode_requires_is_refused_428_and_writes_nothing()
    {
        var fixture = new UsersFixture();
        fixture.Project.Versions.IsMissing = true;
        var refusal = await Assert.ThrowsAsync<DomainException>(() =>
            fixture.Runner.RunAsync(new ChangeProfileCommand(new ProfileChange(DisplayName: "Anne B.")), Token));
        Assert.Equal((ErrorCodes.VersionRequired, RefusalKind.PreconditionRequired), (refusal.Code, refusal.Kind));
        Assert.Equal(("Anne Animatrice", 1), (fixture.UserRecord(fixture.Project.Facilitator.Id).DisplayName, fixture.UserRecord(fixture.Project.Facilitator.Id).Version));
    }

    [Theory]
    [InlineData("de", null, ErrorCodes.InvalidLanguage)]
    [InlineData(null, " ", ErrorCodes.RequiredField)]
    [InlineData(null, "<b>Anne</b>", ErrorCodes.HtmlForbidden)]
    public async Task An_invalid_change_of_the_profile_is_refused_and_writes_nothing(string? language, string? name, string code)
    {
        var fixture = new UsersFixture();
        var refusal = await Assert.ThrowsAsync<DomainException>(() =>
            fixture.Runner.RunAsync(new ChangeProfileCommand(new ProfileChange(language, name)), Token));
        Assert.Equal(code, refusal.Code);
        Assert.Equal(1, fixture.UserRecord(fixture.Project.Facilitator.Id).Version);
        Assert.Equal(0, fixture.Database.Commits);
    }

    [Fact]
    public async Task A_display_name_longer_than_128_characters_is_refused()
    {
        var fixture = new UsersFixture();
        var refusal = await Assert.ThrowsAsync<DomainException>(() =>
            fixture.Runner.RunAsync(new ChangeProfileCommand(new ProfileChange(DisplayName: new string('a', 129))), Token));
        Assert.Equal((ErrorCodes.TextTooLong, (object?)128), (refusal.Code, refusal.Details["max"]));
    }

    [Fact]
    public async Task An_unchanged_profile_writes_nothing()
    {
        var fixture = new UsersFixture();
        var profile = await fixture.Runner.RunAsync(new ChangeProfileCommand(new ProfileChange("fr", "Anne Animatrice", true)), Token);
        Assert.Equal(1, profile.Version);
        Assert.DoesNotContain("save", fixture.Database.Calls);
    }

    [Fact]
    public async Task The_last_project_opened_is_remembered_for_a_reader_of_the_project_only()
    {
        var fixture = new UsersFixture();
        fixture.ActingAs(fixture.Project.Reader);
        await fixture.Runner.RunAsync(new RememberOpenedProjectCommand(fixture.Project.ProjectId), Token);
        Assert.Equal(fixture.Project.ProjectId, (await fixture.Runner.RunAsync(new GetProfileQuery(), Token)).LastProjectId);
        fixture.ActingAs(fixture.Project.Stranger);
        Assert.Equal(ErrorCodes.NotFound, (await Assert.ThrowsAsync<DomainException>(() =>
            fixture.Runner.RunAsync(new RememberOpenedProjectCommand(fixture.Project.ProjectId), Token))).Code);
        Assert.Null(fixture.UserRecord(fixture.Project.Stranger.Id).LastProjectId);
        Assert.Equal(TestProject.Now, fixture.UserRecord(fixture.Project.Reader.Id).UpdatedAt);
    }

    [Fact]
    public async Task A_project_in_the_trash_is_never_remembered_as_the_last_project_opened()
    {
        var fixture = new UsersFixture();
        fixture.Project.PutInTrash();
        Assert.Equal(ErrorCodes.NotFound, (await Assert.ThrowsAsync<DomainException>(() =>
            fixture.Runner.RunAsync(new RememberOpenedProjectCommand(fixture.Project.ProjectId), Token))).Code);
        Assert.Null(fixture.UserRecord(fixture.Project.Facilitator.Id).LastProjectId);
    }
}
