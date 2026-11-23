// PmapInsertion.cs
// A .pmap file inserted as a detailed step of the map open, or replacing the detail of one of its steps (3.3, F-208): its preview, then its copy in one operation undone by one Ctrl+Z, the facilitator's.

using System.Text.Json;
using Topogenia.Application.Audit;
using Topogenia.Application.Authorization;
using Topogenia.Application.Commands;
using Topogenia.Application.OperationLog;
using Topogenia.Application.Ports;
using Topogenia.Application.Presentation;
using Topogenia.Application.Queries;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;
using Topogenia.Domain.Steps;
using Topogenia.Domain.Structure;
using Topogenia.Domain.SubMaps;
using Topogenia.Domain.Translations;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;

namespace Topogenia.Application.Exchange;

/// <summary>
/// What an insertion shows before anything is written: the project of the file, its language, what it holds, its levels and
/// how many the map leaves room for, the points its author asks to check, how many addresses of its documents reach
/// another machine (pre-audit 68, P3-02), the languages of its translations left out (not
/// configured), the label proposed for the new step, and the structure version of the project read.
/// </summary>
public sealed record PmapInsertionPreview(string? ProjectCode, string ProjectName, string Language, int Maps, int Lanes, int Steps, int Flows, int Sheets,
    int Depth, int Room, IReadOnlyList<string> Uncertainties, int RemoteLocations, IReadOnlyList<string> DroppedLanguages, string Label, int StructureVersion);

/// <summary>What an insertion wrote: the detailed step, its code, its new detailed map, what it holds, the languages left out.</summary>
public sealed record PmapInserted(Guid StepId, string Code, Guid DetailMapId, int Maps, int Lanes, int Steps, int Flows, int Sheets,
    IReadOnlyList<string> DroppedLanguages);

/// <summary>
/// Where a new detailed step goes: a lane of the map open, a column and a row of it (RG-C04 when the cell is taken); or,
/// with <see cref="Anchor"/>, in the flow next to a step of the map, as "Insert a step before / after" places it.
/// </summary>
public sealed record InsertionPlace(Guid LaneId, int Column, int Row)
{
    /// <summary>
    /// The step the new one goes before or after, in the flow (decision of the owner of the project of 17/11/2026, the
    /// practice of Visio, Bizagi, Signavio and bpmn.io): its single flow on that side is cut in two, its label kept on the
    /// side of its origin; the column given is not read then.
    /// </summary>
    public Guid? Anchor { get; init; }

    /// <summary>Before or after <see cref="Anchor"/>; after by default.</summary>
    public InsertPosition Position { get; init; } = InsertPosition.After;

    /// <summary>
    /// The flow of the map the new step goes on, as a shape dropped on a connector in Visio, Bizagi or draw.io (decision of
    /// the owner of the project of 17/11/2026): that flow cut in two, even an exit of a gateway among others; it prevails over
    /// <see cref="Anchor"/>, the column given not read.
    /// </summary>
    public Guid? Flow { get; init; }
}

/// <summary>
/// The rules an insertion shares with its preview (3.3, F-208): the copy of a file, never a live link, decision of the owner
/// of the project of 15/11/2026; the levels of the project bounded at those a `.pmap` carries, so that the assembled
/// project can still be copied whole; the label proposed for the step.
/// </summary>
public static class PmapInsertionRules
{
    /// <summary>The levels a map and the details below it may reach: those a `.pmap` carries (`PmapFormat.DepthMax`, held equal by a test).</summary>
    public const int DepthMax = 12;

    /// <summary>The levels a file may have when its root becomes the detail of a step of the map: 0 when the map has none left.</summary>
    public static int Room(ProjectWorkspace workspace, Guid mapId)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        return Math.Max(0, DepthMax - workspace.Depth(mapId));
    }

    /// <summary>A file too deep for the map: FICHIER_INVALIDE with one fault at `map` (PMAP_PROFONDEUR, the levels left), as a file too deep at its opening.</summary>
    public static void CheckDepth(ProjectWorkspace workspace, Guid mapId, PmapInsertionFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        var room = Room(workspace, mapId);
        if (file.Depth <= room)
            return;
        throw new DomainException(ErrorCodes.InvalidFile, RefusalKind.Invalid, new Dictionary<string, object?>
        {
            ["count"] = 1,
            [FileError.DetailsKey] = new[] { new FileError("map", FileErrorCodes.TooDeep, new Dictionary<string, object?> { ["max"] = room }) },
        });
    }

    /// <summary>
    /// The label proposed for the new step: the name of the project of the file in the language of the texts of the project
    /// when the file gives it, its name otherwise, cut to the length of a label, never inside a character.
    /// </summary>
    public static string Label(ProjectWorkspace workspace, PmapInsertionFile file)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(file);
        var name = file.Language != workspace.TextLanguage && file.NameTranslations.TryGetValue(workspace.TextLanguage, out var translated)
            ? translated : file.Document.ProjectName;
        var trimmed = name.Trim();
        if (trimmed.Length <= StructureEdits.StepLabelMax)
            return trimmed;
        // Never between the two halves of a character outside the basic plane: the label would keep a broken one (audit of the copy 64, P2-06).
        var cut = char.IsHighSurrogate(trimmed[StructureEdits.StepLabelMax - 1]) ? StructureEdits.StepLabelMax - 1 : StructureEdits.StepLabelMax;
        return trimmed[..cut].TrimEnd();
    }

    /// <summary>
    /// The translations of the label of the new step when it is the one proposed: those of the name of the project of the
    /// file, and its name in its language; the language of origin of the step left out by the rule of the translations.
    /// </summary>
    public static Dictionary<string, string?> LabelTranslations(PmapInsertionFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        var translations = file.NameTranslations.ToDictionary(pair => pair.Key, pair => (string?)pair.Value, StringComparer.Ordinal);
        translations[file.Language] = file.Document.ProjectName;
        return translations
            .Where(pair => pair.Value is { Length: > 0 and <= StructureEdits.StepLabelMax })
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
    }
}

/// <summary>
/// `POST /api/maps/{id}/insert-pmap/preview` (facilitator, 3.3, F-208): what the file holds and whether the map leaves room
/// for its levels, nothing written; with a step of the map, what replacing its detail would hold, its map the one of the
/// step (INTROUVABLE for a step of another map).
/// </summary>
[RequiresRole(MemberRole.Facilitator)]
public sealed class PmapInsertionPreviewQuery(Guid mapId, PmapInsertionFile file, Guid? stepId = null) : ProjectQuery<PmapInsertionPreview>(AccessTarget.Map(mapId))
{
    public override WorkspaceContent Content => WorkspaceContent.Translations;

    public override PmapInsertionPreview Read(ProjectWorkspace workspace, QueryContext context)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        workspace.LiveMap(mapId);
        if (stepId is { } id && workspace.LiveStep(id).MapId != mapId)
            throw DomainException.NotFound();
        var document = file.Document;
        var counts = document.Counts;
        return new PmapInsertionPreview(document.ProjectCode, document.ProjectName, file.Language, counts.Maps, counts.Lanes, counts.Steps, counts.Flows,
            counts.Sheets, file.Depth, PmapInsertionRules.Room(workspace, mapId), document.Uncertainties, document.RemoteLocations, file.DroppedLanguages,
            PmapInsertionRules.Label(workspace, file), workspace.Project.StructureVersion);
    }
}

/// <summary>
/// What the insertion and the replacement share: the detail built from the file under a step by the format, its comments of
/// presentation written beside the core after it, in the same unit (`CommentLineRules.Imported`, as at the opening).
/// </summary>
public abstract class PmapDetailCommand(IPmapFormat format, PmapInsertionFile file, bool withSheets, LanguageSettings languages, AccessTarget target)
    : ProjectCommand<PmapInserted>(target)
{
    private IReadOnlyList<PresentationComment> comments = [];
    private IReadOnlyList<FlowPresentationComment> flowComments = [];
    private PmapInserted? inserted;

    protected PmapInsertionFile File => file;

    /// <summary>`insert` or `replace`, as the event of the audit gives it.</summary>
    protected abstract string Mode { get; }

    /// <summary>The copy of a file is traced as the opening of a file is (spec 5.5).</summary>
    public override string? AuditKind => AuditKinds.FileInserted;

    public override string? AuditDetails(ChangeSet changes) => inserted is not { } done ? null : JsonSerializer.Serialize(new
    {
        mode = Mode, step = done.StepId, map = done.DetailMapId, maps = done.Maps, steps = done.Steps, sheets = done.Sheets,
    });

    protected LanguageSettings Languages => languages;

    /// <summary>The structure version of the project the preview read (If-Match): a project changed since refuses it (CONFLIT_VERSION).</summary>
    public int? StructureVersion { get; init; }

    /// <summary>The sheets, the documents and the translations, which the opening of a file writes too.</summary>
    public override WorkspaceContent Content => WorkspaceContent.Sheets | WorkspaceContent.Documents | WorkspaceContent.Translations;

    public override string? LogKind => OperationKinds.InsertFile;

    /// <summary>Checks the version read, then builds the detail of the step and keeps its comments for <see cref="WriteBesideAsync"/>.</summary>
    protected PmapInserted Build(ProjectWorkspace workspace, OperationContext context, Step step)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        var built = format.Detail(context, workspace, file.Document, step.Id, languages, withSheets);
        var steps = workspace.All<Step>().Where(item => item.DeletedAt is null).ToDictionary(item => item.Id, item => item.SourceLanguage);
        comments = PresentationCommentRules.Imported(built.Comments, steps, languages);
        flowComments = FlowPresentationCommentRules.Imported(built.FlowComments,
            FlowPresentationCommentRules.Origins(workspace.All<Flow>().Where(flow => flow.DeletedAt is null), steps), languages);
        var counts = built.Counts;
        inserted = new PmapInserted(step.Id, workspace.LiveStep(step.Id).Code, built.MapId, counts.Maps, counts.Lanes, counts.Steps, counts.Flows, counts.Sheets,
            file.DroppedLanguages);
        return inserted;
    }

    /// <summary>The lock of the command moved the structure version on by one before the load (AR-111): the preview read the one before.</summary>
    protected void CheckVersion(ProjectWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        if (StructureVersion is { } read && read != workspace.Project.StructureVersion - 1)
            throw DomainException.Conflict(ErrorCodes.VersionConflict, new Dictionary<string, object?>
            {
                ["object"] = "map", ["updated_by"] = workspace.Project.UpdatedBy, ["updated_at"] = workspace.Project.UpdatedAt,
            });
    }

    public override async Task WriteBesideAsync(BesidePorts beside, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(beside);
        if (comments.Count > 0)
            await beside.Comments.AddAsync(comments, cancellationToken);
        if (flowComments.Count > 0)
            await beside.FlowComments.AddAsync(flowComments, cancellationToken);
    }
}

/// <summary>
/// `POST /api/maps/{id}/insert-pmap` with If-Match (facilitator, 3.3, F-208): a new step in the lane, the column and the
/// row chosen (`StepOperations.Create`, RG-C04 when the cell is taken), under the label typed or the one proposed (with the
/// translations of the name of the file then), then the file as its detail. One operation of the log, undone by one Ctrl+Z;
/// any refusal refuses all of it.
/// </summary>
[RequiresRole(MemberRole.Facilitator)]
public sealed class InsertPmapCommand(IPmapFormat format, Guid mapId, PmapInsertionFile file, InsertionPlace place, string? label, bool withSheets,
    LanguageSettings languages) : PmapDetailCommand(format, file, withSheets, languages, AccessTarget.Map(mapId))
{
    protected override string Mode => "insert";

    public override Guid? MapOf(ProjectWorkspace workspace) => mapId;

    public override PmapInserted Execute(ProjectWorkspace workspace, OperationContext context)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(place);
        CheckVersion(workspace);
        workspace.LiveMap(mapId);
        PmapInsertionRules.CheckDepth(workspace, mapId, File);
        var proposed = PmapInsertionRules.Label(workspace, File);
        var chosen = string.IsNullOrWhiteSpace(label) ? proposed : label.Trim();
        var created = Place(workspace, context, chosen);
        if (chosen == proposed)
            TranslationRules.WriteManual(workspace, context, TranslationObject.Step, created.Id, PmapInsertionRules.LabelTranslations(File), Languages);
        return Build(workspace, context, workspace.LiveStep(created.Id));
    }

    /// <summary>
    /// The new step: in a cell (`StepOperations.Create`); on a flow of the map, that flow cut in two, in the lane chosen; or in
    /// the flow next to a step of the map (`StepInsertion.Insert`, before or after only), then moved into the lane chosen when
    /// it is not the one it was put in, its flows following it.
    /// </summary>
    private StepResult Place(ProjectWorkspace workspace, OperationContext context, string label)
    {
        if (place.Flow is { } flowId)
            return OnFlow(workspace, context, flowId, label);
        if (place.Anchor is not { } anchor)
            return StepOperations.Create(workspace, context, new CreateStepRequest(mapId, place.LaneId, place.Column, label, null, place.Row));
        var reference = workspace.LiveStep(anchor);
        if (reference.MapId != mapId)
            throw DomainException.NotFound();
        if (place.Position is not (InsertPosition.Before or InsertPosition.After))
            throw DomainException.InvalidValue("position");
        StructureEdits.LaneOfMap(workspace, mapId, place.LaneId);
        return Moved(workspace, context, StepInsertion.Insert(workspace, context, new InsertStepRequest(anchor, place.Position, label)));
    }

    /// <summary>The new step moved into the lane chosen when it is not the one it was put in, its flows following it (LANE of another map: INTROUVABLE).</summary>
    private StepResult Moved(ProjectWorkspace workspace, OperationContext context, StepResult created)
    {
        var lane = StructureEdits.LaneOfMap(workspace, mapId, place.LaneId);
        if (lane.Id != workspace.LiveStep(created.Id).LaneId)
            StepOperations.Update(workspace, context, new UpdateStepRequest(created.Id) { LaneId = new FieldChange<Guid>(lane.Id) });
        return created;
    }

    /// <summary>
    /// The new step on a flow of the map, as "Insert a step after" its origin puts it (A-2.7): the column after the origin
    /// (RG-C04 when the cell is taken), in the lane chosen, at the row of the origin in its lane, the first row in another
    /// one (as a step moved into another lane); the flow cut into origin to step, its label, share, translations and sides
    /// kept, and step to target. Made at once in the lane chosen: made in the lane of the origin then moved, it pushed the
    /// steps there for nothing, and could be refused at the last column (audit of the copy 64, P2-06). A flow of another
    /// map or deleted, a lane of another map: INTROUVABLE.
    /// </summary>
    private StepResult OnFlow(ProjectWorkspace workspace, OperationContext context, Guid flowId, string label)
    {
        var cut = workspace.LiveFlow(flowId);
        if (cut.MapId != mapId)
            throw DomainException.NotFound();
        var origin = workspace.LiveStep(cut.SourceStepId);
        var lane = StructureEdits.LaneOfMap(workspace, mapId, place.LaneId);
        var text = TextRules.Required(label, "label", StructureEdits.StepLabelMax);
        var cell = lane.Id == origin.LaneId ? new Cell(origin.LaneId, origin.Column + 1, origin.Row) : new Cell(lane.Id, origin.Column + 1, 0);
        var created = StructureEdits.NewStep(workspace, context, cell, text, StepOperations.ParseType(null), workspace.TextLanguage);
        workspace.SoftDelete(cut, context);
        var first = StructureEdits.CarriedSides(workspace, cut.Id, cut.Id, cut.SourceStepId, created.Id);
        TranslationRules.Carry(workspace, context, StructureEdits.NewFlow(workspace, context, mapId, cut.SourceStepId, created.Id,
            cut.Label, StructureEdits.CarriedShare(workspace, cut.Id, cut.SourceStepId), first.Source, first.Target), cut.Id);
        var second = StructureEdits.CarriedSides(workspace, cut.Id, cut.Id, created.Id, cut.TargetStepId);
        StructureEdits.NewFlow(workspace, context, mapId, created.Id, cut.TargetStepId, null, null, second.Source, second.Target);
        return new StepResult(created.Id, created.Code);
    }
}

/// <summary>
/// `POST /api/steps/{id}/replace-pmap` with If-Match (facilitator, 3.3, F-208): the detail of a step replaced by a file,
/// the newer version of the process it was copied from: its old detail deleted with every level below it (in the batch of
/// the operation, as "Delete the detail"), then the file built as its detail; a step without detail takes one. A start,
/// an end, a parallel or an inclusive step refuses (ETAPE_NON_DETAILLABLE). One operation, undone by one Ctrl+Z.
/// </summary>
[RequiresRole(MemberRole.Facilitator)]
public sealed class ReplaceDetailCommand(IPmapFormat format, Guid stepId, PmapInsertionFile file, bool withSheets, LanguageSettings languages)
    : PmapDetailCommand(format, file, withSheets, languages, AccessTarget.Step(stepId))
{
    protected override string Mode => "replace";

    public override Guid? MapOf(ProjectWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        return workspace.LiveStep(stepId).MapId;
    }

    public override PmapInserted Execute(ProjectWorkspace workspace, OperationContext context)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        CheckVersion(workspace);
        var step = workspace.LiveStep(stepId);
        if (StepTypes.HasNoContent(step.StepType))
            throw DomainException.Invalid(ErrorCodes.StepNotDetailable);
        PmapInsertionRules.CheckDepth(workspace, step.MapId, File);
        workspace.Touch(step);
        if (workspace.SubMapOf(step.Id) is { } old)
            SubMapRules.DeleteSubMap(workspace, context, old.Id);
        return Build(workspace, context, step);
    }
}
