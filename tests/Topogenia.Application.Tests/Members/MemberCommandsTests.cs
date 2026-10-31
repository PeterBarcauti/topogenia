// MemberCommandsTests.cs
// The members through the command template: replacement in one unit with the invited users, never logged, and the participants (annex A 2.1).

using Topogenia.Application.Members;
using Topogenia.Application.Projects;
using Topogenia.Application.Tests.Support;
using Topogenia.Application.Tests.Users;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;
using Topogenia.Domain.Projects;
using Topogenia.Domain.Values;

namespace Topogenia.Application.Tests.Members;

public sealed class MemberCommandsTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ReplaceMembersCommand Replace(UsersFixture fixture, params MemberEntry[] members) =>
        new(fixture.Project.ProjectId, members, PeopleSource.Of(fixture.Ports));

    private static List<(Guid, MemberRole, int)> Members(UsersFixture fixture) =>
        fixture.Database.All<ProjectMember>().Where(member => member.ProjectId == fixture.Project.ProjectId).OrderBy(member => member.DisplayOrder)
            .Select(member => (member.UserId, member.Role, member.DisplayOrder)).ToList();

    [Fact]
    public async Task The_whole_list_is_replaced_in_its_order_with_the_invited_users_and_nothing_is_logged()
    {
        var fixture = new UsersFixture();
        fixture.Directory.Entries[@"FICTIF\invite"] = new DirectoryEntry("Ivan Invité", "ivan@exemple.test");
        var outcome = await fixture.Commands.RunAsync(Replace(fixture,
            new MemberEntry(@"FICTIF\invite", "contributor", false), new MemberEntry(" fictif\\ANIMATEUR ", "facilitator", true)), Token);

        var invited = Assert.Single(fixture.Database.All<AppUser>(), user => user.Login == @"FICTIF\invite");
        Assert.Equal(("Ivan Invité", "ivan@exemple.test"), (invited.DisplayName, invited.Email));
        Assert.Equal([(invited.Id, MemberRole.Contributor, 0), (fixture.Project.Facilitator.Id, MemberRole.Facilitator, 1)], Members(fixture));
        Assert.Equal([invited.Id], outcome.Result.InvitedUserIds);
        Assert.Equal([fixture.Project.Contributor.Id, fixture.Project.Reader.Id], outcome.Result.RemovedUserIds.Order());
        Assert.Equal(["Anne Animatrice", "Ivan Invité"], outcome.Result.Members.Select(member => member.DisplayName));
        Assert.Equal((null, null, 1), (outcome.OperationId, outcome.View, fixture.Database.Commits));
        Assert.Empty(fixture.Database.Entries);
        Assert.Equal([@"FICTIF\invite"], fixture.Directory.Asked);
    }

    [Fact]
    public async Task A_person_who_leaves_no_longer_holds_a_lane_nor_a_step()
    {
        var fixture = new UsersFixture();
        var step = fixture.Project.Step(0, assignee: fixture.Project.Contributor.Id);
        fixture.Database.Add(fixture.Database.Get<Lane>(Lane.KeyOf(fixture.Project.LaneId)) with { ActorUserId = fixture.Project.Contributor.Id });
        await fixture.Commands.RunAsync(Replace(fixture, new MemberEntry(fixture.Project.Facilitator.Login, "facilitator", false)), Token);
        Assert.Null(fixture.Database.Get<Lane>(Lane.KeyOf(fixture.Project.LaneId)).ActorUserId);
        Assert.Null(fixture.Database.Get<Step>(Step.KeyOf(step)).AssigneeUserId);
    }

    [Theory]
    [InlineData("contributor", "reader", ErrorCodes.ProjectWithoutFacilitator)]
    [InlineData("facilitator", "owner", ErrorCodes.InvalidMemberRole)]
    public async Task A_refused_list_leaves_the_members_as_they_were_and_creates_no_invited_user(string first, string second, string code)
    {
        var fixture = new UsersFixture();
        var before = Members(fixture);
        var refusal = await Assert.ThrowsAsync<DomainException>(() => fixture.Commands.RunAsync(Replace(fixture,
            new MemberEntry(fixture.Project.Facilitator.Login, first, false), new MemberEntry(@"FICTIF\invite", second, false)), Token));
        Assert.Equal(code, refusal.Code);
        Assert.Equal(before, Members(fixture));
        Assert.DoesNotContain(fixture.Database.All<AppUser>(), user => user.Login == @"FICTIF\invite");
        Assert.Equal(0, fixture.Database.Commits);
    }

    [Fact]
    public async Task Only_a_facilitator_replaces_the_members_and_a_non_member_does_not_see_the_project()
    {
        var fixture = new UsersFixture();
        var members = new MemberEntry(fixture.Project.Contributor.Login, "facilitator", false);
        fixture.ActingAs(fixture.Project.Contributor);
        Assert.Equal(ErrorCodes.AccessDenied, (await Assert.ThrowsAsync<DomainException>(() => fixture.Commands.RunAsync(Replace(fixture, members), Token))).Code);
        fixture.ActingAs(fixture.Project.Stranger);
        Assert.Equal(ErrorCodes.NotFound, (await Assert.ThrowsAsync<DomainException>(() => fixture.Commands.RunAsync(Replace(fixture, members), Token))).Code);
        fixture.ActingAs(fixture.Project.Admin);
        await fixture.Commands.RunAsync(Replace(fixture, members), Token);
        Assert.Equal([(fixture.Project.Contributor.Id, MemberRole.Facilitator, 0)], Members(fixture));
    }

    [Fact]
    public async Task A_unit_that_lost_the_creation_of_an_invited_user_runs_again_and_takes_the_user_created_since()
    {
        var fixture = new UsersFixture();
        var concurrent = new AppUser(fixture.Project.NextId(), @"FICTIF\invite", @"FICTIF\INVITE", "Créé ailleurs", null, null, false, false, null,
            TestProject.Now, null, null, 1);
        fixture.Database.Add(concurrent);
        fixture.Users.StaleReads = 1;
        await fixture.Commands.RunAsync(Replace(fixture,
            new MemberEntry(fixture.Project.Facilitator.Login, "facilitator", false), new MemberEntry(@"FICTIF\invite", "reader", false)), Token);
        Assert.Equal(2, fixture.Project.Unit.Attempts);
        Assert.Single(fixture.Database.All<AppUser>(), user => user.LoginNormalized == @"FICTIF\INVITE");
        Assert.Contains((concurrent.Id, MemberRole.Reader, 1), Members(fixture));
    }

    [Fact]
    public async Task The_participants_are_read_by_a_reader_sponsors_first()
    {
        var fixture = new UsersFixture();
        fixture.Database.Add(fixture.Database.Get<ProjectMember>(ProjectMember.KeyOf(fixture.Project.ProjectId, fixture.Project.Contributor.Id))
            with { IsSponsor = true, DisplayOrder = 5 });
        fixture.ActingAs(fixture.Project.Reader);
        var participants = await fixture.Queries.RunAsync(new GetParticipantsQuery(fixture.Project.ProjectId), Token);
        Assert.Equal([("Carl Contributeur", true), ("Anne Animatrice", false), ("Lea Lectrice", false)],
            participants.Select(member => (member.DisplayName, member.IsSponsor)));
        Assert.Equal(0, fixture.Database.Commits);
        fixture.ActingAs(fixture.Project.Stranger);
        Assert.Equal(ErrorCodes.NotFound, (await Assert.ThrowsAsync<DomainException>(() =>
            fixture.Queries.RunAsync(new GetParticipantsQuery(fixture.Project.ProjectId), Token))).Code);
    }

    /// <summary>
    /// A-2.1-23: the members of the support all have the display order 0 and were inserted in the order of their names; the
    /// facilitator, renamed with a name that sorts last, is inserted first, so only the name can order them.
    /// </summary>
    [Fact]
    public async Task Members_of_the_same_display_order_are_ordered_by_name()
    {
        var fixture = new UsersFixture();
        fixture.Database.Add(fixture.Database.Get<AppUser>(AppUser.KeyOf(fixture.Project.Facilitator.Id)) with { DisplayName = "Zoé Animatrice" });
        Assert.All(fixture.Database.All<ProjectMember>(), member => Assert.Equal((false, 0), (member.IsSponsor, member.DisplayOrder)));
        fixture.ActingAs(fixture.Project.Reader);

        string[] byName = ["Carl Contributeur", "Lea Lectrice", "Zoé Animatrice"];
        var participants = await fixture.Queries.RunAsync(new GetParticipantsQuery(fixture.Project.ProjectId), Token);
        Assert.Equal(byName, participants.Select(member => member.DisplayName));
        var detail = await fixture.Queries.RunAsync(new GetProjectQuery(fixture.Project.ProjectId), Token);
        Assert.Equal(byName, detail.Members.Select(member => member.DisplayName));
    }
}
