// PackageImportTests.cs
// The import of a package through the template of the user requests (spec 7.3, ADR 052): rights, code, people, replacement, all or nothing.

using System.Text.Json;
using Topogenia.Application.Audit;
using Topogenia.Application.Exchange;
using Topogenia.Application.Ports;
using Topogenia.Application.Tests.Support;
using Topogenia.Application.Tests.Users;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;
using Topogenia.Domain.Values;

namespace Topogenia.Application.Tests.Exchange;

public sealed class PackageImportTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private const string Author = "FICTIF\\auteur";
    private const string Member = "FICTIF\\membre";

    private sealed record FakeRead(string ProjectCode, bool WithPeople, IReadOnlyList<string> Logins, IReadOnlyList<PackageMember> Members,
        PackageCounts Counts) : IReadPackage;

    /// <summary>
    /// An importer that reads any bytes as a package of the code <c>VTE</c> and rebuilds a small project under the code of
    /// the plan: a root map, a lane, two steps (on one cell when <see cref="SameCell"/>), a member and an author by login.
    /// </summary>
    private sealed class FakeImporter(string code = "VTE") : IPackageImporter
    {
        public int Reads { get; private set; }

        public bool SameCell { get; init; }

        public string Language { get; init; } = "fr";

        public Classification Classification { get; init; } = Classification.Internal;

        public string LaneName { get; init; } = "Vendeur";

        public IReadPackage Read(byte[] content)
        {
            Reads++;
            return new FakeRead(code, true, [Author, Member], [new PackageMember(Member, "Marc Membre")], new PackageCounts(1, 1, 2, 0, 0, 0, 0, 0));
        }

        public IReadOnlyList<IRecord> Build(IReadPackage package, ImportPlan plan)
        {
            var context = plan.Context;
            var at = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
            var author = plan.User(Author) ?? context.ActorId;
            var project = new Project(context.NewId(), plan.ProjectCode, "Ventes", Language, null, null, Classification, null, null, 0, at, author,
                null, null, null, null, 1);
            var map = new ProcessMap(context.NewId(), project.Id, null, "Ventes", "fr", at, author, null, null, null, null, 1);
            var lane = new Lane(context.NewId(), map.Id, plan.ProjectCode + "-C01", LaneName, "fr", LaneType.Person, null, "#E3F2FD", 0, null, at,
                author, null, null, null, null, 1);
            Step Step(int number, int column) => new(context.NewId(), map.Id, lane.Id, $"{plan.ProjectCode}-E{number:D3}", "Étape", "fr", StepType.Task,
                column, 0, null, AutomationStatus.Manual, null, at, author, null, null, null, null, 1);
            List<IRecord> records = [project, new ProjectCounter(project.Id, CounterKind.Step, 2), new ProjectCounter(project.Id, CounterKind.Lane, 1),
                map, lane, Step(1, 0), Step(2, SameCell ? 0 : 1)];
            if (plan.WithMembers && plan.User(Member) is { } member)
                records.Add(new ProjectMember(project.Id, member, MemberRole.Contributor, false, false, 0, at, author, null, null));
            return records;
        }
    }

    private static (UsersFixture Fixture, FakeImporter Importer, PackageImport Import) Arrange(FakeImporter? importer = null)
    {
        var fixture = new UsersFixture();
        importer ??= new FakeImporter();
        return (fixture, importer, new PackageImport(fixture.Runner, importer));
    }

    private static Project ProjectCoded(UsersFixture fixture, string code) => fixture.Database.All<Project>().Single(project => project.Code == code);

    [Fact]
    public async Task An_administrator_imports_a_new_project_under_the_code_of_the_package_and_becomes_its_facilitator()
    {
        var (fixture, _, import) = Arrange();
        fixture.ActingAs(fixture.Project.Admin);

        var imported = await import.ImportAsync([1], null, withMembers: true, Token);

        var project = ProjectCoded(fixture, "VTE");
        Assert.Equal((project.Id, "VTE", (string?)null), (imported.Id, imported.Code, imported.TrashCode));
        Assert.Equal(imported.RootMapId, fixture.Database.All<ProcessMap>().Single(map => map.ProjectId == project.Id).Id);
        var members = fixture.Database.All<ProjectMember>().Where(member => member.ProjectId == project.Id).OrderBy(member => member.DisplayOrder).ToList();
        Assert.Equal((fixture.Project.Admin.Id, MemberRole.Facilitator), (members[0].UserId, members[0].Role));
        Assert.Equal(MemberRole.Contributor, members[1].Role);
        var created = fixture.Database.All<AppUser>().Where(user => imported.CreatedUserIds.Contains(user.Id)).ToList();
        Assert.Equal(["FICTIF\\auteur", "FICTIF\\membre"], created.Select(user => user.Login).Order(StringComparer.Ordinal));
        Assert.Equal("Marc Membre", created.Single(user => user.Login == Member).DisplayName);
        Assert.Equal(created.Single(user => user.Login == Author).Id, project.CreatedBy);
        var audit = Assert.Single(fixture.Database.All<AuditEvent>(), item => item.Kind == AuditKinds.ProjectImported);
        Assert.Equal((fixture.Project.Admin.Id, (Guid?)project.Id), (audit.UserId, audit.ProjectId));
        Assert.Equal("new", JsonDocument.Parse(audit.Details!).RootElement.GetProperty("mode").GetString());
    }

    [Fact]
    public async Task Without_the_members_a_login_named_only_by_the_members_creates_no_user()
    {
        var (fixture, _, import) = Arrange();
        fixture.ActingAs(fixture.Project.Admin);

        var imported = await import.ImportAsync([1], null, withMembers: false, Token);

        Assert.DoesNotContain(fixture.Database.All<AppUser>(), user => user.Login == Member);
        Assert.Single(imported.CreatedUserIds);
        Assert.Equal(fixture.Project.Admin.Id,
            Assert.Single(fixture.Database.All<ProjectMember>(), member => member.ProjectId == imported.Id).UserId);
    }

    [Fact]
    public async Task A_known_login_is_not_created_again()
    {
        var importer = new FakeImporter();
        var (fixture, _, import) = Arrange(importer);
        fixture.ActingAs(fixture.Project.Admin);
        var known = fixture.Project.Contributor;
        var users = fixture.Database.All<AppUser>().Count();
        // The author of the package is the contributor of the database (same login, another case).
        fixture.Database.Add(fixture.UserRecord(known.Id) with { Login = Author, LoginNormalized = Login.Normalize(Author) });

        var imported = await import.ImportAsync([1], null, withMembers: false, Token);

        Assert.Equal(users, fixture.Database.All<AppUser>().Count());
        Assert.Empty(imported.CreatedUserIds);
        Assert.Equal(known.Id, ProjectCoded(fixture, "VTE").CreatedBy);
    }

    [Theory]
    [InlineData("facilitator")]
    [InlineData("contributor")]
    [InlineData("stranger")]
    public async Task Only_an_administrator_imports_a_new_project_and_a_refused_import_reads_nothing(string who)
    {
        var (fixture, importer, import) = Arrange();
        fixture.ActingAs(who switch
        {
            "facilitator" => fixture.Project.Facilitator,
            "contributor" => fixture.Project.Contributor,
            _ => fixture.Project.Stranger,
        });
        var before = fixture.Database.All<IRecord>().Count();

        var refusal = await Assert.ThrowsAsync<DomainException>(() => import.ImportAsync([1], null, withMembers: true, Token));
        var preview = await Assert.ThrowsAsync<DomainException>(() => import.PreviewAsync([1], null, withMembers: true, Token));

        Assert.Equal((ErrorCodes.AccessDenied, ErrorCodes.AccessDenied), (refusal.Code, preview.Code));
        Assert.Equal(0, importer.Reads);
        Assert.Equal(before, fixture.Database.All<IRecord>().Count());
    }

    [Theory]
    [InlineData(false, ErrorCodes.ProjectCodeTaken)]
    [InlineData(true, ErrorCodes.ProjectCodeInTrash)]
    public async Task A_package_whose_code_is_taken_is_refused_until_a_free_code_is_chosen(bool inTrash, string code)
    {
        var (fixture, _, import) = Arrange(new FakeImporter("PRJ"));
        fixture.ActingAs(fixture.Project.Admin);
        if (inTrash)
            fixture.Project.PutInTrash();

        var refusal = await Assert.ThrowsAsync<DomainException>(() => import.ImportAsync([1], null, withMembers: false, Token));
        var imported = await import.ImportAsync([1], "ACH", withMembers: false, Token);

        Assert.Equal(code, refusal.Code);
        Assert.Equal("ACH", imported.Code);
        Assert.Equal("ACH-C01", fixture.Database.All<Lane>().Single(lane => fixture.Database.All<ProcessMap>().Any(map => map.Id == lane.MapId && map.ProjectId == imported.Id)).Code);
    }

    [Fact]
    public async Task The_preview_does_everything_but_the_writing_and_says_whether_the_code_is_free()
    {
        var (fixture, _, import) = Arrange(new FakeImporter("PRJ"));
        fixture.ActingAs(fixture.Project.Admin);
        var before = fixture.Database.All<IRecord>().Count();

        var taken = await import.PreviewAsync([1], null, withMembers: true, Token);
        var free = await import.PreviewAsync([1], "ACH", withMembers: false, Token);

        Assert.Equal(before, fixture.Database.All<IRecord>().Count());
        Assert.Equal(("PRJ", "PRJ", CodeState.Taken, true, 2), (taken.PackageCode, taken.ProjectCode, taken.CodeState, taken.WithPeople, taken.Members));
        Assert.Equal([Author, Member], taken.UnknownLogins);
        Assert.Equal(new PackageCounts(1, 1, 2, 0, 0, 0, 0, 0), taken.Counts);
        Assert.True(taken.Settlement.ImporterAdded);
        Assert.Equal(("ACH", CodeState.Free, 1, (string?)null), (free.ProjectCode, free.CodeState, free.Members, free.TrashCode));
    }

    [Fact]
    public async Task A_package_that_breaks_a_rule_of_the_structure_is_refused_and_nothing_is_written_not_even_its_users()
    {
        var (fixture, _, import) = Arrange(new FakeImporter { SameCell = true });
        fixture.ActingAs(fixture.Project.Admin);
        var before = fixture.Database.All<IRecord>().Count();

        var refusal = await Assert.ThrowsAsync<DomainException>(() => import.ImportAsync([1], null, withMembers: true, Token));

        Assert.Equal((ErrorCodes.InvalidPackage, (object?)"structure"), (refusal.Code, refusal.Details["reason"]));
        Assert.Equal(["cell"], (IEnumerable<string>)refusal.Details["rules"]!);
        Assert.Equal(before, fixture.Database.All<IRecord>().Count());
    }

    [Fact]
    public async Task A_package_with_a_value_that_its_rule_refuses_is_refused_with_its_field_and_nothing_is_written_not_even_its_users()
    {
        var (fixture, _, import) = Arrange(new FakeImporter { LaneName = "Vendeur " });
        fixture.ActingAs(fixture.Project.Admin);
        var before = fixture.Database.All<IRecord>().Count();

        var preview = await Assert.ThrowsAsync<DomainException>(() => import.PreviewAsync([1], null, withMembers: true, Token));
        var refusal = await Assert.ThrowsAsync<DomainException>(() => import.ImportAsync([1], null, withMembers: true, Token));

        foreach (var refused in new[] { preview, refusal })
        {
            Assert.Equal((ErrorCodes.InvalidPackage, (object?)"values"), (refused.Code, refused.Details["reason"]));
            Assert.Equal(["lane.name"], (IEnumerable<string>)refused.Details["fields"]!);
        }
        Assert.Equal(before, fixture.Database.All<IRecord>().Count());
    }

    [Fact]
    public async Task A_replacement_keeps_the_classification_of_the_replaced_project_and_a_new_import_takes_the_one_of_the_package()
    {
        var (fixture, _, import) = Arrange(new FakeImporter("NEUF") { Classification = Classification.Internal });
        var old = fixture.Project.ProjectId;
        fixture.Database.Add(fixture.Database.Get<Project>(Project.KeyOf(old)) with { Classification = Classification.StrictlyConfidential });
        fixture.ActingAs(fixture.Project.Facilitator);

        var replacing = await import.ReplaceAsync(old, [1], withMembers: false, version: null, Token);
        fixture.ActingAs(fixture.Project.Admin);
        var created = await import.ImportAsync([1], null, withMembers: false, Token);

        Assert.Equal(Classification.StrictlyConfidential, fixture.Database.Get<Project>(Project.KeyOf(replacing.Id)).Classification);
        Assert.Equal(Classification.Internal, fixture.Database.Get<Project>(Project.KeyOf(created.Id)).Classification);
    }

    [Fact]
    public async Task A_package_in_a_language_not_configured_here_is_refused()
    {
        var (fixture, _, import) = Arrange(new FakeImporter { Language = "de" });
        fixture.ActingAs(fixture.Project.Admin);

        var refusal = await Assert.ThrowsAsync<DomainException>(() => import.ImportAsync([1], null, withMembers: true, Token));

        Assert.Equal(ErrorCodes.InvalidLanguage, refusal.Code);
        Assert.DoesNotContain(fixture.Database.All<Project>(), project => project.Code == "VTE");
    }

    [Fact]
    public async Task The_facilitator_replaces_his_project_which_goes_to_the_trash_under_a_free_code_and_the_package_takes_its_code()
    {
        var (fixture, _, import) = Arrange();
        fixture.ActingAs(fixture.Project.Facilitator);
        var old = fixture.Project.ProjectId;

        var imported = await import.ReplaceAsync(old, [1], withMembers: true, version: 1, Token);

        var trashed = fixture.Database.Get<Project>(Project.KeyOf(old));
        Assert.Equal(("PRJA", true, TestProject.Now), (trashed.Code, trashed.IsInTrash, trashed.DeletedAt!.Value));
        Assert.Equal("PRJA-C01", fixture.Database.Get<Lane>(Lane.KeyOf(fixture.Project.LaneId)).Code);
        Assert.Equal(("PRJ", "PRJA"), (imported.Code, imported.TrashCode));
        Assert.Equal("PRJ-E002", fixture.Database.All<Step>().Single(step => step.Code.EndsWith("E002", StringComparison.Ordinal)).Code);
        Assert.Equal(fixture.Project.Facilitator.Id,
            fixture.Database.All<ProjectMember>().Single(member => member.ProjectId == imported.Id && member.Role == MemberRole.Facilitator).UserId);
        var kinds = fixture.Database.All<AuditEvent>().Select(audit => (audit.Kind, audit.ProjectId)).ToList();
        Assert.Contains((AuditKinds.ProjectTrashed, (Guid?)old), kinds);
        Assert.Contains((AuditKinds.ProjectImported, (Guid?)imported.Id), kinds);
    }

    [Fact]
    public async Task A_replacement_that_brings_no_members_keeps_the_team_of_the_replaced_project_and_its_preview_counts_it()
    {
        var (fixture, _, import) = Arrange();
        fixture.ActingAs(fixture.Project.Facilitator);
        var old = fixture.Project.ProjectId;
        var team = fixture.Database.All<ProjectMember>().Where(member => member.ProjectId == old).OrderBy(member => member.DisplayOrder)
            .Select(member => (member.UserId, member.Role, member.IsSponsor, member.DisplayOrder)).ToList();

        var preview = await import.PreviewReplacementAsync(old, [1], withMembers: false, Token);
        var imported = await import.ReplaceAsync(old, [1], withMembers: false, version: null, Token);

        Assert.True(team.Count >= 3);
        Assert.Equal(team, fixture.Database.All<ProjectMember>().Where(member => member.ProjectId == imported.Id).OrderBy(member => member.DisplayOrder)
            .Select(member => (member.UserId, member.Role, member.IsSponsor, member.DisplayOrder)));
        Assert.Equal((team.Count, false), (preview.Members, preview.Settlement.ImporterAdded));
    }

    [Fact]
    public async Task A_replacement_that_takes_the_members_of_the_package_replaces_the_team()
    {
        var (fixture, _, import) = Arrange();
        fixture.ActingAs(fixture.Project.Facilitator);

        var imported = await import.ReplaceAsync(fixture.Project.ProjectId, [1], withMembers: true, version: null, Token);

        var member = fixture.Database.All<AppUser>().Single(user => user.LoginNormalized == Login.Normalize(Member)).Id;
        Assert.Equal([fixture.Project.Facilitator.Id, member],
            fixture.Database.All<ProjectMember>().Where(item => item.ProjectId == imported.Id).OrderBy(item => item.DisplayOrder).Select(item => item.UserId));
    }

    [Fact]
    public async Task The_replacement_checks_the_version_of_the_replaced_project_and_writes_nothing_on_a_conflict()
    {
        var (fixture, _, import) = Arrange();
        fixture.ActingAs(fixture.Project.Facilitator);
        var before = fixture.Database.All<IRecord>().Count();

        var refusal = await Assert.ThrowsAsync<DomainException>(() => import.ReplaceAsync(fixture.Project.ProjectId, [1], withMembers: true, version: 7, Token));

        Assert.Equal(ErrorCodes.VersionConflict, refusal.Code);
        Assert.False(fixture.Project.Project.IsInTrash);
        Assert.Equal("PRJ", fixture.Project.Project.Code);
        Assert.Equal(before, fixture.Database.All<IRecord>().Count());
    }

    [Theory]
    [InlineData("contributor", ErrorCodes.AccessDenied)]
    [InlineData("reader", ErrorCodes.AccessDenied)]
    [InlineData("stranger", ErrorCodes.NotFound)]
    public async Task Only_the_facilitator_of_the_project_replaces_it(string who, string code)
    {
        var (fixture, importer, import) = Arrange();
        fixture.ActingAs(who switch
        {
            "contributor" => fixture.Project.Contributor,
            "reader" => fixture.Project.Reader,
            _ => fixture.Project.Stranger,
        });

        var refusal = await Assert.ThrowsAsync<DomainException>(() => import.ReplaceAsync(fixture.Project.ProjectId, [1], withMembers: true, version: null, Token));
        var preview = await Assert.ThrowsAsync<DomainException>(() => import.PreviewReplacementAsync(fixture.Project.ProjectId, [1], withMembers: true, Token));

        Assert.Equal((code, code), (refusal.Code, preview.Code));
        Assert.Equal(0, importer.Reads);
        Assert.False(fixture.Project.Project.IsInTrash);
    }

    [Fact]
    public async Task The_preview_of_a_replacement_gives_the_code_the_project_would_take_in_the_trash_and_writes_nothing()
    {
        var (fixture, _, import) = Arrange();
        fixture.ActingAs(fixture.Project.Facilitator);
        fixture.OtherProject("PRJA", fixture.Project.Facilitator, MemberRole.Facilitator, inTrash: true);
        var before = fixture.Database.All<IRecord>().Count();

        var preview = await import.PreviewReplacementAsync(fixture.Project.ProjectId, [1], withMembers: false, Token);

        Assert.Equal(("VTE", "PRJ", "PRJB"), (preview.PackageCode, preview.ProjectCode, preview.TrashCode));
        Assert.Equal(before, fixture.Database.All<IRecord>().Count());
        Assert.False(fixture.Project.Project.IsInTrash);
    }

    [Fact]
    public async Task The_replacement_locks_the_replaced_project_before_it_reads_it()
    {
        var (fixture, _, import) = Arrange();
        fixture.ActingAs(fixture.Project.Facilitator);
        var structure = fixture.Project.Project.StructureVersion;

        await import.ReplaceAsync(fixture.Project.ProjectId, [1], withMembers: false, version: null, Token);

        Assert.Equal(structure + 1, fixture.Database.Get<Project>(Project.KeyOf(fixture.Project.ProjectId)).StructureVersion);
        Assert.Contains("lock", fixture.Database.Calls);
    }
}
