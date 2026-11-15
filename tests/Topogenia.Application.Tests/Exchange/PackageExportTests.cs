// PackageExportTests.cs
// The export of a project through the query template (spec 7.2, 5.5, AR-113): its right, the logins it reads, its audit event.

using System.Text.Json;
using Topogenia.Application.Audit;
using Topogenia.Application.Exchange;
using Topogenia.Application.Ports;
using Topogenia.Application.Presentation;
using Topogenia.Application.Tests.Coverage;
using Topogenia.Application.Tests.Support;
using Topogenia.Application.Tests.Users;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;

namespace Topogenia.Application.Tests.Exchange;

public sealed class PackageExportTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A writer that records what it was asked and names two users of the project.</summary>
    private sealed class RecordingExporter(params Guid[] named) : IPackageExporter
    {
        public PackageRequest? Request { get; private set; }

        public IReadOnlySet<Guid> UsersNamedBy(ProjectWorkspace workspace) => named.ToHashSet();

        public byte[] Write(PackageRequest request)
        {
            Request = request;
            return [1, 2, 3];
        }

        public string FileName(Project project, DateTime exportedAt) => $"{project.Code}_{exportedAt:yyyy-MM-dd}.package";

        public IReadOnlyList<PackageText> Describe(ProjectWorkspace workspace) => [new("project.json", "{}")];
    }

    private static (UsersFixture Fixture, RecordingExporter Exporter, CountingUserReader Users, PackageExport Export) Arrange()
    {
        var fixture = new UsersFixture();
        var exporter = new RecordingExporter(fixture.Project.Facilitator.Id, fixture.Project.Contributor.Id);
        var users = new CountingUserReader(fixture.Users);
        var export = new PackageExport(fixture.Queries, exporter, users, fixture.Projects, new InMemoryAuditStore(fixture.Database), fixture.Project.Clock,
            fixture.Project.CurrentUser, fixture.Comments, fixture.FlowComments, fixture.Voices);
        return (fixture, exporter, users, export);
    }

    /// <summary>
    /// The voices the project keeps go to the writer with the workspace (ADR 103), with or without the people: a voice is
    /// no person. Those of another project stay with it.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task The_export_hands_the_writer_the_voices_of_the_project_only_with_or_without_the_people(bool withoutPeople)
    {
        var (fixture, exporter, _, export) = Arrange();
        var french = new PresentationVoice(fixture.Project.ProjectId, "fr", "Hortense", true, 2, TestProject.Now, fixture.Project.Facilitator.Id);
        var english = french with { Language = "en", Name = "Julie", Local = false, Version = 1 };
        var other = fixture.OtherProject("AUT", fixture.Project.Facilitator, MemberRole.Facilitator);
        fixture.Database.AddVoice(french);
        fixture.Database.AddVoice(english);
        fixture.Database.AddVoice(french with { ProjectId = other, Name = "Autre projet" });
        fixture.ActingAs(fixture.Project.Facilitator);

        await export.RunAsync(fixture.Project.ProjectId, withoutPeople, Token);

        Assert.Equal([english, french], exporter.Request!.Voices.OrderBy(voice => voice.Language, StringComparer.Ordinal));
        Assert.Empty(exporter.Request.Comments);
    }

    /// <summary>The comments of presentation of the live steps go to the writer with the workspace (ADR 099); a deleted step keeps its own.</summary>
    [Fact]
    public async Task The_export_hands_the_writer_the_comments_of_the_live_steps_only()
    {
        var (fixture, exporter, _, export) = Arrange();
        var live = fixture.Project.Step(0);
        var deleted = fixture.Project.Step(1);
        fixture.Database.Add(fixture.Database.Get<Step>(Step.KeyOf(deleted)) with { DeletedAt = TestProject.Now, DeletionBatchId = fixture.Project.NextId() });
        var said = new PresentationComment(live, "À dire.", "fr", CommentSource.Manual, 2, TestProject.Now, fixture.Project.Facilitator.Id);
        fixture.Database.AddComment(said);
        fixture.Database.AddComment(said with { StepId = deleted, Text = "Étape supprimée." });
        fixture.ActingAs(fixture.Project.Facilitator);

        await export.RunAsync(fixture.Project.ProjectId, withoutPeople: true, Token);

        Assert.Equal([said], exporter.Request!.Comments);
        Assert.Empty(exporter.Request.FlowComments);
    }

    /// <summary>The comments of presentation of the live flows go to the writer too (ADR 099, lot J10); a deleted flow keeps its own.</summary>
    [Fact]
    public async Task The_export_hands_the_writer_the_comments_of_the_live_flows_only()
    {
        var (fixture, exporter, _, export) = Arrange();
        var (first, second, third) = (fixture.Project.Step(0), fixture.Project.Step(1), fixture.Project.Step(2));
        var live = fixture.Project.Flow(first, second);
        var deleted = fixture.Project.Flow(second, third);
        fixture.Database.Add(fixture.Database.Get<Flow>(Flow.KeyOf(deleted)) with { DeletedAt = TestProject.Now, DeletionBatchId = fixture.Project.NextId() });
        var said = new FlowPresentationComment(live, "À dire pendant le passage.", "fr", CommentSource.Manual, 2, TestProject.Now, fixture.Project.Facilitator.Id);
        var translated = said with { Language = "en", Text = "To say along the flow.", Source = CommentSource.Assistant };
        fixture.Database.AddFlowComment(said);
        fixture.Database.AddFlowComment(translated);
        fixture.Database.AddFlowComment(said with { FlowId = deleted, Text = "Flèche supprimée." });
        fixture.ActingAs(fixture.Project.Facilitator);

        await export.RunAsync(fixture.Project.ProjectId, withoutPeople: true, Token);

        Assert.Equal([translated, said], exporter.Request!.FlowComments.OrderBy(comment => comment.Language, StringComparer.Ordinal));
        Assert.Empty(exporter.Request.Comments);
    }

    [Fact]
    public async Task The_facilitator_exports_the_whole_project_with_the_logins_it_names_and_the_export_is_audited()
    {
        var (fixture, exporter, users, export) = Arrange();
        fixture.ActingAs(fixture.Project.Facilitator);

        var package = await export.RunAsync(fixture.Project.ProjectId, withoutPeople: false, Token);

        Assert.Equal([1, 2, 3], package.Content);
        Assert.Equal("PRJ_2026-10-24.package", package.FileName);
        var request = exporter.Request!;
        Assert.Equal(fixture.Project.ProjectId, request.Workspace.ProjectId);
        Assert.NotEmpty(request.Workspace.All<ProjectMember>());
        Assert.Equal(new Dictionary<Guid, string>
        {
            [fixture.Project.Facilitator.Id] = fixture.Project.Facilitator.Login,
            [fixture.Project.Contributor.Id] = fixture.Project.Contributor.Login,
        }, request.Logins);
        Assert.False(request.WithoutPeople);
        Assert.Equal((fixture.Project.Clock.UtcNow, fixture.Project.Facilitator.Login), (request.ExportedAt, request.ExportedBy));
        Assert.Equal(1, users.ByIds);
        var audit = Assert.Single(fixture.Database.All<AuditEvent>());
        Assert.Equal((AuditKinds.ProjectExported, fixture.Project.Facilitator.Id, fixture.Project.ProjectId), (audit.Kind, audit.UserId, audit.ProjectId));
        Assert.False(JsonDocument.Parse(audit.Details!).RootElement.GetProperty("without_people").GetBoolean());
    }

    [Fact]
    public async Task Without_the_people_no_user_is_read_and_no_login_is_handed_to_the_writer()
    {
        var (fixture, exporter, users, export) = Arrange();
        fixture.ActingAs(fixture.Project.Facilitator);

        await export.RunAsync(fixture.Project.ProjectId, withoutPeople: true, Token);

        Assert.Empty(exporter.Request!.Logins);
        Assert.True(exporter.Request.WithoutPeople);
        Assert.Equal(0, users.ByIds);
        Assert.True(JsonDocument.Parse(Assert.Single(fixture.Database.All<AuditEvent>()).Details!).RootElement.GetProperty("without_people").GetBoolean());
    }

    [Fact]
    public async Task An_administrator_exports_as_a_facilitator()
    {
        var (fixture, exporter, _, export) = Arrange();
        fixture.ActingAs(fixture.Project.Admin);

        await export.RunAsync(fixture.Project.ProjectId, withoutPeople: true, Token);

        Assert.NotNull(exporter.Request);
        Assert.Equal(fixture.Project.Admin.Id, Assert.Single(fixture.Database.All<AuditEvent>()).UserId);
    }

    [Theory]
    [InlineData("contributor", ErrorCodes.AccessDenied)]
    [InlineData("reader", ErrorCodes.AccessDenied)]
    [InlineData("stranger", ErrorCodes.NotFound)]
    public async Task Only_the_facilitator_exports_a_project_and_a_refused_export_writes_nothing(string who, string code)
    {
        var (fixture, exporter, users, export) = Arrange();
        fixture.ActingAs(who switch
        {
            "contributor" => fixture.Project.Contributor,
            "reader" => fixture.Project.Reader,
            _ => fixture.Project.Stranger,
        });

        var refusal = await Assert.ThrowsAsync<DomainException>(() => export.RunAsync(fixture.Project.ProjectId, withoutPeople: false, Token));

        Assert.Equal(code, refusal.Code);
        Assert.Null(exporter.Request);
        Assert.Equal(0, users.ByIds);
        Assert.Empty(fixture.Database.All<AuditEvent>());
    }

    [Fact]
    public async Task A_project_in_the_trash_is_not_exported()
    {
        var (fixture, exporter, _, export) = Arrange();
        fixture.ActingAs(fixture.Project.Facilitator);
        fixture.Project.PutInTrash();

        var refusal = await Assert.ThrowsAsync<DomainException>(() => export.RunAsync(fixture.Project.ProjectId, withoutPeople: true, Token));

        Assert.Equal(ErrorCodes.NotFound, refusal.Code);
        Assert.Null(exporter.Request);
    }

    /// <summary>A target state names its source by code (ADR 095, P3d): only a live source that the exporter may see.</summary>
    [Theory]
    [InlineData("member", "SRC")]
    [InlineData("stranger", null)]
    [InlineData("trash", null)]
    public async Task The_package_of_a_target_state_names_its_source_only_when_it_is_live_and_seen_by_the_exporter(string source, string? expected)
    {
        var (fixture, exporter, _, export) = Arrange();
        var sourceId = fixture.OtherProject("SRC", source == "stranger" ? fixture.Project.Contributor : fixture.Project.Facilitator, MemberRole.Reader,
            inTrash: source == "trash");
        fixture.Database.Add(fixture.Project.Project with { BaselineProjectId = sourceId });
        fixture.ActingAs(fixture.Project.Facilitator);

        await export.RunAsync(fixture.Project.ProjectId, withoutPeople: true, Token);

        Assert.Equal(expected, exporter.Request!.BaselineProjectCode);
    }

    [Fact]
    public async Task The_package_of_a_project_that_is_no_target_state_names_no_source()
    {
        var (fixture, exporter, _, export) = Arrange();
        fixture.ActingAs(fixture.Project.Facilitator);

        await export.RunAsync(fixture.Project.ProjectId, withoutPeople: false, Token);

        Assert.Null(exporter.Request!.BaselineProjectCode);
    }
}
