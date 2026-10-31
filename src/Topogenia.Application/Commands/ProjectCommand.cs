// ProjectCommand.cs
// A write request on one project, run by the command template: what it names, needs, checks and does (spec 3.4).

using Topogenia.Application.Ports;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Workspace;

namespace Topogenia.Application.Commands;

/// <summary>
/// One write request on a project. A concrete command declares its right with one attribute of
/// <see cref="Authorization.AccessRequirementAttribute"/> and says what it needs; <see cref="CommandRunner"/> does the
/// rest in the same order for every command (spec 3.4): right, unit of work and lock of the project, workspace, If-Match,
/// domain, writing, operation log, reply with the view of the map. A command holds no rule: it calls the domain.
/// </summary>
/// <typeparam name="TResult">What the domain returns (identifiers, codes, warnings).</typeparam>
public abstract class ProjectCommand<TResult>
{
    protected ProjectCommand(AccessTarget target)
    {
        Target = target;
    }

    /// <summary>The object named by the route, whose project is locked and loaded.</summary>
    public AccessTarget Target { get; }

    /// <summary>True for the commands of the trash, which act on a project in the trash (annex A 1, `Droits.cs:20`).</summary>
    public virtual bool TargetsTrash => false;

    /// <summary>The groups of records the domain needs besides the structure.</summary>
    public virtual WorkspaceContent Content => WorkspaceContent.None;

    /// <summary>
    /// The kind written in the operation log (the codes of the 1.x `historique.action.*`, spec 4.6); null for a command
    /// that cannot be undone (sheets, documents, members, translations...).
    /// </summary>
    public virtual string? LogKind => null;

    /// <summary>
    /// The kind of the audit event of the command (spec 5.5: members, trash, restore), written with its changes in the same
    /// unit of work; null for a command the audit does not trace. A command that changes nothing leaves no event.
    /// </summary>
    public virtual string? AuditKind => null;

    /// <summary>The details of the audit event, identifiers and counts only (<see cref="Audit.AuditTrail"/>).</summary>
    public virtual string? AuditDetails(ChangeSet changes) => null;

    /// <summary>The versions read by the client (`If-Match`), checked before the domain runs.</summary>
    public IReadOnlyList<VersionCheck> Versions { get; init; } = [];

    /// <summary>The step whose sheet the command fills, for a command that requires the right to fill.</summary>
    public virtual Guid? SheetStepId => null;

    /// <summary>
    /// The map whose view the reply carries and on which the operation is logged, read on the workspace before the domain
    /// runs; null for a command that touches no map (members, project settings).
    /// </summary>
    public abstract Guid? MapOf(ProjectWorkspace workspace);

    /// <summary>
    /// Reads what the domain needs besides the workspace, under the lock of the project and before the loading (undo and
    /// redo read their entry of the operation log). Runs again with the whole unit on a retry.
    /// </summary>
    public virtual Task PrepareAsync(IOperationLog log, Guid userId, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Calls the domain on the workspace; the template commits the workspace afterwards.</summary>
    public abstract TResult Execute(ProjectWorkspace workspace, OperationContext context);

    /// <summary>
    /// Writes the operation log once the changes are written, and returns the identifier of the logged operation: by
    /// default, a new entry for a command of a <see cref="LogKind"/> that changed something (spec 4.6); undo and redo
    /// write their own entries.
    /// </summary>
    public virtual async Task<Guid?> LogAsync(IOperationLog log, OperationContext context, Guid projectId, Guid mapId, ChangeSet changes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(changes);
        if (LogKind is not { } kind || changes.IsEmpty)
            return null;
        return await OperationLog.OperationRecorder.RecordAsync(log, context, projectId, mapId, kind, changes, cancellationToken);
    }

    /// <summary>
    /// True when a refusal of the domain must still change the log, in a unit of its own after the refused one is rolled
    /// back (AR-05: a refused undo or redo clears the stacks of the user on the map).
    /// </summary>
    public virtual bool LogsRefusal(DomainException refusal) => false;

    /// <summary>What the log keeps of a refusal for which <see cref="LogsRefusal"/> is true, under the lock of the project.</summary>
    public virtual Task LogRefusalAsync(IOperationLog log, Guid userId, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// The refusal the route owes for a refusal of the domain, read in the unit of work before it is rolled back: by
    /// default the refusal itself. The workspace holds one project only (ADR 015), so an object of another project named
    /// by the request is absent from it and the domain answers INTROUVABLE; a command whose route has its own code for
    /// that case reads the access of the objects it names (AR-71: TRANSFERT_HORS_PROJET).
    /// </summary>
    public virtual Task<DomainException> TranslateRefusalAsync(DomainException refusal, RefusalContext context, CancellationToken cancellationToken) =>
        Task.FromResult(refusal);

    /// <summary>The warnings of the operation itself (codes); the warnings of the layout are in the view of the map.</summary>
    public virtual IReadOnlyList<string> WarningsOf(TResult result) => [];
}

/// <summary>What a command reads to translate a refusal of the domain: the access of other objects, the user, the locked project.</summary>
public sealed record RefusalContext(IAccessReader Access, CurrentUser User, Guid ProjectId);
