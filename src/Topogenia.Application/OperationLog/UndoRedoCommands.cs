// UndoRedoCommands.cs
// Undo and redo of the last operation of the user on a map, through the command template (spec 4.6, AR-05, ADR 042).

using Topogenia.Application.Authorization;
using Topogenia.Application.Commands;
using Topogenia.Application.Ports;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;
using Topogenia.Domain.Structure;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;

namespace Topogenia.Application.OperationLog;

/// <summary>The operation undone or redone: the chain it belongs to and its kind (the message "undone: {action}", annex B 2.6).</summary>
public sealed record JournalResult(Guid OperationId, string Kind);

/// <summary>
/// What undo and redo share: the stack of the user on the map is read under the lock (<see cref="PrepareAsync"/>), the
/// domain replays it (`UndoReplay`), and a refusal (OPERATION_DEPASSEE, CARTE_MODIFIEE) clears the stacks of the user on
/// the map in a unit of its own (AR-05). An empty stack is INTROUVABLE: there is nothing to undo or to redo.
/// </summary>
public abstract class JournalCommand : ProjectCommand<JournalResult>
{
    protected JournalCommand(Guid mapId)
        : base(AccessTarget.Map(mapId))
    {
    }

    /// <summary>The chain replayed, read by <see cref="PrepareAsync"/>.</summary>
    protected OperationChain? Chain { get; private set; }

    public override WorkspaceContent Content => WorkspaceContent.All;

    public override Guid? MapOf(ProjectWorkspace workspace) => Target.Id;

    public override async Task PrepareAsync(IOperationLog log, Guid userId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(log);
        var entries = await log.EntriesAsync(Target.Id, userId, cancellationToken);
        Chain = Next(entries) ?? throw DomainException.NotFound();
    }

    public override bool LogsRefusal(DomainException refusal) =>
        refusal is { Code: ErrorCodes.OperationOutdated or ErrorCodes.MapModified };

    /// <summary>AR-05: a refused undo or redo clears the undo and the redo stacks of the user on the map.</summary>
    public override async Task LogRefusalAsync(IOperationLog log, Guid userId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(log);
        var entries = await log.EntriesAsync(Target.Id, userId, cancellationToken);
        if (entries.Count > 0)
            await log.RemoveAsync(entries.Select(entry => entry.Id).ToList(), cancellationToken);
    }

    /// <summary>The chain this command replays among the entries of the user on the map, or null when there is none.</summary>
    protected abstract OperationChain? Next(IReadOnlyList<OperationLogEntry> entries);

    protected OperationLogEntry Latest => (Chain ?? throw new InvalidOperationException("The stack is read before the replay.")).Latest;
}

/// <summary>Undoes the most recent live operation of the user on the map (spec 4.6); its entry keeps the change set of the undo.</summary>
[RequiresRole(MemberRole.Facilitator)]
public sealed class UndoCommand(Guid mapId) : JournalCommand(mapId)
{
    protected override OperationChain? Next(IReadOnlyList<OperationLogEntry> entries) => OperationStack.Undoable(entries).FirstOrDefault();

    public override JournalResult Execute(ProjectWorkspace workspace, OperationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        UndoReplay.Undo(workspace, context.Replaying(Chain!.RootId), ChangeSetJson.Read(Latest.ForwardJson));
        return new JournalResult(Chain!.RootId, Latest.Kind);
    }

    /// <summary>The entry is marked undone and keeps what the undo really applied, repairs included, for the redo.</summary>
    public override async Task<Guid?> LogAsync(IOperationLog log, OperationContext context, Guid projectId, Guid mapId, ChangeSet changes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(context);
        await log.UpdateAsync(Latest with { UndoneAt = context.Now, InverseJson = ChangeSetJson.Write(changes) }, cancellationToken);
        return Chain!.RootId;
    }
}

/// <summary>
/// Redoes the operation undone last by the user on the map (spec 4.6): a new entry of the same chain, pointing to its root
/// (`redone_from_id`), which keeps the changes of the redo; the redo stack is not cleared by it.
/// </summary>
[RequiresRole(MemberRole.Facilitator)]
public sealed class RedoCommand(Guid mapId) : JournalCommand(mapId)
{
    protected override OperationChain? Next(IReadOnlyList<OperationLogEntry> entries) => OperationStack.Redoable(entries).FirstOrDefault();

    public override JournalResult Execute(ProjectWorkspace workspace, OperationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        UndoReplay.Redo(workspace, context.Replaying(Chain!.RootId), ChangeSetJson.Read(Latest.ForwardJson), ChangeSetJson.Read(Latest.InverseJson));
        return new JournalResult(Chain!.RootId, Latest.Kind);
    }

    public override async Task<Guid?> LogAsync(IOperationLog log, OperationContext context, Guid projectId, Guid mapId, ChangeSet changes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(context);
        var sequence = await log.NextSequenceAsync(mapId, cancellationToken);
        await log.AppendAsync(new OperationLogEntry(context.OperationId, projectId, mapId, context.ActorId, Latest.Kind, sequence,
            ChangeSetJson.Write(changes), OperationRecorder.NotUndoneYet, ChangeSetJson.WriteTouched(changes), context.Now, null,
            Chain!.RootId), cancellationToken);
        return Chain.RootId;
    }
}
