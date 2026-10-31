// PmapExchangeTests.cs
// The .pmap requests (ADR 054): copy with the sheets as the user sees them, opening under the code and name entered, duplication, audit.

using System.Text.Json;
using Topogenia.Application.Audit;
using Topogenia.Application.Exchange;
using Topogenia.Application.Ports;
using Topogenia.Application.Tests.Support;
using Topogenia.Application.Tests.Users;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;
using Topogenia.Domain.Projects;
using Topogenia.Domain.Sheets;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;

namespace Topogenia.Application.Tests.Exchange;

public sealed class PmapExchangeTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private sealed record Document(string? ProjectCode, string ProjectName, IReadOnlyList<string> Uncertainties) : IPmapDocument
    {
        public PmapCounts Counts => new(1, 0, 0, 0, 0);
    }

    /// <summary>A format that records what it was asked: it reads any text as <see cref="File"/> and builds a project of one record.</summary>
    private sealed class RecordingFormat : IPmapFormat
    {
        public Document File { get; set; } = new("VTE", "Ventes", ["À vérifier"]);

        public DomainException? Refusal { get; set; }

        public (string Content, LanguageSettings Languages, string Letters)? ReadWith { get; private set; }

        public (ProjectWorkspace Workspace, Func<Step, SheetViewer>? ViewerOf)? OutlinedWith { get; private set; }

        public (IPmapDocument Document, string Code, string Name)? BuiltWith { get; private set; }

        public IPmapDocument Read(string content, LanguageSettings languages, string raciLetters)
        {
            ReadWith = (content, languages, raciLetters);
            return Refusal is null ? File : throw Refusal;
        }

        public PmapOutlineResult Outline(ProjectWorkspace workspace, Func<Step, SheetViewer>? viewerOf)
        {
            OutlinedWith = (workspace, viewerOf);
            return new PmapOutlineResult(new Document(workspace.Project.Code, workspace.Project.Name, []), DepthCut: true);
        }

        public PmapOutlineResult OutlineMap(ProjectWorkspace workspace, Guid mapId, Func<Step, SheetViewer> viewerOf) =>
            new(new Document(workspace.Project.Code, workspace.Project.Name, []), DepthCut: false);

        public PmapOutlineResult OutlineTree(ProjectWorkspace workspace, Guid mapId) =>
            new(new Document(workspace.Project.Code, workspace.Project.Name, []), DepthCut: false);

        public Domain.Proposals.DesiredMap Desired(IPmapDocument document) => new([], [], []);

        public IPmapDocument Carry(IPmapDocument proposal, ProjectWorkspace origin, Guid mapId, Func<Step, SheetViewer> viewerOf) => proposal;

        public ProjectWorkspace? ModelOf { get; private set; }

        public PmapOutlineResult Model(ProjectWorkspace workspace)
        {
            ModelOf = workspace;
            return new PmapOutlineResult(new Document(workspace.Project.Code, workspace.Project.Name, []), DepthCut: true);
        }

        public string Write(IPmapDocument document, string generatedBy, DateTime generatedAt) => $"{document.ProjectCode} by {generatedBy} at {generatedAt:O}";

        public PmapProjectBuild Build(OperationContext context, IPmapDocument document, string code, string name, ProjectCreationInputs inputs)
        {
            BuiltWith = (document, code, name);
            ProjectCodeRules.EnsureFree(ProjectCode.Parse(code), inputs.ExistingCodes);
            var project = new Project(context.NewId(), code, name, "fr", null, null, Classification.Internal, null, null, 0, context.Now, context.ActorId,
                null, null, null, null, 1);
            return new PmapProjectBuild(project, context.NewId(), new ChangeSet([]), 1, 2, 3, 4, 5);
        }

        public string FileName(Project project, DateTime exportedAt) => $"{project.Code}.pmap";
    }

    private static (UsersFixture Fixture, RecordingFormat Format) Arrange() => (new UsersFixture(), new RecordingFormat());

    private static List<AuditEvent> Events(UsersFixture fixture, string kind) => [.. fixture.Database.All<AuditEvent>().Where(audit => audit.Kind == kind)];

    [Fact]
    public async Task An_opening_takes_the_code_and_the_name_entered_those_of_the_file_otherwise_and_is_audited()
    {
        var (fixture, format) = Arrange();
        fixture.ActingAs(fixture.Project.Admin);

        var entered = await fixture.Runner.RunAsync(new OpenPmapCommand(format, "texte", " ACH ", "  Achats  "), Token);
        Assert.Equal(("ACH", "Achats"), (format.BuiltWith!.Value.Code, format.BuiltWith.Value.Name));
        var fromFile = await fixture.Runner.RunAsync(new OpenPmapCommand(format, "texte", null, "   "), Token);

        Assert.Equal(("ACH", 1, 2, 3, 4, 5), (entered.Code, entered.Maps, entered.Lanes, entered.Steps, entered.Flows, entered.Sheets));
        Assert.Equal(["À vérifier"], entered.Uncertainties);
        Assert.Equal(("VTE", "Ventes"), (format.BuiltWith!.Value.Code, format.BuiltWith.Value.Name));
        Assert.Equal("VTE", fromFile.Code);
        Assert.Equal(("texte", "ACI"), (format.ReadWith!.Value.Content, format.ReadWith.Value.Letters));
        Assert.Equal(new[] { entered.Id, fromFile.Id }.Order(), fixture.Database.All<Project>().Where(project => project.Code is "ACH" or "VTE").Select(project => project.Id).Order());
        var audits = Events(fixture, AuditKinds.ProjectImported);
        Assert.Equal(2, audits.Count);
        Assert.All(audits, audit => Assert.Equal("""{"mode":"pmap"}""", audit.Details));
    }

    [Fact]
    public async Task A_content_longer_than_eight_million_characters_is_refused_before_it_is_read()
    {
        var (fixture, format) = Arrange();
        fixture.ActingAs(fixture.Project.Admin);

        var refusal = await Assert.ThrowsAsync<DomainException>(() => fixture.Runner.RunAsync(
            new OpenPmapCommand(format, new string(' ', OpenPmapCommand.ContentMax + 1), null, null), Token));
        await fixture.Runner.RunAsync(new OpenPmapCommand(format, new string(' ', OpenPmapCommand.ContentMax), null, null), Token);

        Assert.Equal((ErrorCodes.FileTooLarge, (object?)8), (refusal.Code, refusal.Details["max"]));
        Assert.Equal(OpenPmapCommand.ContentMax, format.ReadWith!.Value.Content.Length);
    }

    [Fact]
    public async Task A_file_with_faults_and_a_code_in_use_write_nothing()
    {
        var (fixture, format) = Arrange();
        fixture.ActingAs(fixture.Project.Admin);
        var before = fixture.Database.All<IRecord>().Count();
        format.Refusal = new DomainException(ErrorCodes.InvalidFile, RefusalKind.Invalid);

        var invalid = await Assert.ThrowsAsync<DomainException>(() => fixture.Runner.RunAsync(new OpenPmapCommand(format, "x", null, null), Token));
        format.Refusal = null;
        var taken = await Assert.ThrowsAsync<DomainException>(() => fixture.Runner.RunAsync(new OpenPmapCommand(format, "x", "PRJ", null), Token));

        Assert.Equal((ErrorCodes.InvalidFile, ErrorCodes.ProjectCodeTaken), (invalid.Code, taken.Code));
        Assert.Equal(before, fixture.Database.All<IRecord>().Count());
    }

    [Fact]
    public async Task A_duplication_takes_the_model_of_the_source_reports_its_cut_and_names_the_source_in_its_audit_event()
    {
        var (fixture, format) = Arrange();
        fixture.ActingAs(fixture.Project.Admin);

        var copy = await fixture.Runner.RunAsync(new DuplicateProjectCommand(format, fixture.Project.ProjectId, "COPIE", null), Token);

        Assert.Equal(fixture.Project.ProjectId, format.ModelOf!.ProjectId);
        Assert.Null(format.OutlinedWith);
        Assert.True(copy.DepthCut);
        Assert.Equal(("COPIE", fixture.Project.Project.Name), (format.BuiltWith!.Value.Code, format.BuiltWith.Value.Name));
        var audit = Assert.Single(Events(fixture, AuditKinds.ProjectDuplicated));
        Assert.Equal((copy.Id, JsonSerializer.Serialize(new { source = fixture.Project.ProjectId })), (audit.ProjectId!.Value, audit.Details));
    }

    [Fact]
    public async Task A_project_in_the_trash_is_not_duplicated()
    {
        var (fixture, format) = Arrange();
        fixture.ActingAs(fixture.Project.Admin);
        fixture.Project.PutInTrash();

        var refusal = await Assert.ThrowsAsync<DomainException>(() => fixture.Runner.RunAsync(new DuplicateProjectCommand(format, fixture.Project.ProjectId, "COPIE", null), Token));

        Assert.Equal(ErrorCodes.NotFound, refusal.Code);
        Assert.Null(format.ModelOf);
    }

    [Theory]
    [InlineData("reader", false)]
    [InlineData("facilitator", true)]
    public async Task The_copy_gives_each_sheet_as_the_user_sees_it_and_is_audited(string who, bool seesDrafts)
    {
        var (fixture, format) = Arrange();
        fixture.ActingAs(who == "reader" ? fixture.Project.Reader : fixture.Project.Facilitator);
        var step = fixture.Project.Step(0);
        var export = new PmapCopyExport(fixture.Queries, format, new InMemoryAuditStore(fixture.Database), fixture.Project.Clock);

        var copy = await export.RunAsync(fixture.Project.ProjectId, withSheets: true, Token);

        var (workspace, viewerOf) = format.OutlinedWith!.Value;
        Assert.Equal(seesDrafts, viewerOf!(workspace.LiveStep(step)).SeesDraft);
        Assert.Equal(("PRJ.pmap", true), (copy.FileName, copy.DepthCut));
        Assert.Equal($"PRJ by Topogenia at {TestProject.Now:O}", copy.Content);
        var audit = Assert.Single(Events(fixture, AuditKinds.ProjectExported));
        Assert.Equal("""{"format":"pmap","with_sheets":true}""", audit.Details);
    }

    [Fact]
    public async Task Without_the_option_the_copy_asks_for_no_sheet()
    {
        var (fixture, format) = Arrange();
        fixture.ActingAs(fixture.Project.Reader);
        var export = new PmapCopyExport(fixture.Queries, format, new InMemoryAuditStore(fixture.Database), fixture.Project.Clock);

        await export.RunAsync(fixture.Project.ProjectId, withSheets: false, Token);

        Assert.Null(format.OutlinedWith!.Value.ViewerOf);
        Assert.Equal("""{"format":"pmap","with_sheets":false}""", Assert.Single(Events(fixture, AuditKinds.ProjectExported)).Details);
    }
}
