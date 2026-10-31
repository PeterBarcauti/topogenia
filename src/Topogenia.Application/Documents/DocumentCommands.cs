// DocumentCommands.cs
// The documents of a step and their uses: add, update, delete, tick, untick, and the "Other" window (annex A 1, A 2.11, 2.12).

using Topogenia.Application.Authorization;
using Topogenia.Application.Commands;
using Topogenia.Application.Ports;
using Topogenia.Application.Queries;
using Topogenia.Domain.Documents;
using Topogenia.Domain.Model;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;

namespace Topogenia.Application.Documents;

/// <summary>
/// A command on the documents of a step, by the one who may fill its sheet (« Rempl. »). Documents and uses are not
/// undoable (annex B 2.6-38, AR-21): no kind, no log entry. Its reply carries the view of the map of the step: the markers
/// of the steps, the documents carried by the flows and the completeness gauges change with them (1.x: the sheet reloaded
/// the map after each change of the flows, `PanneauFiche.tsx:250`).
/// </summary>
public abstract class StepDocumentCommand<TResult>(Guid stepId) : ProjectCommand<TResult>(AccessTarget.Step(stepId))
{
    /// <summary>The documents, the uses and the pain points of the project; the sheets for the gauges.</summary>
    public override WorkspaceContent Content => WorkspaceContent.Sheets | WorkspaceContent.Documents;

    public override Guid? SheetStepId => Target.Id;

    public override Guid? MapOf(ProjectWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        return workspace.LiveStep(Target.Id).MapId;
    }
}

/// <summary>`POST /api/steps/{id}/documents` (1.x `POST /api/etapes/{id}/documents`, Rempl.): an output or an external input.</summary>
[RequiresSheetFill]
public sealed class AddDocumentCommand(Guid stepId, DocumentInput input) : StepDocumentCommand<Guid>(stepId)
{
    public override Guid Execute(ProjectWorkspace workspace, OperationContext context) => DocumentEdits.Add(workspace, context, Target.Id, input);
}

/// <summary>`PUT /api/steps/{id}/documents/{docId}` with If-Match (1.x `PUT /api/etapes/{id}/documents/{docId}`, Rempl.).</summary>
[RequiresSheetFill]
public sealed class UpdateDocumentCommand(Guid stepId, Guid documentId, DocumentInput input) : StepDocumentCommand<Guid>(stepId)
{
    public override Guid Execute(ProjectWorkspace workspace, OperationContext context)
    {
        DocumentEdits.Update(workspace, Target.Id, documentId, input);
        return documentId;
    }

    /// <summary>The If-Match of the request on the document.</summary>
    public static UpdateDocumentCommand WithVersion(Guid stepId, Guid documentId, DocumentInput input, int? version) =>
        new(stepId, documentId, input) { Versions = version is { } read ? [VersionCheck.Of<Document>(Document.KeyOf(documentId), read)] : [] };
}

/// <summary>`DELETE /api/steps/{id}/documents/{docId}` with If-Match (1.x `DELETE /api/etapes/{id}/documents/{docId}`, Rempl.): with its uses.</summary>
[RequiresSheetFill]
public sealed class DeleteDocumentCommand(Guid stepId, Guid documentId) : StepDocumentCommand<Guid>(stepId)
{
    public override Guid Execute(ProjectWorkspace workspace, OperationContext context)
    {
        DocumentEdits.Delete(workspace, Target.Id, documentId);
        return documentId;
    }

    /// <summary>The If-Match of the request on the deleted document (spec 5.3, ADR 045 revised).</summary>
    public static DeleteDocumentCommand WithVersion(Guid stepId, Guid documentId, int? version) =>
        new(stepId, documentId) { Versions = version is { } read ? [VersionCheck.Of<Document>(Document.KeyOf(documentId), read)] : [] };
}

/// <summary>
/// `PUT /api/steps/{id}/uses/{docId}` (1.x `PUT /api/etapes/{id}/usages/{docId}`, Rempl.): ticks or updates the use, with
/// its pain point raised on the producing step. The If-Match on an existing use, absent in 1.x, follows A-2.16-05.
/// </summary>
[RequiresSheetFill]
public sealed class UseDocumentCommand(Guid stepId, Guid documentId, DocumentUseInput input) : StepDocumentCommand<DocumentUseResult>(stepId)
{
    public override DocumentUseResult Execute(ProjectWorkspace workspace, OperationContext context) =>
        DocumentUses.Use(workspace, context, Target.Id, documentId, input);

    /// <summary>The If-Match of the request on the use of the document by the step.</summary>
    public static UseDocumentCommand WithVersion(Guid stepId, Guid documentId, DocumentUseInput input, int? version) =>
        new(stepId, documentId, input)
        {
            Versions = version is { } read ? [VersionCheck.Of<DocumentUse>(DocumentUse.KeyOf(documentId, stepId), read)] : [],
        };
}

/// <summary>
/// `DELETE /api/steps/{id}/uses/{docId}` (1.x `DELETE /api/etapes/{id}/usages/{docId}`, Rempl.): unticks, idempotent, with
/// If-Match on the use: 0 for a use the client did not read; a use already unticked is the state asked, whatever version
/// was read (AR-107), a use ticked again since is a conflict (ADR 045 revised).
/// </summary>
[RequiresSheetFill]
public sealed class StopUsingDocumentCommand(Guid stepId, Guid documentId) : StepDocumentCommand<Guid>(stepId)
{
    public override Guid Execute(ProjectWorkspace workspace, OperationContext context)
    {
        DocumentUses.StopUsing(workspace, Target.Id, documentId);
        return documentId;
    }

    /// <summary>The If-Match of the request on the use of the document by the step.</summary>
    public static StopUsingDocumentCommand WithVersion(Guid stepId, Guid documentId, int? version) =>
        new(stepId, documentId)
        {
            Versions = version is { } read ? [VersionCheck.OfErasure<DocumentUse>(DocumentUse.KeyOf(documentId, stepId), read)] : [],
        };
}

/// <summary>
/// `GET /api/steps/{id}/documents/available` (1.x `GET /api/etapes/{id}/documents/disponibles`, Lecteur): the "Other"
/// window, the outputs of the other steps of the map grouped by step. It reads the documents only, never the map view.
/// </summary>
[RequiresRole(MemberRole.Reader)]
public sealed class GetAvailableDocumentsQuery(Guid stepId) : ProjectQuery<IReadOnlyList<AvailableOutputs>>(AccessTarget.Step(stepId))
{
    public override WorkspaceContent Content => WorkspaceContent.Documents;

    public override IReadOnlyList<AvailableOutputs> Read(ProjectWorkspace workspace, QueryContext context) =>
        DocumentFlows.OtherOutputs(workspace, Target.Id);
}
