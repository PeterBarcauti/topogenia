// CommentLineStore.cs
// The lines of one table kept beside the closed core by subject and language, the comments of presentation of the steps or of the flows (ADR 099) and the voices of the projects (ADR 103): every line read for some subjects, lines written or removed together in the unit of the request with the version read of each checked, lines added after the records of an import.

using Microsoft.EntityFrameworkCore;
using Topogenia.Application.Presentation;
using Topogenia.Persistence.Errors;
using Topogenia.Persistence.Loading;
using Topogenia.Persistence.Writing;

namespace Topogenia.Persistence.Presentation;

/// <summary>
/// One line to write in such a table: created or replaced by <see cref="Line"/>, or removed when it is null, over the
/// version read of it (0 for none); its subject is the step or the flow of a comment, the project of a voice.
/// </summary>
internal sealed record LineWrite<TLine>(Guid SubjectId, string Language, TLine? Line, int VersionRead)
    where TLine : class;

/// <summary>
/// What the store reads of a line whatever its record: its key (its subject and its language), its version, the author and
/// the time of its last change, which a refusal of its version names.
/// </summary>
internal readonly record struct LineStamp(Guid SubjectId, string Language, int Version, Guid UpdatedBy, DateTime UpdatedAt)
{
    /// <summary>The stamp of a line of a comment, of a step or of a flow.</summary>
    public static LineStamp Of(CommentLine line) => new(line.SubjectId, line.Language, line.Version, line.UpdatedBy, line.UpdatedAt);
}

/// <summary>
/// What <see cref="PresentationCommentStore"/>, <see cref="FlowPresentationCommentStore"/> and
/// <see cref="PresentationVoiceStore"/> do, written once, on a <see cref="StoreSession"/>, by LINQ only. A line is a
/// record of the persistence, beside the records of the closed core (which does not know it): its key is its subject (the
/// step or the flow of a comment, the project of a voice) and its language, its version the concurrency token, so that
/// two writers of the same line do not overwrite each other silently: the second is refused `CONFLIT_VERSION`. It is
/// written only inside a unit of work, the lines of one request in one save: all or none; the reads go by packets of
/// identifiers.
/// </summary>
/// <typeparam name="TLine">The record of a line of the table.</typeparam>
internal sealed class CommentLineStore<TLine>
    where TLine : class
{
    /// <summary>The property of the version of a line, named alike by the records of every such table: the concurrency token.</summary>
    private const string VersionProperty = nameof(LineStamp.Version);

    private readonly StoreSession session;
    private readonly string table;
    private readonly string subjectProperty;
    private readonly Func<TLine, LineStamp> read;

    /// <summary>
    /// The lines of <paramref name="table"/>, whose subject is kept by the property <paramref name="subjectProperty"/> of
    /// their record; <paramref name="read"/> gives the stamp of a line whatever its record.
    /// </summary>
    public CommentLineStore(StoreSession session, string table, string subjectProperty, Func<TLine, LineStamp> read)
    {
        ArgumentNullException.ThrowIfNull(session);
        this.session = session;
        this.table = table;
        this.subjectProperty = subjectProperty;
        this.read = read;
    }

    /// <summary>Every line of these subjects, in one consistent read or in the unit of the request.</summary>
    public Task<IReadOnlyList<TLine>> OfAsync(IReadOnlyCollection<Guid> subjectIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(subjectIds);
        return ConsistentRead.RunAsync<IReadOnlyList<TLine>>(session, async context =>
        {
            var found = new List<TLine>();
            foreach (var chunk in Chunks.Of(subjectIds))
                found.AddRange(await Of(context, chunk).AsNoTracking().ToListAsync(cancellationToken));
            return found;
        }, cancellationToken);
    }

    /// <summary>Each line written or removed in the unit of work of the request, all or none, the version read of each checked.</summary>
    public async Task WriteAsync(IReadOnlyList<LineWrite<TLine>> writes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(writes);
        if (writes.Any(write => write.Line is { } line && KeyOf(line) != (write.SubjectId, write.Language)))
            throw new ArgumentException("A line is written on its own subject and language.", nameof(writes));
        if (writes.Select(write => (write.SubjectId, write.Language)).Distinct().Count() != writes.Count)
            throw new ArgumentException("A line is written once in a request.", nameof(writes));
        if (writes.Count == 0)
            return;
        var context = UnitContext();
        try
        {
            var kept = await KeptAsync(context, writes, tracked: true, cancellationToken);
            foreach (var write in writes)
            {
                kept.TryGetValue((write.SubjectId, write.Language), out var row);
                if ((row is null ? 0 : read(row).Version) != write.VersionRead)
                    throw Conflict(row);
                Track(context, write, row);
            }
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException conflict)
        {
            // A line changed or removed since it was read: the one of the first statement that affected no row.
            var changed = conflict.Entries.Select(entry => entry.Entity).OfType<TLine>().FirstOrDefault();
            context.ChangeTracker.Clear();
            var present = await KeptAsync(context, writes, tracked: false, cancellationToken);
            throw Conflict(changed is null ? null : present.GetValueOrDefault(KeyOf(changed)));
        }
        catch (DbUpdateException failure) when (session.Dialect.Classify(failure, context.Model) is { Kind: StoreErrorKind.Unique })
        {
            // A first line written by another facilitator since it was read: the row is there now.
            context.ChangeTracker.Clear();
            var present = await KeptAsync(context, writes, tracked: false, cancellationToken);
            throw Conflict(writes.Where(write => write.VersionRead == 0).Select(write => present.GetValueOrDefault((write.SubjectId, write.Language)))
                .FirstOrDefault(row => row is not null));
        }
        finally
        {
            context.ChangeTracker.Clear();
        }
    }

    /// <summary>The lines of an import added in its unit of work, after the records they name.</summary>
    public async Task AddAsync(IReadOnlyList<TLine> lines, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lines);
        if (lines.Count == 0)
            return;
        var context = UnitContext();
        try
        {
            context.AddRange(lines);
            await context.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            context.ChangeTracker.Clear();
        }
    }

    /// <summary>The lines of the table whose subject is one of a packet of identifiers.</summary>
    private IQueryable<TLine> Of(AppDbContext context, List<Guid> chunk) =>
        context.Set<TLine>().Where(line => chunk.Contains(EF.Property<Guid>(line, subjectProperty)));

    private (Guid SubjectId, string Language) KeyOf(TLine line)
    {
        var stamp = read(line);
        return (stamp.SubjectId, stamp.Language);
    }

    /// <summary>The lines kept of the subjects the writes name, by subject and language.</summary>
    private async Task<Dictionary<(Guid SubjectId, string Language), TLine>> KeptAsync(AppDbContext context, IReadOnlyList<LineWrite<TLine>> writes, bool tracked,
        CancellationToken cancellationToken)
    {
        var kept = new Dictionary<(Guid, string), TLine>();
        foreach (var chunk in Chunks.Of([.. writes.Select(write => write.SubjectId).Distinct()]))
        {
            var rows = Of(context, chunk);
            foreach (var row in await (tracked ? rows : rows.AsNoTracking()).ToListAsync(cancellationToken))
                kept[KeyOf(row)] = row;
        }
        return kept;
    }

    /// <summary>One write on the context: the version read is the original value of the concurrency token, so that a change written meanwhile affects no row.</summary>
    private static void Track(AppDbContext context, LineWrite<TLine> write, TLine? row)
    {
        if (row is null)
        {
            if (write.Line is { } created)
                context.Add(created);
            return;
        }
        var entry = context.Entry(row);
        if (write.Line is { } line)
            entry.CurrentValues.SetValues(line);
        else
            context.Remove(row);
        entry.Property(VersionProperty).OriginalValue = write.VersionRead;
    }

    private AppDbContext UnitContext() =>
        session.InUnit ? session.Context : throw new InvalidOperationException($"The lines of {table} are written in a unit of work.");

    /// <summary>`CONFLIT_VERSION`, with the author and the time of the line written meanwhile (none when it was removed).</summary>
    private Exception Conflict(TLine? present)
    {
        LineStamp? stamp = present is null ? null : read(present);
        return StoreRefusals.VersionConflict(table, stamp?.UpdatedBy, stamp?.UpdatedAt);
    }
}
