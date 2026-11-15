// ChangeSetWriter.cs
// Writes the change set of an operation in the transaction of the unit: versions checked, steps parked, refusals translated (ADR 016, D06).

using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Topogenia.Domain.Model;
using Topogenia.Domain.Workspace;
using Topogenia.Persistence.Errors;

namespace Topogenia.Persistence.Writing;

/// <summary>
/// Writes a <see cref="ChangeSet"/> in two saves of EF Core (<see cref="WritePlan"/>). Each statement starts from the
/// state the change set read: an update or a delete of a versioned record holds its version in its condition (the
/// concurrency token, D06), so that a record changed or removed since the loading affects no row and the whole change
/// set is refused with `CONFLIT_VERSION` (who and when, A-2.16-06); a record without version is written only under
/// the lock of its project, and compared before (<see cref="UnversionedCheck"/>, ADR 030); an insert of a key that exists since is refused by its key. Each statement must
/// affect exactly its row: after the save, every record touched by the operation is in the state the change set gives
/// it (`ChangeSet.Touched`: its content, absent, or created). The records of the domain are never handed to EF Core
/// themselves: EF Core tracks copies, which it may change.
/// </summary>
internal static class ChangeSetWriter
{
    private static readonly MethodInfo CloneMethod = typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic)!;

    public static async Task WriteAsync(AppDbContext context, EngineDialect dialect, ChangeSet changes, IReadOnlySet<Guid> lockedProjects, CancellationToken cancellationToken)
    {
        if (changes.IsEmpty)
            return;
        var plan = WritePlan.Of(changes);
        await UnversionedCheck.EnsureAsync(context, changes, lockedProjects, cancellationToken);
        await SaveAsync(context, dialect, plan.First, cancellationToken);
        await SaveAsync(context, dialect, plan.Then, cancellationToken);
    }

    private static async Task SaveAsync(AppDbContext context, EngineDialect dialect, IReadOnlyList<RecordWrite> writes, CancellationToken cancellationToken)
    {
        if (writes.Count == 0)
            return;
        try
        {
            foreach (var write in writes)
                Track(context, write);
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException conflict)
        {
            throw await VersionConflictAsync(conflict, cancellationToken);
        }
        catch (DbUpdateException failure) when (dialect.Classify(failure, context.Model) is { } error && StoreRefusals.Of(error) is { } refusal)
        {
            throw refusal;
        }
        finally
        {
            context.ChangeTracker.Clear();
        }
    }

    private static void Track(AppDbContext context, RecordWrite write)
    {
        switch (write.Kind)
        {
            case WriteKind.Insert:
                context.Add(Copy(write.To!));
                break;
            case WriteKind.Update:
                // Tracked as it was read, then given its new values: EF Core writes the changed columns only.
                context.Attach(Copy(write.From!)).CurrentValues.SetValues(write.To!);
                break;
            default:
                context.Remove(Copy(write.From!));
                break;
        }
    }

    private static object Copy(IRecord record) => CloneMethod.Invoke(record, null)!;

    /// <summary>`CONFLIT_VERSION` on the first record whose row no longer has the version read, with the change that blocks.</summary>
    private static async Task<Exception> VersionConflictAsync(DbUpdateConcurrencyException conflict, CancellationToken cancellationToken)
    {
        var entry = conflict.Entries.FirstOrDefault();
        if (entry?.Entity is not IRecord record)
            return conflict;
        var present = await entry.GetDatabaseValuesAsync(cancellationToken);
        return StoreRefusals.VersionConflict(record.Key.Type, Read<Guid?>(present, "UpdatedBy") ?? Read<Guid?>(present, "CreatedBy"),
            Read<DateTime?>(present, "UpdatedAt") ?? Read<DateTime?>(present, "CreatedAt"));
    }

    private static T? Read<T>(PropertyValues? values, string property) =>
        values is not null && values.Properties.Any(candidate => candidate.Name == property) ? values.GetValue<T>(property) : default;
}
