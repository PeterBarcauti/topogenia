// PmapExchangeTests.cs
// The .pmap requests (ADR 054): copy with the sheets as the user sees them, opening under the code and name entered, duplication, audit.

using System.Text.Json;
using Topogenia.Application.Audit;
using Topogenia.Application.Exchange;
using Topogenia.Application.Ports;
using Topogenia.Application.Presentation;
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

        public int RemoteLocations { get; init; }
    }

    /// <summary>A format that records what it was asked: it reads any text as <see cref="File"/> and builds a project of one record.</summary>
    private sealed class RecordingFormat : IPmapFormat
    {
        public Document File { get; set; } = new("VIS", "Visites", ["À vérifier"]);

        public DomainException? Refusal { get; set; }

        public (string Content, LanguageSettings Languages, string Letters)? ReadWith { get; private set; }

        public (ProjectWorkspace Workspace, Func<Step, SheetViewer>? ViewerOf)? OutlinedWith { get; private set; }

        public (IPmapDocument Document, string Code, string Name)? BuiltWith { get; private set; }

        public IPmapDocument Read(string content, LanguageSettings languages, string raciLetters)
        {
            ReadWith = (content, languages, raciLetters);
            ReadAsProposal = false;
            return Refusal is null ? File : throw Refusal;
        }

        /// <summary>Whether the last read was the one of a proposal (ADR 091: its routes are not read).</summary>
        public bool ReadAsProposal { get; private set; }

        public IPmapDocument ReadProposal(string content, LanguageSettings languages, string raciLetters)
        {
            var document = Read(content, languages, raciLetters);
            ReadAsProposal = true;
            return document;
        }

        // The insertion of a file (3.3, F-208) goes through the whole host (`PmapInsertionTests`): this double never reads one.
        public PmapInsertionFile ReadInsertion(string content, LanguageSettings languages, string raciLetters) => throw new NotSupportedException();

        // A collection (ADR 114) is read by the interchange itself (`PmapCollectionReaderTests`): this double never reads one.
        public PmapCollection ReadCollection(string content, LanguageSettings languages, string raciLetters) => throw new NotSupportedException();

        public PmapDetailBuild Detail(OperationContext context, ProjectWorkspace workspace, IPmapDocument document, Guid stepId, LanguageSettings languages,
            bool withSheets) => throw new NotSupportedException();

        /// <summary>What the last copy, copy of a map or duplication handed besides the structure (1.5).</summary>
        public PmapExtras? ExtrasGiven { get; private set; }

        /// <summary>The comments of presentation the build gives to the new steps and flows.</summary>
        public PmapProjectBuild? Extras { get; set; }

        public PmapOutlineResult Outline(ProjectWorkspace workspace, Func<Step, SheetViewer>? viewerOf, PmapExtras? extras)
        {
            OutlinedWith = (workspace, viewerOf);
            ExtrasGiven = extras;
            return new PmapOutlineResult(new Document(workspace.Project.Code, workspace.Project.Name, []), DepthCut: true);
        }

        public PmapOutlineResult OutlineMap(ProjectWorkspace workspace, Guid mapId, Func<Step, SheetViewer> viewerOf) =>
            new(new Document(workspace.Project.Code, workspace.Project.Name, []), DepthCut: false);

        public PmapOutlineResult OutlineTree(ProjectWorkspace workspace, Guid mapId) =>
            new(new Document(workspace.Project.Code, workspace.Project.Name, []), DepthCut: false);

        public (ProjectWorkspace Workspace, Guid MapId, Func<Step, SheetViewer> ViewerOf)? BranchOf { get; private set; }

        public PmapOutlineResult OutlineBranch(ProjectWorkspace workspace, Guid mapId, Func<Step, SheetViewer> viewerOf, PmapExtras? extras)
        {
            workspace.LiveMap(mapId);
            BranchOf = (workspace, mapId, viewerOf);
            ExtrasGiven = extras;
            return new PmapOutlineResult(new Document(workspace.Project.Code, workspace.Project.Name, []), DepthCut: false);
        }

        public (ProjectWorkspace Workspace, Guid MapId)? ViewOf { get; private set; }

        public PmapOutlineResult OutlineView(ProjectWorkspace workspace, Guid mapId, Func<Step, SheetViewer> viewerOf)
        {
            workspace.LiveMap(mapId);
            ViewOf = (workspace, mapId);
            return new PmapOutlineResult(new Document(workspace.Project.Code, workspace.Project.Name, []), DepthCut: true);
        }

        public (ProjectWorkspace Workspace, Guid MapId, Func<Step, SheetViewer> ViewerOf)? PrintOf { get; private set; }

        public PmapOutlineResult OutlinePrint(ProjectWorkspace workspace, Guid mapId, Func<Step, SheetViewer> viewerOf)
        {
            workspace.LiveMap(mapId);
            PrintOf = (workspace, mapId, viewerOf);
            return new PmapOutlineResult(new Document(workspace.Project.Code, workspace.Project.Name, []), DepthCut: true);
        }

        public Domain.Proposals.DesiredMap Desired(IPmapDocument document) => new([], [], []);

        public IPmapDocument Carry(IPmapDocument proposal, ProjectWorkspace origin, Guid mapId, Func<Step, SheetViewer> viewerOf) => proposal;

        public ProjectWorkspace? ModelOf { get; private set; }

        public PmapOutlineResult Model(ProjectWorkspace workspace, PmapExtras? extras)
        {
            ModelOf = workspace;
            ExtrasGiven = extras;
            return new PmapOutlineResult(new Document(workspace.Project.Code, workspace.Project.Name, []), DepthCut: true);
        }

        public string Write(IPmapDocument document, string generatedBy, DateTime generatedAt) => $"{document.ProjectCode} by {generatedBy} at {generatedAt:O}";

        public PmapProjectBuild Build(OperationContext context, IPmapDocument document, string code, string name, ProjectCreationInputs inputs)
        {
            BuiltWith = (document, code, name);
            ProjectCodeRules.EnsureFree(ProjectCode.Parse(code), inputs.ExistingCodes);
            var project = new Project(context.NewId(), code, name, "fr", null, null, Classification.Internal, null, null, 0, context.Now, context.ActorId,
                null, null, null, null, 1);
            return new PmapProjectBuild(project, context.NewId(), new ChangeSet([]), 1, 2, 3, 4, 5)
            {
                Comments = Extras?.Comments ?? [], FlowComments = Extras?.FlowComments ?? [],
            };
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
        Assert.Equal(("VIS", "Visites"), (format.BuiltWith!.Value.Code, format.BuiltWith.Value.Name));
        Assert.Equal("VIS", fromFile.Code);
        // Every letter of the RACI opens, whatever the setting (T-FICR-09 revised on 16/11/2026).
        Assert.Equal(("texte", "ASCI"), (format.ReadWith!.Value.Content, format.ReadWith.Value.Letters));
        // A file keeps the routes chosen; only a proposal of the assistant is read without them (ADR 091).
        Assert.False(format.ReadAsProposal);
        Assert.Equal(new[] { entered.Id, fromFile.Id }.Order(), fixture.Database.All<Project>().Where(project => project.Code is "ACH" or "VIS").Select(project => project.Id).Order());
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
        var export = new PmapCopyExport(fixture.Queries, format, new InMemoryAuditStore(fixture.Database), fixture.Project.Clock, fixture.Comments, fixture.FlowComments);

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
        var export = new PmapCopyExport(fixture.Queries, format, new InMemoryAuditStore(fixture.Database), fixture.Project.Clock, fixture.Comments, fixture.FlowComments);

        await export.RunAsync(fixture.Project.ProjectId, withSheets: false, Token);

        Assert.Null(format.OutlinedWith!.Value.ViewerOf);
        Assert.Equal("""{"format":"pmap","with_sheets":false}""", Assert.Single(Events(fixture, AuditKinds.ProjectExported)).Details);
    }

    [Theory]
    [InlineData("reader", false)]
    [InlineData("facilitator", true)]
    public async Task The_copy_of_a_map_gives_its_branch_each_sheet_as_the_user_sees_it_and_names_the_map_in_its_audit_event(string who, bool seesDrafts)
    {
        var (fixture, format) = Arrange();
        fixture.ActingAs(who == "reader" ? fixture.Project.Reader : fixture.Project.Facilitator);
        var map = fixture.Project.RootMapId;
        var step = fixture.Project.Step(0);
        var export = new MapPmapExport(fixture.Queries, format, new InMemoryAuditStore(fixture.Database), fixture.Project.Clock, fixture.Comments, fixture.FlowComments);

        var copy = await export.RunAsync(map, MapCopyPurpose.Copy, Token);

        var (workspace, mapId, viewerOf) = format.BranchOf!.Value;
        Assert.Equal(map, mapId);
        Assert.Equal(seesDrafts, viewerOf(workspace.LiveStep(step)).SeesDraft);
        Assert.Equal(("PRJ.pmap", false), (copy.FileName, copy.DepthCut));
        Assert.Equal($"PRJ by Topogenia at {TestProject.Now:O}", copy.Content);
        var audit = Assert.Single(Events(fixture, AuditKinds.ProjectExported));
        Assert.Equal((fixture.Project.ProjectId, JsonSerializer.Serialize(new { format = "pmap", scope = "map", map, with_sheets = true })), (audit.ProjectId!.Value, audit.Details));
    }

    [Fact]
    public async Task The_copy_of_a_map_is_refused_to_a_stranger_and_for_a_map_unknown_and_writes_no_event()
    {
        var (fixture, format) = Arrange();
        var export = new MapPmapExport(fixture.Queries, format, new InMemoryAuditStore(fixture.Database), fixture.Project.Clock, fixture.Comments, fixture.FlowComments);
        fixture.ActingAs(fixture.Project.Stranger);

        var stranger = await Assert.ThrowsAnyAsync<DomainException>(() => export.RunAsync(fixture.Project.RootMapId, MapCopyPurpose.Copy, Token));
        fixture.ActingAs(fixture.Project.Reader);
        var unknown = await Assert.ThrowsAnyAsync<DomainException>(() => export.RunAsync(Guid.NewGuid(), MapCopyPurpose.Copy, Token));

        Assert.Equal(ErrorCodes.NotFound, unknown.Code);
        Assert.Contains(stranger.Code, new[] { ErrorCodes.NotFound, ErrorCodes.AccessDenied });
        Assert.Null(format.BranchOf);
        Assert.Empty(Events(fixture, AuditKinds.ProjectExported));
    }

    /// <summary>
    /// The copy of a map read for the view in BPMN 2.0 writes no event (a consultation, as the map itself); read for the
    /// export of a .bpmn it writes the event of an export with the format `bpmn` (ADR 106).
    /// </summary>
    [Fact]
    public async Task The_copy_of_a_map_read_for_the_view_writes_no_event_and_for_the_bpmn_names_its_format()
    {
        var (fixture, format) = Arrange();
        fixture.ActingAs(fixture.Project.Reader);
        var map = fixture.Project.RootMapId;
        var export = new MapPmapExport(fixture.Queries, format, new InMemoryAuditStore(fixture.Database), fixture.Project.Clock, fixture.Comments, fixture.FlowComments);

        var viewed = await export.RunAsync(map, MapCopyPurpose.View, Token);
        Assert.Empty(Events(fixture, AuditKinds.ProjectExported));
        // Without event, the view reads no more than the map shows, and names no file to keep (audit of the copy 64, P2-03).
        Assert.Equal((map, (Guid?)null), (format.ViewOf?.MapId, format.BranchOf?.MapId));
        Assert.Equal(((string?)null, false), (viewed.FileName, viewed.DepthCut));
        await export.RunAsync(map, MapCopyPurpose.Bpmn, Token);

        Assert.Equal($"PRJ by Topogenia at {TestProject.Now:O}", viewed.Content);
        var audit = Assert.Single(Events(fixture, AuditKinds.ProjectExported));
        Assert.Equal(JsonSerializer.Serialize(new { format = "bpmn", scope = "map", map, with_sheets = true }), audit.Details);
    }

    /// <summary>A comment on a step and one on a flow of the project of the fixture, in French.</summary>
    private static (PresentationComment Step, FlowPresentationComment Flow) Said(UsersFixture fixture)
    {
        var first = fixture.Project.Step(0);
        var second = fixture.Project.Step(1);
        var flow = fixture.Project.Flow(first, second);
        var step = new PresentationComment(first, "On reçoit la demande.", "fr", CommentSource.Manual, 1, TestProject.Now, fixture.Project.Facilitator.Id);
        var onFlow = new FlowPresentationComment(flow, "Elle passe à l'acheteur.", "fr", CommentSource.Manual, 1, TestProject.Now, fixture.Project.Facilitator.Id);
        fixture.Database.AddComment(step);
        fixture.Database.AddFlowComment(onFlow);
        return (step, onFlow);
    }

    [Fact]
    public async Task The_copies_and_the_duplication_hand_the_comments_of_the_live_steps_and_flows_to_the_format()
    {
        var (fixture, format) = Arrange();
        var (step, flow) = Said(fixture);
        fixture.ActingAs(fixture.Project.Admin);

        await new PmapCopyExport(fixture.Queries, format, new InMemoryAuditStore(fixture.Database), fixture.Project.Clock, fixture.Comments, fixture.FlowComments)
            .RunAsync(fixture.Project.ProjectId, withSheets: true, Token);
        var copied = format.ExtrasGiven!;
        await new MapPmapExport(fixture.Queries, format, new InMemoryAuditStore(fixture.Database), fixture.Project.Clock, fixture.Comments, fixture.FlowComments)
            .RunAsync(fixture.Project.RootMapId, MapCopyPurpose.Copy, Token);
        var branch = format.ExtrasGiven!;
        await fixture.Runner.RunAsync(new DuplicateProjectCommand(format, fixture.Project.ProjectId, "COPIE", null), Token);
        var model = format.ExtrasGiven!;

        Assert.All(new[] { copied, branch, model }, extras =>
        {
            Assert.Equal(step, Assert.Single(extras.StepComments));
            Assert.Equal(flow, Assert.Single(extras.FlowComments));
        });
    }

    [Fact]
    public async Task An_opening_writes_beside_the_new_project_the_comments_its_build_gives_and_nothing_when_it_is_refused()
    {
        var (fixture, format) = Arrange();
        fixture.ActingAs(fixture.Project.Admin);
        var newStep = Guid.NewGuid();
        var newFlow = Guid.NewGuid();
        format.Extras = new PmapProjectBuild(null!, Guid.Empty, new ChangeSet([]), 0, 0, 0, 0, 0)
        {
            Comments = [new PresentationComment(newStep, "Bonjour.", "fr", CommentSource.Manual, 1, TestProject.Now, fixture.Project.Admin.Id)],
            FlowComments = [new FlowPresentationComment(newFlow, "Ensuite.", "fr", CommentSource.Manual, 1, TestProject.Now, fixture.Project.Admin.Id)],
        };

        await fixture.Runner.RunAsync(new OpenPmapCommand(format, "texte", "OUV", null), Token);
        Assert.Equal("Bonjour.", fixture.Database.CommentsOf(newStep)["fr"].Text);
        Assert.Equal("Ensuite.", fixture.Database.FlowCommentsOf(newFlow)["fr"].Text);

        // A code in use refuses the opening: nothing of it stays, its comments neither.
        var other = Guid.NewGuid();
        format.Extras = format.Extras with { Comments = [new PresentationComment(other, "Non.", "fr", CommentSource.Manual, 1, TestProject.Now, fixture.Project.Admin.Id)] };
        await Assert.ThrowsAsync<DomainException>(() => fixture.Runner.RunAsync(new OpenPmapCommand(format, "texte", "PRJ", null), Token));
        Assert.Empty(fixture.Database.CommentsOf(other));
    }

    private static MapPmapExport Export(UsersFixture fixture, RecordingFormat format) =>
        new(fixture.Queries, format, new InMemoryAuditStore(fixture.Database), fixture.Project.Clock, fixture.Comments, fixture.FlowComments);

    /// <summary>A sheet of the step with the purpose <paramref name="draft"/> in its draft, validated before with <paramref name="validated"/> unless null.</summary>
    private static void Sheet(TestProject project, Guid stepId, string? validated, string draft)
    {
        var sheetId = project.NextId();
        project.Database.Add(new StepSheet(sheetId, stepId, SheetStatus.Draft, null, draft, null, null, null, null, "fr", null, TestProject.Now,
            project.Facilitator.Id, null, null, 1));
        if (validated is not null)
            project.Database.Add(new StepSheetVersion(project.NextId(), sheetId, 1, SheetJson.Freeze(new SheetContent(null, validated, null, null, null, [], null)),
                null, null, project.Facilitator.Id, TestProject.Now));
    }

    /// <summary>The purpose of the sheet of a step as the viewer the copy for a pdf handed to the format sees it, null without content.</summary>
    private static string? PurposeSeen(RecordingFormat format, Guid stepId)
    {
        var (workspace, _, viewerOf) = format.PrintOf!.Value;
        return SheetVisibility.VisibleContent(workspace, stepId, viewerOf(workspace.LiveStep(stepId)))?.Purpose;
    }

    /// <summary>
    /// The copy the project as PDF reads (3.4, lot 5, ADR 113), from a detailed map: the branch of that map asked of the
    /// format for the print, each sheet as the user sees it, no file named (the text is a source, not a file to keep), its
    /// event of export with the format `pdf` written before the text is handed out.
    /// </summary>
    [Fact]
    public async Task The_copy_for_a_pdf_is_the_branch_with_the_sheets_as_the_user_sees_them_and_its_event_says_pdf()
    {
        var (fixture, format) = Arrange();
        var project = fixture.Project;
        var (subMap, subLane) = project.SubMap(project.Step(0));
        var step = project.Step(0, laneId: subLane, mapId: subMap);
        Sheet(project, step, "Version 1", "Version 2");
        fixture.ActingAs(project.Facilitator);

        var copy = await Export(fixture, format).RunAsync(subMap, MapCopyPurpose.Pdf, Token);

        Assert.Equal((subMap, (Guid?)null, (Guid?)null), (format.PrintOf?.MapId, format.BranchOf?.MapId, format.ViewOf?.MapId));
        Assert.Equal("Version 2", PurposeSeen(format, step));
        Assert.Equal(($"PRJ by Topogenia at {TestProject.Now:O}", (string?)null), (copy.Content, copy.FileName));
        var audit = Assert.Single(Events(fixture, AuditKinds.ProjectExported));
        Assert.Equal((project.ProjectId, project.Facilitator.Id, JsonSerializer.Serialize(new { format = "pdf", scope = "map", map = subMap, with_sheets = true })),
            (audit.ProjectId!.Value, audit.UserId!.Value, audit.Details));
    }

    /// <summary>
    /// The print leaves out what the PDF never prints (ADR 113, arbitration 9): the format is asked for the print, whose
    /// copy names no place of a document (held by the tests of the format), and no comment of presentation is read for it.
    /// </summary>
    [Fact]
    public async Task The_copy_for_a_pdf_names_no_location_and_carries_no_comment_of_presentation()
    {
        var (fixture, format) = Arrange();
        Said(fixture);
        fixture.ActingAs(fixture.Project.Admin);

        await Export(fixture, format).RunAsync(fixture.Project.RootMapId, MapCopyPurpose.Pdf, Token);

        Assert.Equal((fixture.Project.RootMapId, (Guid?)null), (format.PrintOf?.MapId, format.BranchOf?.MapId));
        Assert.Null(format.ExtrasGiven);
    }

    [Fact]
    public async Task A_reader_gets_the_latest_validated_version_never_the_draft_and_no_sheet_never_validated()
    {
        var (fixture, format) = Arrange();
        var project = fixture.Project;
        var (validated, never) = (project.Step(0, assignee: project.Reader.Id), project.Step(1));
        Sheet(project, validated, "Version 1", "Version 2");
        Sheet(project, never, null, "Brouillon");
        fixture.ActingAs(project.Reader);

        await Export(fixture, format).RunAsync(project.RootMapId, MapCopyPurpose.Pdf, Token);

        Assert.Equal(("Version 1", (string?)null), (PurposeSeen(format, validated), PurposeSeen(format, never)));
    }

    /// <summary>The contributor fills the step assigned to him and the steps of the lane he holds (AR-01): their drafts; the validated version of the others.</summary>
    [Fact]
    public async Task A_contributor_gets_the_draft_of_the_sheets_he_may_fill_and_the_validated_version_of_the_others()
    {
        var (fixture, format) = Arrange();
        var project = fixture.Project;
        var heldLane = project.NextId();
        project.Database.Add(new Lane(heldLane, project.RootMapId, "PRJ-C02", "Acteur 2", "fr", LaneType.Person, project.Contributor.Id, "#E3F2FD", 1, null,
            TestProject.Now, project.Facilitator.Id, null, null, null, null, 1));
        Guid[] steps = [project.Step(0, assignee: project.Contributor.Id), project.Step(1, laneId: heldLane), project.Step(2)];
        foreach (var step in steps)
            Sheet(project, step, "Version 1", "Version 2");
        fixture.ActingAs(project.Contributor);

        await Export(fixture, format).RunAsync(project.RootMapId, MapCopyPurpose.Pdf, Token);

        Assert.Equal(["Version 2", "Version 2", "Version 1"], steps.Select(step => PurposeSeen(format, step)));
    }

    /// <summary>
    /// The print is the branch of the map down to the 12 levels of the format, not the map alone of the view: the cut the
    /// format reports beyond them reaches the copy, which the route says by its header (the levels themselves are held by
    /// the tests of the format).
    /// </summary>
    [Fact]
    public async Task The_copy_for_a_pdf_stops_at_twelve_levels_as_the_copy_of_a_branch()
    {
        var (fixture, format) = Arrange();
        fixture.ActingAs(fixture.Project.Reader);

        var copy = await Export(fixture, format).RunAsync(fixture.Project.RootMapId, MapCopyPurpose.Pdf, Token);

        Assert.True(copy.DepthCut);
        Assert.Equal((fixture.Project.RootMapId, (Guid?)null), (format.PrintOf?.MapId, format.ViewOf?.MapId));
    }
}
