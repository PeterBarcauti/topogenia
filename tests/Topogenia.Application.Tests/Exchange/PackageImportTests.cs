// PackageImportTests.cs
// The import of a package through the template of the user requests (spec 7.3, ADR 052): rights, code, people, replacement, all or nothing.

using System.Text.Json;
using Topogenia.Application.Audit;
using Topogenia.Application.Exchange;
using Topogenia.Application.Ports;
using Topogenia.Application.Presentation;
using Topogenia.Application.Tests.Support;
using Topogenia.Application.Tests.Users;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;
using Topogenia.Domain.Values;

namespace Topogenia.Application.Tests.Exchange;

public sealed partial class PackageImportTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private const string Author = "FICTIF\\auteur";
    private const string Member = "FICTIF\\membre";

    private sealed record FakeRead(string ProjectCode, bool WithPeople, IReadOnlyList<string> Logins, IReadOnlyList<PackageMember> Members,
        PackageCounts Counts, string? BaselineProjectCode) : IReadPackage;

    /// <summary>
    /// An importer that reads any bytes as a package of the code <c>VIS</c> and rebuilds a small project under the code of
    /// the plan: a root map, a lane, two steps (on one cell when <see cref="SameCell"/>), a member and an author by login.
    /// </summary>
    private sealed class FakeImporter(string code = "VIS") : IPackageImporter
    {
        public int Reads { get; private set; }

        public bool SameCell { get; init; }

        public string Language { get; init; } = "fr";

        public Classification Classification { get; init; } = Classification.Internal;

        public string LaneName { get; init; } = "Vendeur";

        /// <summary>The code of the source the package names, as the package of a target state does (ADR 095).</summary>
        public string? Baseline { get; init; }

        /// <summary>The comments of presentation the package carries (ADR 099): the index of their step (0 or 1), their text and language.</summary>
        public IReadOnlyList<(int Step, string Text, string Language)> Comments { get; init; } = [];

        /// <summary>
        /// The comments of presentation the package carries on a flow (ADR 099, lot J10): their text, language and source; with
        /// one at least, the package holds a flow from its first step to its second one.
        /// </summary>
        public IReadOnlyList<(string Text, string Language, CommentSource Source)> FlowComments { get; init; } = [];

        /// <summary>The voices the package carries for its project (ADR 103): their language, their name, whether they are voices of the computer.</summary>
        public IReadOnlyList<(string Language, string Name, bool Local)> Voices { get; init; } = [];

        public IReadPackage Read(byte[] content)
        {
            Reads++;
            return new FakeRead(code, true, [Author, Member], [new PackageMember(Member, "Marc Membre")], new PackageCounts(1, 1, 2, 0, 0, 0, 0, 0),
                Baseline);
        }

        public IReadOnlyList<IRecord> Build(IReadPackage package, ImportPlan plan)
        {
            var context = plan.Context;
            var at = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
            var author = plan.User(Author) ?? context.ActorId;
            var project = new Project(context.NewId(), plan.ProjectCode, "Visites", Language, null, null, Classification, null, null, 0, at, author,
                null, null, null, null, 1);
            var map = new ProcessMap(context.NewId(), project.Id, null, "Visites", "fr", at, author, null, null, null, null, 1);
            var lane = new Lane(context.NewId(), map.Id, plan.ProjectCode + "-C01", LaneName, "fr", LaneType.Person, null, "#E3F2FD", 0, null, at,
                author, null, null, null, null, 1);
            Step Step(int number, int column) => new(context.NewId(), map.Id, lane.Id, $"{plan.ProjectCode}-E{number:D3}", "Étape", "fr", StepType.Task,
                column, 0, null, AutomationStatus.Manual, null, at, author, null, null, null, null, 1);
            List<IRecord> records = [project, new ProjectCounter(project.Id, CounterKind.Step, 2), new ProjectCounter(project.Id, CounterKind.Lane, 1),
                map, lane, Step(1, 0), Step(2, SameCell ? 0 : 1)];
            if (plan.WithMembers && plan.User(Member) is { } member)
                records.Add(new ProjectMember(project.Id, member, MemberRole.Contributor, false, false, 0, at, author, null, null));
            if (FlowComments.Count > 0)
            {
                var steps = records.OfType<Step>().ToList();
                records.Add(new Flow(context.NewId(), map.Id, steps[0].Id, steps[1].Id, null, at, author, null, null, null, null, 1));
            }
            return records;
        }

        public RebuiltPackage Rebuild(IReadPackage package, ImportPlan plan)
        {
            var records = Build(package, plan);
            var steps = records.OfType<Step>().ToList();
            var flow = records.OfType<Flow>().SingleOrDefault();
            return new RebuiltPackage(records, [.. Comments.Select(comment =>
                new PresentationComment(steps[comment.Step].Id, comment.Text, comment.Language, CommentSource.Manual, 1, plan.Context.Now, plan.Context.ActorId))])
            {
                FlowComments = [.. FlowComments.Select(comment =>
                    new FlowPresentationComment(flow!.Id, comment.Text, comment.Language, comment.Source, 1, plan.Context.Now, plan.Context.ActorId))],
                Voices = [.. Voices.Select(voice =>
                    new PresentationVoice(((Project)records[0]).Id, voice.Language, voice.Name, voice.Local, 1, plan.Context.Now, plan.Context.ActorId))],
            };
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

        var project = ProjectCoded(fixture, "VIS");
        Assert.Equal((project.Id, "VIS", (string?)null), (imported.Id, imported.Code, imported.TrashCode));
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
        Assert.Equal(known.Id, ProjectCoded(fixture, "VIS").CreatedBy);
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

    /// <summary>The comments of a package of the format 2.4 (ADR 099) are written beside the steps of the new project, by the importer.</summary>
    [Fact]
    public async Task An_import_writes_the_comments_of_its_steps_and_leaves_out_those_in_a_language_not_configured_here()
    {
        var (fixture, _, import) = Arrange(new FakeImporter { Comments = [(0, "Le client demande un devis.", "fr"), (1, "The quote leaves.", "en"), (1, "Das Angebot.", "de")] });
        fixture.ActingAs(fixture.Project.Admin);

        var imported = await import.ImportAsync([1], null, withMembers: false, Token);

        var steps = fixture.Database.All<Step>().Where(step => fixture.Database.Get<ProcessMap>(ProcessMap.KeyOf(step.MapId)).ProjectId == imported.Id)
            .ToDictionary(step => step.Id);
        var written = fixture.Database.Comments.Values.Where(comment => steps.ContainsKey(comment.StepId)).ToList();
        Assert.Equal([("VIS-E001", "Le client demande un devis.", "fr"), ("VIS-E002", "The quote leaves.", "en")],
            written.Select(comment => (steps[comment.StepId].Code, comment.Text, comment.Language)).Order());
        Assert.All(written, comment => Assert.Equal((1, fixture.Project.Admin.Id), (comment.Version, comment.UpdatedBy)));
    }

    [Theory]
    [InlineData("<b>Gras</b>")]
    [InlineData(" Rogné ")]
    public async Task A_comment_that_its_rule_refuses_refuses_the_package_and_nothing_is_written(string text)
    {
        var (fixture, _, import) = Arrange(new FakeImporter { Comments = [(0, text, "fr")] });
        fixture.ActingAs(fixture.Project.Admin);

        var refusal = await Assert.ThrowsAsync<DomainException>(() => import.ImportAsync([1], null, withMembers: false, Token));

        Assert.Equal((ErrorCodes.InvalidPackage, (object?)"values"), (refusal.Code, refusal.Details["reason"]));
        Assert.Equal(["presentationComment"], (IEnumerable<string>)refusal.Details["fields"]!);
        Assert.DoesNotContain(fixture.Database.All<Project>(), project => project.Code == "VIS");
        Assert.Empty(fixture.Database.Comments);
    }

    [Fact]
    public async Task A_replacement_writes_the_comments_of_the_new_project_and_the_replaced_one_keeps_its_own_in_the_trash()
    {
        var (fixture, _, import) = Arrange(new FakeImporter("NEUF") { Comments = [(1, "Nouvelle phrase.", "fr")] });
        var old = fixture.Project.ProjectId;
        var oldStep = fixture.Project.Step(0);
        fixture.Database.AddComment(new PresentationComment(oldStep, "Ancienne phrase.", "fr", CommentSource.Manual, 2, TestProject.Now, fixture.Project.Facilitator.Id));
        fixture.ActingAs(fixture.Project.Facilitator);

        var imported = await import.ReplaceAsync(old, [1], withMembers: false, version: null, Token);

        Assert.Equal(("Ancienne phrase.", 2), (fixture.Database.Comments[(oldStep, "fr")].Text, fixture.Database.Comments[(oldStep, "fr")].Version));
        var added = Assert.Single(fixture.Database.Comments.Values, comment => comment.StepId != oldStep);
        Assert.Equal((imported.Id, "Nouvelle phrase."),
            (fixture.Database.Get<ProcessMap>(ProcessMap.KeyOf(fixture.Database.Get<Step>(Step.KeyOf(added.StepId)).MapId)).ProjectId, added.Text));
    }

    [Fact]
    public async Task A_package_in_a_language_not_configured_here_is_refused()
    {
        var (fixture, _, import) = Arrange(new FakeImporter { Language = "de" });
        fixture.ActingAs(fixture.Project.Admin);

        var refusal = await Assert.ThrowsAsync<DomainException>(() => import.ImportAsync([1], null, withMembers: true, Token));

        Assert.Equal(ErrorCodes.InvalidLanguage, refusal.Code);
        Assert.DoesNotContain(fixture.Database.All<Project>(), project => project.Code == "VIS");
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

        Assert.Equal(("VIS", "PRJ", "PRJB"), (preview.PackageCode, preview.ProjectCode, preview.TrashCode));
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

    /// <summary>The source of a target state, named by its code in the package (ADR 095, P3d), is linked again only to a live project.</summary>
    [Fact]
    public async Task The_import_links_a_target_state_again_to_the_live_project_of_its_source_code_and_only_to_a_live_one()
    {
        var (fixture, _, import) = Arrange(new FakeImporter { Baseline = "PRJ" });
        fixture.ActingAs(fixture.Project.Admin);

        var linked = await import.ImportAsync([1], null, withMembers: false, Token);
        fixture.Project.PutInTrash();
        var unlinked = await import.ImportAsync([1], "ACH", withMembers: false, Token);

        Assert.Equal(fixture.Project.ProjectId, fixture.Database.Get<Project>(Project.KeyOf(linked.Id)).BaselineProjectId);
        Assert.Null(fixture.Database.Get<Project>(Project.KeyOf(unlinked.Id)).BaselineProjectId);
    }

    [Fact]
    public async Task A_source_code_that_names_no_project_here_leaves_the_target_unlinked()
    {
        var (fixture, _, import) = Arrange(new FakeImporter { Baseline = "ZZZ" });
        fixture.ActingAs(fixture.Project.Admin);

        var imported = await import.ImportAsync([1], null, withMembers: false, Token);

        Assert.Null(fixture.Database.Get<Project>(Project.KeyOf(imported.Id)).BaselineProjectId);
    }

    [Fact]
    public async Task A_replacement_whose_package_names_the_replaced_project_as_its_source_leaves_it_unlinked()
    {
        var (fixture, _, import) = Arrange(new FakeImporter { Baseline = "PRJ" });
        fixture.ActingAs(fixture.Project.Facilitator);

        var imported = await import.ReplaceAsync(fixture.Project.ProjectId, [1], withMembers: false, version: null, Token);

        Assert.Null(fixture.Database.Get<Project>(Project.KeyOf(imported.Id)).BaselineProjectId);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_facilitator_who_replaces_his_project_links_it_only_to_a_source_he_may_see(bool member)
    {
        var (fixture, _, import) = Arrange(new FakeImporter { Baseline = "SRC" });
        var source = fixture.OtherProject("SRC", member ? fixture.Project.Facilitator : fixture.Project.Contributor, MemberRole.Reader);
        fixture.ActingAs(fixture.Project.Facilitator);

        var imported = await import.ReplaceAsync(fixture.Project.ProjectId, [1], withMembers: false, version: null, Token);

        Assert.Equal(member ? source : null, fixture.Database.Get<Project>(Project.KeyOf(imported.Id)).BaselineProjectId);
    }

    /// <summary>F-108 (decision of 07/11/2026): the target states of a replaced source follow it, live or in the trash, seen or not by who replaces it.</summary>
    [Fact]
    public async Task The_target_states_of_a_replaced_project_follow_it_and_the_event_of_the_import_counts_them()
    {
        var (fixture, _, import) = Arrange();
        var old = fixture.Project.ProjectId;
        var live = fixture.OtherProject("CIB", fixture.Project.Contributor, MemberRole.Facilitator);
        var trashed = fixture.OtherProject("COR", fixture.Project.Contributor, MemberRole.Facilitator, inTrash: true);
        var other = fixture.OtherProject("AUT", fixture.Project.Contributor, MemberRole.Facilitator);
        var elsewhere = Guid.CreateVersion7();
        Guid? SourceOf(Guid id) => fixture.Database.Get<Project>(Project.KeyOf(id)).BaselineProjectId;
        void Name(Guid id, Guid source) => fixture.Database.Add(fixture.Database.Get<Project>(Project.KeyOf(id)) with { BaselineProjectId = source });
        Name(live, old);
        Name(trashed, old);
        Name(other, elsewhere);
        fixture.ActingAs(fixture.Project.Facilitator);

        var imported = await import.ReplaceAsync(old, [1], withMembers: false, version: null, Token);

        Assert.Equal(((Guid?)imported.Id, (Guid?)imported.Id, (Guid?)elsewhere), (SourceOf(live), SourceOf(trashed), SourceOf(other)));
        Assert.True(fixture.Database.Get<Project>(Project.KeyOf(trashed)).IsInTrash);
        var audit = Assert.Single(fixture.Database.All<AuditEvent>(), item => item.Kind == AuditKinds.ProjectImported);
        Assert.Equal(2, JsonDocument.Parse(audit.Details!).RootElement.GetProperty("targets_relinked").GetInt32());
    }

    [Fact]
    public async Task A_replacement_without_target_state_says_nothing_of_them_and_a_new_import_moves_no_link()
    {
        var (fixture, _, import) = Arrange();
        var target = fixture.OtherProject("CIB", fixture.Project.Contributor, MemberRole.Facilitator);
        fixture.Database.Add(fixture.Database.Get<Project>(Project.KeyOf(target)) with { BaselineProjectId = fixture.Project.ProjectId });
        fixture.ActingAs(fixture.Project.Admin);

        var imported = await import.ImportAsync([1], null, withMembers: false, Token);

        Assert.Equal(fixture.Project.ProjectId, fixture.Database.Get<Project>(Project.KeyOf(target)).BaselineProjectId);
        var audit = Assert.Single(fixture.Database.All<AuditEvent>(), item => item.Kind == AuditKinds.ProjectImported && item.ProjectId == imported.Id);
        Assert.False(JsonDocument.Parse(audit.Details!).RootElement.TryGetProperty("targets_relinked", out _));
    }
}
