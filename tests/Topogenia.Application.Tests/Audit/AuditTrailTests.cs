// AuditTrailTests.cs
// The audit trail: the events of each traced change written with it, identifiers only, read by an administrator alone (spec 5.5, A-4-27).

using System.Text.Json;
using Topogenia.Application.Audit;
using Topogenia.Application.Members;
using Topogenia.Application.Projects;
using Topogenia.Application.Tests.Support;
using Topogenia.Application.Tests.Users;
using Topogenia.Application.Trash;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;
using Topogenia.Domain.Projects;
using Topogenia.Domain.Workspace;

namespace Topogenia.Application.Tests.Audit;

public sealed class AuditTrailTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static List<AuditEvent> Events(UsersFixture fixture) => [.. fixture.Database.All<AuditEvent>().OrderBy(audit => audit.At).ThenBy(audit => audit.Kind)];

    private static ReplaceMembersCommand Replace(UsersFixture fixture, params MemberEntry[] members) =>
        new(fixture.Project.ProjectId, members, PeopleSource.Of(fixture.Ports));

    [Theory]
    [InlineData(0, 365)]
    [InlineData(-30, 365)]
    [InlineData(1, 1)]
    [InlineData(90, 90)]
    public void A_retention_that_is_not_positive_takes_the_year_by_default_and_never_erases_the_whole_trail(int setting, int days) =>
        Assert.Equal(days, AuditTrail.RetentionDays(setting));

    [Fact]
    public async Task The_first_sign_in_of_a_login_is_traced_once_with_the_new_user()
    {
        var fixture = new UsersFixture();
        var first = await fixture.Runner.SignInAsync(@"FICTIF\nouveau", null, Token);
        await fixture.Runner.SignInAsync(@"FICTIF\nouveau", null, Token);
        await fixture.Runner.SignInAsync(fixture.Project.Reader.Login, null, Token);
        var audit = Assert.Single(Events(fixture));
        Assert.Equal((AuditKinds.UserCreated, (Guid?)first.Id, (Guid?)null, (string?)null, TestProject.Now),
            (audit.Kind, audit.UserId, audit.ProjectId, audit.Details, audit.At));
        Assert.Equal(1, fixture.Database.Commits);
    }

    [Fact]
    public async Task A_project_created_is_traced_in_its_unit_with_its_creator()
    {
        var fixture = new UsersFixture();
        fixture.ActingAs(fixture.Project.Admin);
        var created = await fixture.Runner.RunAsync(new CreateProjectCommand(new CreateProjectRequest("VTE", "Ventes")), Token);
        var audit = Assert.Single(Events(fixture));
        Assert.Equal((AuditKinds.ProjectCreated, (Guid?)fixture.Project.Admin.Id, (Guid?)created.Id), (audit.Kind, audit.UserId, audit.ProjectId));
        Assert.Equal(1, fixture.Database.Commits);
    }

    [Fact]
    public async Task A_change_of_the_members_is_traced_with_the_users_added_removed_and_changed_and_an_unchanged_list_is_not()
    {
        var fixture = new UsersFixture();
        var project = fixture.Project;
        await fixture.Commands.RunAsync(Replace(fixture,
            new MemberEntry(project.Facilitator.Login, "facilitator", false), new MemberEntry(project.Contributor.Login, "reader", false),
            new MemberEntry(project.Admin.Login, "contributor", false)), Token);
        var audit = Assert.Single(Events(fixture));
        Assert.Equal((AuditKinds.MembersChanged, (Guid?)project.Facilitator.Id, (Guid?)project.ProjectId), (audit.Kind, audit.UserId, audit.ProjectId));
        var details = JsonDocument.Parse(audit.Details!).RootElement;
        Assert.Equal([project.Admin.Id], details.GetProperty("added").EnumerateArray().Select(id => id.GetGuid()));
        Assert.Equal([project.Reader.Id], details.GetProperty("removed").EnumerateArray().Select(id => id.GetGuid()));
        Assert.Equal([project.Contributor.Id], details.GetProperty("changed").EnumerateArray().Select(id => id.GetGuid()));
        Assert.DoesNotContain("FICTIF", audit.Details, StringComparison.OrdinalIgnoreCase);
        await fixture.Commands.RunAsync(Replace(fixture,
            new MemberEntry(project.Facilitator.Login, "facilitator", false), new MemberEntry(project.Contributor.Login, "reader", false),
            new MemberEntry(project.Admin.Login, "contributor", false)), Token);
        Assert.Single(Events(fixture));
    }

    [Fact]
    public async Task A_refused_change_leaves_no_event()
    {
        var fixture = new UsersFixture();
        await Assert.ThrowsAsync<DomainException>(() => fixture.Commands.RunAsync(new MoveToTrashCommand(fixture.Project.ProjectId, "AUTRE"), Token));
        await Assert.ThrowsAsync<DomainException>(() => fixture.Commands.RunAsync(Replace(fixture,
            new MemberEntry(fixture.Project.Contributor.Login, "contributor", false)), Token));
        Assert.Empty(Events(fixture));
    }

    [Fact]
    public async Task The_trash_the_restore_and_the_purge_of_a_project_are_traced_with_their_actor()
    {
        var fixture = new UsersFixture();
        var projectId = fixture.Project.ProjectId;
        await fixture.Commands.RunAsync(new MoveToTrashCommand(projectId, "PRJ"), Token);
        await fixture.Commands.RunAsync(new RestoreProjectCommand(projectId), Token);
        fixture.Project.PutInTrash();
        fixture.ActingAs(fixture.Project.Admin);
        await fixture.Runner.RunAsync(new PurgeProjectCommand(projectId), Token);
        Assert.Equal(
            [(AuditKinds.ProjectTrashed, fixture.Project.Facilitator.Id), (AuditKinds.ProjectRestored, fixture.Project.Facilitator.Id),
                (AuditKinds.ProjectPurged, fixture.Project.Admin.Id)],
            fixture.Database.All<AuditEvent>().OrderBy(audit => audit.Kind == AuditKinds.ProjectPurged ? 2 : audit.Kind == AuditKinds.ProjectRestored ? 1 : 0)
                .Select(audit => (audit.Kind, audit.UserId!.Value)));
        Assert.All(Events(fixture), audit => Assert.Equal(projectId, audit.ProjectId));
    }

    [Fact]
    public async Task The_trail_is_read_by_an_administrator_alone_newest_first_and_by_its_filters()
    {
        var fixture = new UsersFixture();
        var store = new InMemoryAuditStore(fixture.Database);
        var other = Guid.CreateVersion7();
        foreach (var (minutes, kind, project) in new[] { (1, "a", fixture.Project.ProjectId), (2, "b", other), (3, "a", other) })
            await store.AppendAsync(new AuditEvent(Guid.CreateVersion7(), TestProject.Now.AddMinutes(minutes), fixture.Project.Admin.Id, kind, project, null), Token);
        var refusal = await Assert.ThrowsAsync<DomainException>(() => fixture.Runner.RunAsync(new ReadAuditQuery(null, null, null, null, null, null), Token));
        Assert.Equal(ErrorCodes.AccessDenied, refusal.Code);
        fixture.ActingAs(fixture.Project.Admin);
        var all = await fixture.Runner.RunAsync(new ReadAuditQuery(null, null, null, null, null, null), Token);
        Assert.Equal([3, 2, 1], all.Select(audit => (int)(audit.At - TestProject.Now).TotalMinutes));
        var filtered = await fixture.Runner.RunAsync(new ReadAuditQuery(TestProject.Now.AddMinutes(2), TestProject.Now.AddMinutes(4), other, null, " a ", 5), Token);
        Assert.Equal([3], filtered.Select(audit => (int)(audit.At - TestProject.Now).TotalMinutes));
        Assert.Single(await fixture.Runner.RunAsync(new ReadAuditQuery(null, null, null, null, null, 1), Token));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1001, false)]
    [InlineData(10, true)]
    public async Task A_limit_outside_its_bounds_or_a_period_that_ends_before_it_starts_is_refused(int limit, bool reversed)
    {
        var fixture = new UsersFixture();
        fixture.ActingAs(fixture.Project.Admin);
        var from = reversed ? TestProject.Now : (DateTime?)null;
        var to = reversed ? TestProject.Now.AddMinutes(-1) : (DateTime?)null;
        var refusal = await Assert.ThrowsAsync<DomainException>(() => fixture.Runner.RunAsync(new ReadAuditQuery(from, to, null, null, null, limit), Token));
        Assert.Equal((ErrorCodes.InvalidValue, (object?)(reversed ? "to" : "limit")), (refusal.Code, refusal.Details["field"]));
    }

    [Fact]
    public void The_details_of_a_change_of_many_members_fall_back_to_their_counts()
    {
        var members = Enumerable.Range(0, 120).Select(_ => new RecordChange(ChangeKind.Created, null,
            new ProjectMember(Guid.Empty, Guid.CreateVersion7(), Domain.Values.MemberRole.Reader, false, false, 0, TestProject.Now, Guid.Empty, null, null))).ToList();
        var details = AuditTrail.MembersDetails(new ChangeSet(members));
        Assert.Equal("""{"added":120,"removed":0,"changed":0}""", details);
        Assert.True(AuditTrail.MembersDetails(new ChangeSet(members.Take(10).ToList())).Length <= AuditTrail.DetailsMax);
    }

    [Fact]
    public void A_series_of_refusals_is_described_by_its_threshold_and_window_only()
    {
        var user = Guid.CreateVersion7();
        var audit = AuditTrail.OfRefusals(TestProject.Now, user);
        Assert.Equal((AuditKinds.RepeatedRefusals, (Guid?)user, (Guid?)null, """{"refusals":10,"window_minutes":10}"""),
            (audit.Kind, audit.UserId, audit.ProjectId, audit.Details));
    }
}
