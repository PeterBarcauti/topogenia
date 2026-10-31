// WritePlan.cs
// Order of the writing of a change set: what frees a unique slot or parks a step first, then every change at its final value (ADR 016, 030).

using Topogenia.Domain.Model;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;

namespace Topogenia.Persistence.Writing;

/// <summary>The three kinds of statements of a change set: insert a record, update it from one state to another, delete it.</summary>
public enum WriteKind
{
    Insert,
    Update,
    Delete,
}

/// <summary>One statement: <see cref="From"/> is the state the row must have (null for an insert), <see cref="To"/> the state written.</summary>
public sealed record RecordWrite(WriteKind Kind, IRecord? From, IRecord? To)
{
    public IRecord Record => (To ?? From)!;
}

/// <summary>
/// The change set in two saves (ADR 016, ADR 030). SQLite checks a unique index row by row and SQL Server statement by
/// statement: a row that takes a unique value freed by another row of the same save must come after it, and an exchange
/// of cells has no possible order. The first save therefore writes what frees a slot of a partial unique index, after
/// the deletion batches it refers to: a record deleted logically (out of the live indexes), a role A that becomes another
/// role or is removed, a frame that closes, a use that gives up its pain point; and it parks every live step whose cell
/// changes on the column 1 000 000 000 + i (outside the bounds of the domain, admitted by the CHECK `column_index >= 0`).
/// A change set that creates a deletion batch with the project or the users it names (the import of a package whose
/// open frame comes back in a new batch, ADR 052) writes those projects and users in the first save too, before the
/// batch: EF Core orders the statements of one save by their foreign keys, not across two saves.
/// The second save writes every change of the domain at its final value, in the order of the change set (creations,
/// modifications, removals, touched records), EF Core ordering the statements by their foreign keys. A step parked or a
/// record freed in the first save is updated in the second from its intermediate state, version included: the version
/// is checked once, on the first statement of each record (D06).
/// </summary>
public sealed record WritePlan(IReadOnlyList<RecordWrite> First, IReadOnlyList<RecordWrite> Then)
{
    /// <summary>The first parking column (the 1.x `1 000 000 000 + i`, annex A 6-4).</summary>
    public const int ParkingColumn = 1_000_000_000;

    public static WritePlan Of(ChangeSet changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        var first = new List<RecordWrite>();
        var then = new List<RecordWrite>();
        var parked = 0;
        var batchCreated = changes.Changes.Any(change => change is { Kind: ChangeKind.Created, After: DeletionBatch });
        foreach (var change in changes.Changes)
        {
            switch (change.Kind)
            {
                case ChangeKind.Created when change.After is DeletionBatch || (batchCreated && change.After is Project or AppUser):
                    first.Add(new RecordWrite(WriteKind.Insert, null, change.After));
                    break;
                case ChangeKind.Created:
                    then.Add(new RecordWrite(WriteKind.Insert, null, change.After));
                    break;
                case ChangeKind.Modified when Intermediate(change.Before!, change.After!, ref parked) is { } intermediate:
                    first.Add(new RecordWrite(WriteKind.Update, change.Before, intermediate));
                    then.Add(new RecordWrite(WriteKind.Update, intermediate, change.After));
                    break;
                case ChangeKind.Modified:
                    then.Add(new RecordWrite(WriteKind.Update, change.Before, change.After));
                    break;
                case ChangeKind.Removed when FreesASlot(change.Before!):
                    first.Add(new RecordWrite(WriteKind.Delete, change.Before, null));
                    break;
                default:
                    then.Add(new RecordWrite(WriteKind.Delete, change.Before, null));
                    break;
            }
        }
        return new WritePlan(first, then);
    }

    /// <summary>A removed record that holds a slot of a partial unique index: removed in the first save.</summary>
    private static bool FreesASlot(IRecord removed) =>
        removed is StepRaci { Role: RaciRole.A } or DocumentUse { PainPointId: not null };

    /// <summary>
    /// The state written by the first save for a modification that frees a slot or moves a live step, null when the
    /// modification is written once. It is the final state (version and update audit included), except for a moved step,
    /// parked in the lane, map and row it leaves (the lane it goes to may be created by the second save), and for a use
    /// that changes its pain point, which first gives up the one it held.
    /// </summary>
    private static IRecord? Intermediate(IRecord before, IRecord after, ref int parked)
    {
        if (before is Step step && after is Step { DeletedAt: null } moved && (step.Cell != moved.Cell || step.MapId != moved.MapId))
            return moved with { Column = ParkingColumn + parked++, LaneId = step.LaneId, MapId = step.MapId, Row = step.Row };
        if (before is ISoftDeletable { IsAlive: true } && after is ISoftDeletable { IsAlive: false })
            return after;
        if (before is StepRaci { Role: RaciRole.A } && after is StepRaci { Role: not RaciRole.A })
            return after;
        if (before is Dissolution { IsOpen: true } && after is Dissolution { IsOpen: false })
            return after;
        if (before is DocumentUse { PainPointId: not null } use && after is DocumentUse changed && changed.PainPointId != use.PainPointId)
            return changed with { PainPointId = null };
        return null;
    }
}
