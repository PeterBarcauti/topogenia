// ProjectCommandsTests.cs
// The projects through the templates: creation by an administrator, list, detail, the label of the sharing group and the classification (annex A 1, A 2.1, spec 4.1).

using Topogenia.Application.Audit;
using Topogenia.Application.Projects;
using Topogenia.Application.Tests.Users;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;
using Topogenia.Domain.Projects;
using Topogenia.Domain.Values;

namespace Topogenia.Application.Tests.Projects;

public sealed class ProjectCommandsTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static CreateProjectRequest Sales(params MemberEntry[] members) =>
        new("VTE", "Ventes", Translations: new Dictionary<string, string?> { ["en"] = "Sales" }, Description: "Cycle de vente",
            SharingGroupLabel: "Groupe finance", Members: members);

    [Fact]
    public async Task An_administrator_creates_the_project_its_root_map_first_lane_counters_and_members_in_one_unit()
    {
        var fixture = new UsersFixture();
        fixture.ActingAs(fixture.Project.Admin);
        fixture.Directory.Entries[@"FICTIF\invite"] = new DirectoryEntry("Ivan Invité", null);
        var created = await fixture.Runner.RunAsync(new CreateProjectCommand(Sales(
            new MemberEntry(fixture.Project.Contributor.Login, "contributor", true), new MemberEntry(@" FICTIF\invite ", "reader", false))), Token);

        var project = fixture.Database.Get<Project>(Project.KeyOf(created.Id));
        Assert.Equal(("VTE", "Ventes", "fr", "Groupe finance", 1), (project.Code, project.Name, project.SourceLanguage, project.SharingGroupLabel, fixture.Database.Commits));
        var root = Assert.Single(fixture.Database.All<ProcessMap>(), map => map.ProjectId == created.Id);
        Assert.Equal((created.RootMapId, "Ventes"), (root.Id, root.Name));
        Assert.Equal("Acteur 1", Assert.Single(fixture.Database.All<Lane>(), lane => lane.MapId == root.Id).Name);
        Assert.Equal(3, fixture.Database.All<ProjectCounter>().Count(counter => counter.ProjectId == created.Id));
        var invited = Assert.Single(fixture.Database.All<AppUser>(), user => user.Login == @"FICTIF\invite");
        Assert.Equal([invited.Id], created.InvitedUserIds);
        Assert.Equal("Ivan Invité", invited.DisplayName);
        var members = fixture.Database.All<ProjectMember>().Where(member => member.ProjectId == created.Id).OrderBy(member => member.DisplayOrder)
            .Select(member => (member.UserId, member.Role, member.IsSponsor)).ToList();
        Assert.Equal([(fixture.Project.Admin.Id, MemberRole.Facilitator, false), (fixture.Project.Contributor.Id, MemberRole.Contributor, true),
            (invited.Id, MemberRole.Reader, false)], members);
        Assert.Contains(fixture.Database.All<Translation>(), translation => translation.ObjectId == created.Id && translation.Text == "Sales");
    }

    /// <summary>
    /// A-2.1-07: the creator is put first as facilitator only when the list does not hold him already. Here the list names
    /// the administrator who creates (another case, blanks around) as reader: he stays a single member, with that role, at his place in the list.
    /// </summary>
    [Fact]
    public async Task A_creator_already_in_the_list_keeps_his_role_and_is_not_added_twice()
    {
        var fixture = new UsersFixture();
        fixture.ActingAs(fixture.Project.Admin);
        var created = await fixture.Runner.RunAsync(new CreateProjectCommand(Sales(
            new MemberEntry(fixture.Project.Facilitator.Login, "facilitator", false), new MemberEntry(@" fictif\ADMIN ", "reader", false))), Token);

        var members = fixture.Database.All<ProjectMember>().Where(member => member.ProjectId == created.Id).OrderBy(member => member.DisplayOrder)
            .Select(member => (member.UserId, member.Role, member.DisplayOrder)).ToList();
        Assert.Equal([(fixture.Project.Facilitator.Id, MemberRole.Facilitator, 0), (fixture.Project.Admin.Id, MemberRole.Reader, 1)], members);
        Assert.Empty(created.InvitedUserIds);
        Assert.Single(fixture.Database.All<AppUser>(), user => user.LoginNormalized == @"FICTIF\ADMIN");
    }

    [Fact]
    public async Task Without_a_language_of_origin_the_effective_language_of_the_creator_applies()
    {
        var fixture = new UsersFixture();
        fixture.ActingAs(fixture.Project.Admin with { Language = "en" });
        var created = await fixture.Runner.RunAsync(new CreateProjectCommand(new CreateProjectRequest("VTE", "Sales")), Token);
        Assert.Equal("en", fixture.Database.Get<Project>(Project.KeyOf(created.Id)).SourceLanguage);
        Assert.Equal("Actor 1", Assert.Single(fixture.Database.All<Lane>(), lane => lane.MapId == created.RootMapId).Name);
    }

    [Fact]
    public async Task Only_an_administrator_creates_a_project_and_a_refusal_writes_nothing()
    {
        var fixture = new UsersFixture();
        var before = fixture.Database.All<IRecord>().Count();
        var refusal = await Assert.ThrowsAsync<DomainException>(() => fixture.Runner.RunAsync(new CreateProjectCommand(Sales()), Token));
        Assert.Equal(ErrorCodes.AccessDenied, refusal.Code);
        Assert.Empty(fixture.Database.Calls);
        Assert.Equal(before, fixture.Database.All<IRecord>().Count());
    }

    [Theory]
    [InlineData(false, ErrorCodes.ProjectCodeTaken)]
    [InlineData(true, ErrorCodes.ProjectCodeInTrash)]
    public async Task The_code_of_a_live_project_or_of_one_in_the_trash_is_taken_and_no_invited_user_is_created(bool inTrash, string code)
    {
        var fixture = new UsersFixture();
        fixture.OtherProject("VTE", fixture.Project.Reader, MemberRole.Facilitator, inTrash);
        fixture.ActingAs(fixture.Project.Admin);
        var refusal = await Assert.ThrowsAsync<DomainException>(() =>
            fixture.Runner.RunAsync(new CreateProjectCommand(Sales(new MemberEntry(@"FICTIF\invite", "reader", false)) with { Code = " VTE " }), Token));
        Assert.Equal((code, "VTE"), (refusal.Code, refusal.Details["code"]));
        Assert.Single(fixture.Database.All<Project>(), project => project.Code == "VTE");
        Assert.DoesNotContain(fixture.Database.All<AppUser>(), user => user.Login == @"FICTIF\invite");
        Assert.Equal(0, fixture.Database.Commits);
    }

    [Fact]
    public async Task An_invalid_member_list_refuses_the_creation_before_anything_is_written()
    {
        var fixture = new UsersFixture();
        fixture.ActingAs(fixture.Project.Admin);
        var refusal = await Assert.ThrowsAsync<DomainException>(() => fixture.Runner.RunAsync(new CreateProjectCommand(Sales(
            new MemberEntry(@"FICTIF\invite", "reader", false), new MemberEntry("invite@fictif", "reader", false))), Token));
        Assert.Equal(ErrorCodes.InvalidMemberLogin, refusal.Code);
        Assert.DoesNotContain(fixture.Database.All<Project>(), project => project.Code == "VTE");
        Assert.DoesNotContain(fixture.Database.All<AppUser>(), user => user.Login == @"FICTIF\invite");
    }

    [Fact]
    public async Task A_member_lists_his_live_projects_sorted_by_code_with_his_role_and_the_counts()
    {
        var fixture = new UsersFixture();
        fixture.Project.Step(0);
        fixture.Project.Step(1);
        fixture.OtherProject("ACH", fixture.Project.Reader, MemberRole.Contributor, steps: 1);
        fixture.OtherProject("ZZZ", fixture.Project.Reader, MemberRole.Facilitator, inTrash: true);
        fixture.OtherProject("ABC", fixture.Project.Contributor, MemberRole.Facilitator);
        fixture.ActingAs(fixture.Project.Reader);
        var items = await fixture.Runner.RunAsync(new ListProjectsQuery(), Token);
        Assert.Equal([("ACH", MemberRole.Contributor, 1, 1), ("PRJ", MemberRole.Reader, 2, 1)],
            items.Select(item => (item.Code, item.Role, item.StepCount, item.MapCount)));
        Assert.Equal(fixture.Project.RootMapId, items[1].RootMapId);
        fixture.ActingAs(fixture.Project.Stranger);
        Assert.Empty(await fixture.Runner.RunAsync(new ListProjectsQuery(), Token));
    }

    [Fact]
    public async Task An_administrator_lists_every_live_project_as_facilitator()
    {
        var fixture = new UsersFixture();
        fixture.OtherProject("ACH", fixture.Project.Reader, MemberRole.Reader);
        fixture.OtherProject("ZZZ", fixture.Project.Reader, MemberRole.Facilitator, inTrash: true);
        fixture.ActingAs(fixture.Project.Admin);
        var items = await fixture.Runner.RunAsync(new ListProjectsQuery(), Token);
        Assert.Equal([("ACH", MemberRole.Facilitator), ("PRJ", MemberRole.Facilitator)], items.Select(item => (item.Code, item.Role)));
    }

    [Fact]
    public async Task A_reader_gets_the_detail_with_the_members_in_display_order_the_version_and_the_root_map()
    {
        var fixture = new UsersFixture();
        fixture.Database.Add(fixture.Database.Get<ProjectMember>(ProjectMember.KeyOf(fixture.Project.ProjectId, fixture.Project.Reader.Id)) with { IsSponsor = true });
        fixture.ActingAs(fixture.Project.Reader);
        var detail = await fixture.Queries.RunAsync(new GetProjectQuery(fixture.Project.ProjectId), Token);
        Assert.Equal(("PRJ", fixture.Project.RootMapId, MemberRole.Reader, 1), (detail.Code, detail.RootMapId, detail.Role, detail.Version));
        Assert.Equal(["Lea Lectrice", "Anne Animatrice", "Carl Contributeur"], detail.Members.Select(member => member.DisplayName));
        fixture.ActingAs(fixture.Project.Stranger);
        Assert.Equal(ErrorCodes.NotFound, (await Assert.ThrowsAsync<DomainException>(() =>
            fixture.Queries.RunAsync(new GetProjectQuery(fixture.Project.ProjectId), Token))).Code);
    }

    [Fact]
    public async Task A_project_in_the_trash_is_out_of_reach_of_everyone_the_administrator_included()
    {
        var fixture = new UsersFixture();
        fixture.Project.PutInTrash();
        foreach (var user in new[] { fixture.Project.Facilitator, fixture.Project.Admin })
        {
            fixture.ActingAs(user);
            Assert.Equal(ErrorCodes.NotFound, (await Assert.ThrowsAsync<DomainException>(() =>
                fixture.Queries.RunAsync(new GetProjectQuery(fixture.Project.ProjectId), Token))).Code);
            Assert.Equal(ErrorCodes.NotFound, (await Assert.ThrowsAsync<DomainException>(() =>
                fixture.Commands.RunAsync(new ChangeSharingGroupLabelCommand(fixture.Project.ProjectId, "Groupe"), Token))).Code);
        }
    }

    [Fact]
    public async Task The_facilitator_changes_the_label_of_the_sharing_group_with_if_match_and_nothing_is_logged()
    {
        var fixture = new UsersFixture();
        var stale = ChangeSharingGroupLabelCommand.WithVersion(fixture.Project.ProjectId, "Groupe finance", 7);
        Assert.Equal(ErrorCodes.VersionConflict, (await Assert.ThrowsAsync<DomainException>(() => fixture.Commands.RunAsync(stale, Token))).Code);
        Assert.Null(fixture.Project.Project.SharingGroupLabel);
        await fixture.Commands.RunAsync(ChangeSharingGroupLabelCommand.WithVersion(fixture.Project.ProjectId, " Groupe finance ", 1), Token);
        Assert.Equal(("Groupe finance", 2), (fixture.Project.Project.SharingGroupLabel, fixture.Project.Project.Version));
        Assert.Empty(fixture.Database.Entries);
        fixture.ActingAs(fixture.Project.Contributor);
        Assert.Equal(ErrorCodes.AccessDenied, (await Assert.ThrowsAsync<DomainException>(() =>
            fixture.Commands.RunAsync(new ChangeSharingGroupLabelCommand(fixture.Project.ProjectId, null), Token))).Code);
    }

    [Fact]
    public async Task The_facilitator_changes_the_classification_with_if_match_the_audit_keeps_both_levels_and_the_list_and_the_detail_give_it()
    {
        var fixture = new UsersFixture();
        var stale = ChangeClassificationCommand.WithVersion(fixture.Project.ProjectId, "confidential", 7);
        Assert.Equal(ErrorCodes.VersionConflict, (await Assert.ThrowsAsync<DomainException>(() => fixture.Commands.RunAsync(stale, Token))).Code);
        Assert.Equal(Classification.Internal, fixture.Project.Project.Classification);
        await fixture.Commands.RunAsync(ChangeClassificationCommand.WithVersion(fixture.Project.ProjectId, "strictly_confidential", 1), Token);
        Assert.Equal((Classification.StrictlyConfidential, 2), (fixture.Project.Project.Classification, fixture.Project.Project.Version));
        Assert.Empty(fixture.Database.Entries);
        var audit = Assert.Single(fixture.Database.All<AuditEvent>());
        Assert.Equal((AuditKinds.ClassificationChanged, (Guid?)fixture.Project.Facilitator.Id, (Guid?)fixture.Project.ProjectId),
            (audit.Kind, audit.UserId, audit.ProjectId));
        Assert.Equal("""{"from":"internal","to":"strictly_confidential"}""", audit.Details);
        await fixture.Commands.RunAsync(new ChangeClassificationCommand(fixture.Project.ProjectId, "strictly_confidential"), Token);
        Assert.Single(fixture.Database.All<AuditEvent>());
        Assert.Equal(2, fixture.Project.Project.Version);
        fixture.ActingAs(fixture.Project.Reader);
        Assert.Equal(Classification.StrictlyConfidential, (await fixture.Queries.RunAsync(new GetProjectQuery(fixture.Project.ProjectId), Token)).Classification);
        Assert.Equal(Classification.StrictlyConfidential, Assert.Single(await fixture.Runner.RunAsync(new ListProjectsQuery(), Token)).Classification);
        fixture.ActingAs(fixture.Project.Contributor);
        Assert.Equal(ErrorCodes.AccessDenied, (await Assert.ThrowsAsync<DomainException>(() =>
            fixture.Commands.RunAsync(new ChangeClassificationCommand(fixture.Project.ProjectId, "internal"), Token))).Code);
        Assert.Equal(Classification.StrictlyConfidential, fixture.Project.Project.Classification);
        Assert.Single(fixture.Database.All<AuditEvent>());
    }

    [Fact]
    public async Task A_classification_refused_by_the_domain_writes_neither_the_project_nor_an_audit_event()
    {
        var fixture = new UsersFixture();
        foreach (var (code, error) in new[] { ((string?)null, ErrorCodes.RequiredField), ("secret", ErrorCodes.InvalidValue) })
        {
            var refusal = await Assert.ThrowsAsync<DomainException>(() => fixture.Commands.RunAsync(new ChangeClassificationCommand(fixture.Project.ProjectId, code), Token));
            Assert.Equal((error, (object?)"classification"), (refusal.Code, refusal.Details["field"]));
        }
        Assert.Equal((Classification.Internal, 1), (fixture.Project.Project.Classification, fixture.Project.Project.Version));
        Assert.Empty(fixture.Database.All<AuditEvent>());
    }

    [Fact]
    public async Task The_facilitator_changes_the_glossary_of_the_translations_with_if_match_the_detail_gives_it_and_nothing_is_logged()
    {
        var fixture = new UsersFixture();
        const string Glossary = "clôture mensuelle = monthly closing\nrapprochement = reconciliation";
        var stale = ChangeTranslationGlossaryCommand.WithVersion(fixture.Project.ProjectId, Glossary, 7);
        Assert.Equal(ErrorCodes.VersionConflict, (await Assert.ThrowsAsync<DomainException>(() => fixture.Commands.RunAsync(stale, Token))).Code);
        Assert.Null(fixture.Project.Project.TranslationGlossary);
        await fixture.Commands.RunAsync(ChangeTranslationGlossaryCommand.WithVersion(fixture.Project.ProjectId, " " + Glossary + " ", 1), Token);
        Assert.Equal((Glossary, 2), (fixture.Project.Project.TranslationGlossary, fixture.Project.Project.Version));
        Assert.Empty(fixture.Database.Entries);
        fixture.ActingAs(fixture.Project.Reader);
        Assert.Equal(Glossary, (await fixture.Queries.RunAsync(new GetProjectQuery(fixture.Project.ProjectId), Token)).TranslationGlossary);
        fixture.ActingAs(fixture.Project.Contributor);
        Assert.Equal(ErrorCodes.AccessDenied, (await Assert.ThrowsAsync<DomainException>(() =>
            fixture.Commands.RunAsync(new ChangeTranslationGlossaryCommand(fixture.Project.ProjectId, null), Token))).Code);
        Assert.Equal(Glossary, fixture.Project.Project.TranslationGlossary);
    }
}
