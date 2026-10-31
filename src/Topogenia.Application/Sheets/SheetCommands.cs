// SheetCommands.cs
// The read and the writes of a step sheet: draft, validation, clarification request, changed or withdrawn (annex A 1 fiches, A 2.10, spec 3.0 section 16), none undoable.

using Topogenia.Application.Authorization;
using Topogenia.Application.Commands;
using Topogenia.Application.Ports;
using Topogenia.Application.Queries;
using Topogenia.Domain.Model;
using Topogenia.Domain.Sheets;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;

namespace Topogenia.Application.Sheets;

/// <summary>
/// `GET /api/steps/{id}/sheet` (1.x `GET /api/etapes/{id}/fiche`, Lecteur): the sheet as the reader may see it. It reads
/// the sheets and the documents, never the view of the map.
/// </summary>
[RequiresRole(MemberRole.Reader)]
public sealed class GetSheetQuery(Guid stepId) : ProjectQuery<SheetView>(AccessTarget.Step(stepId))
{
    public override WorkspaceContent Content => WorkspaceContent.Sheets | WorkspaceContent.Documents;

    public override SheetView Read(ProjectWorkspace workspace, QueryContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return SheetProjection.Of(workspace, Target.Id, context.Role, context.UserId);
    }
}

/// <summary>
/// The If-Match of a sheet. The client names the step, not the sheet, and a step has no sheet before its first save: the
/// version is checked on the sheet of the step when it exists, by the check of the template (<see cref="VersionCheck"/>),
/// before the domain runs; without a sheet there is nothing to compare (1.x `ServiceFiches.cs:125`, `:159`). A client that
/// read no sheet sends <see cref="VersionCheck.NoRecord"/> (the server mode requires the header, ADR 045): a sheet
/// created by someone else since is then a conflict.
/// </summary>
public static class SheetIfMatch
{
    public static void Ensure(ProjectWorkspace workspace, Guid stepId, int? expected)
    {
        if (expected is { } version && SheetQueries.SheetOf(workspace, stepId) is { } sheet)
            VersionCheck.Of<StepSheet>(sheet.Key, version).Ensure(workspace);
    }
}

/// <summary>
/// A command on the sheet of a step: its target is the step, it is not undoable (the sheet has its own versions, annex
/// B 2.6-37, AR-21), and its reply carries the view of the map of the step, whose summary (state, completeness gauge,
/// frequency) changes with the sheet (1.x `onResume` updated the gauge from the reply, annex B 2.6-11).
/// </summary>
public abstract class StepSheetCommand<TResult>(Guid stepId) : ProjectCommand<TResult>(AccessTarget.Step(stepId))
{
    /// <summary>The version of the sheet read by the client (`If-Match`), or null.</summary>
    public int? SheetVersion { get; init; }

    public override WorkspaceContent Content => WorkspaceContent.Sheets | WorkspaceContent.Documents;

    public override Guid? MapOf(ProjectWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        return workspace.LiveStep(Target.Id).MapId;
    }

    public sealed override TResult Execute(ProjectWorkspace workspace, OperationContext context)
    {
        SheetIfMatch.Ensure(workspace, Target.Id, SheetVersion);
        return Run(workspace, context);
    }

    /// <summary>Calls the domain, once the If-Match of the sheet is checked.</summary>
    protected abstract TResult Run(ProjectWorkspace workspace, OperationContext context);
}

/// <summary>`PUT /api/steps/{id}/sheet` with If-Match (1.x `PUT /api/etapes/{id}/fiche`, Rempl.): the complete draft.</summary>
[RequiresSheetFill]
public sealed class SaveSheetCommand(Guid stepId, SheetInput input) : StepSheetCommand<SheetSaveResult>(stepId)
{
    public override Guid? SheetStepId => Target.Id;

    protected override SheetSaveResult Run(ProjectWorkspace workspace, OperationContext context) =>
        SheetWorkflow.Save(workspace, context, Target.Id, input);

    /// <summary>The If-Match of the request on the sheet of the step.</summary>
    public static SaveSheetCommand WithVersion(Guid stepId, SheetInput input, int? version) => new(stepId, input) { SheetVersion = version };
}

/// <summary>`POST /api/steps/{id}/sheet/validate` with If-Match (1.x `POST /api/etapes/{id}/fiche/valider`, Rempl.).</summary>
[RequiresSheetFill]
public sealed class ValidateSheetCommand(Guid stepId) : StepSheetCommand<SheetValidationResult>(stepId)
{
    public override Guid? SheetStepId => Target.Id;

    protected override SheetValidationResult Run(ProjectWorkspace workspace, OperationContext context) =>
        SheetWorkflow.Validate(workspace, context, Target.Id);

    public static ValidateSheetCommand WithVersion(Guid stepId, int? version) => new(stepId) { SheetVersion = version };
}

/// <summary>
/// `POST /api/steps/{id}/sheet/clarification` `{text}` (1.x `POST /api/etapes/{id}/fiche/demander-precision`, Animateur):
/// the validated sheet goes to needs_review. The If-Match, absent in 1.x, follows A-2.16-05 (every change of a versioned
/// record).
/// </summary>
[RequiresRole(MemberRole.Facilitator)]
public sealed class RequestClarificationCommand(Guid stepId, string? text) : StepSheetCommand<Guid>(stepId)
{
    protected override Guid Run(ProjectWorkspace workspace, OperationContext context)
    {
        SheetWorkflow.RequestClarification(workspace, Target.Id, text);
        return Target.Id;
    }

    public static RequestClarificationCommand WithVersion(Guid stepId, string? text, int? version) => new(stepId, text) { SheetVersion = version };
}

/// <summary>
/// `PUT /api/steps/{id}/sheet/clarification` `{text}` (3.0, Animateur, spec 3.0 section 16): the question asked is
/// rewritten, the state of the sheet kept; a sheet without a question is refused (FICHE_ETAT_INVALIDE).
/// </summary>
[RequiresRole(MemberRole.Facilitator)]
public sealed class ChangeClarificationCommand(Guid stepId, string? text) : StepSheetCommand<Guid>(stepId)
{
    protected override Guid Run(ProjectWorkspace workspace, OperationContext context)
    {
        SheetWorkflow.ChangeClarification(workspace, Target.Id, text);
        return Target.Id;
    }

    public static ChangeClarificationCommand WithVersion(Guid stepId, string? text, int? version) => new(stepId, text) { SheetVersion = version };
}

/// <summary>
/// `DELETE /api/steps/{id}/sheet/clarification` (3.0, Animateur, spec 3.0 section 16): the question asked is withdrawn,
/// a sheet in needs_review back to validated; a sheet without a question is refused (FICHE_ETAT_INVALIDE).
/// </summary>
[RequiresRole(MemberRole.Facilitator)]
public sealed class WithdrawClarificationCommand(Guid stepId) : StepSheetCommand<Guid>(stepId)
{
    protected override Guid Run(ProjectWorkspace workspace, OperationContext context)
    {
        SheetWorkflow.WithdrawClarification(workspace, Target.Id);
        return Target.Id;
    }

    public static WithdrawClarificationCommand WithVersion(Guid stepId, int? version) => new(stepId) { SheetVersion = version };
}
