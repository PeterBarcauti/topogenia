// UnversionedCheck.cs
// Before a change set is written, every record without version that it modifies or removes must be under the lock of its project and still as it was loaded (D06).

using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Topogenia.Domain.Model;
using Topogenia.Domain.Workspace;
using Topogenia.Persistence.Errors;
using Topogenia.Persistence.Loading;

namespace Topogenia.Persistence.Writing;

/// <summary>
/// A record without version (counter, deletion batch, procedure row, frame step...) has no column to compare in the
/// statement that writes it, and the statement that writes it has no condition: in SQL Server the row is read back under
/// row versioning (RCSI), so a concurrent unit could change it between this reading and the write, and one of the two
/// writes would be lost. The contract (ADR 030): a record without version is modified or removed only by a unit that
/// holds the lock of its project (spec 4.7), which serialises every unit that writes the project; then the reading below
/// sees the last committed state and nothing can change it before the commit. The check enforces it: the rows are read
/// back by their key through the records of the projects locked by the unit (<see cref="ProjectScope"/>), in packets of
/// 500 identifiers (spec 4.3-2); a record that exists outside these projects, or a unit that locked no project, is
/// refused with <see cref="UnlockedWriteException"/> before anything is written. A row changed or gone since the loading
/// refuses the whole change set with `CONFLIT_VERSION`, as a versioned record would (D06): nothing is overwritten silently.
/// </summary>
internal static class UnversionedCheck
{
    private static readonly MethodInfo ReadMethod = typeof(UnversionedCheck).GetMethod(nameof(ReadAsync), BindingFlags.NonPublic | BindingFlags.Static)!;

    public static async Task EnsureAsync(AppDbContext context, ChangeSet changes, IReadOnlySet<Guid> lockedProjects, CancellationToken cancellationToken)
    {
        var expected = changes.Changes
            .Where(change => change.Kind != ChangeKind.Created && change.Before is not IVersionedRecord)
            .Select(change => change.Before!)
            .GroupBy(record => record.GetType());
        foreach (var group in expected)
        {
            var records = group.ToList();
            if (lockedProjects.Count == 0)
                throw UnlockedWriteException.Of(records[0].Key.Type);
            var present = await PresentAsync(context, group.Key, records, lockedProjects, cancellationToken);
            var missing = records.Where(record => !present.ContainsKey(record.Key)).ToList();
            if (missing.Count > 0 && (await PresentAsync(context, group.Key, missing, null, cancellationToken)).Count > 0)
                throw UnlockedWriteException.Of(records[0].Key.Type);
            foreach (var record in records)
            {
                if (!present.TryGetValue(record.Key, out var row) || !row.Equals(record))
                    throw StoreRefusals.VersionConflict(record.Key.Type, null, null);
            }
        }
    }

    /// <summary>
    /// The rows of these records read by the first column of their key, packet by packet: among the records of the
    /// locked projects, or anywhere when <paramref name="projects"/> is null.
    /// </summary>
    private static async Task<Dictionary<RecordKey, IRecord>> PresentAsync(
        AppDbContext context, Type type, IReadOnlyList<IRecord> records, IReadOnlySet<Guid>? projects, CancellationToken cancellationToken)
    {
        var key = context.Model.FindEntityType(type)?.FindPrimaryKey()?.Properties[0]
            ?? throw new InvalidOperationException($"{type.Name} is not a record of the model.");
        if (key.ClrType != typeof(Guid))
            throw new NotSupportedException($"The key of {type.Name} does not start with an identifier.");
        var getter = key.GetGetter();
        var read = ReadMethod.MakeGenericMethod(type);
        IReadOnlyList<ProjectScope?> scopes = projects is null ? [null] : [.. projects.Select(project => new ProjectScope(context, project))];
        var present = new Dictionary<RecordKey, IRecord>();
        foreach (var chunk in Chunks.Of(records.Select(record => (Guid)getter.GetClrValue(record)!)))
        {
            foreach (var scope in scopes)
            {
                var rows = await (Task<List<IRecord>>)read.Invoke(null, [context, scope, key.Name, chunk, cancellationToken])!;
                foreach (var row in rows)
                    present[row.Key] = row;
            }
        }
        return present;
    }

    private static async Task<List<IRecord>> ReadAsync<T>(AppDbContext context, ProjectScope? scope, string property, List<Guid> identifiers, CancellationToken cancellationToken)
        where T : class, IRecord
    {
        var rows = scope is null
            ? context.Set<T>().AsNoTracking()
            : scope.Unversioned<T>() ?? throw new NotSupportedException($"{typeof(T).Name} belongs to no project: it cannot be written under the lock of one.");
        return [.. await rows.Where(record => identifiers.Contains(EF.Property<Guid>(record, property))).ToListAsync(cancellationToken)];
    }
}
