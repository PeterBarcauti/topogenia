// AssistantSettingsStore.cs
// Adapter of the port of the settings of the assistant: the one row read in a consistent read, written with the event of its audit in the unit of the request (spec 3.0 section 4).

using Microsoft.EntityFrameworkCore;
using Topogenia.Application.Assistant;
using Topogenia.Application.Ports;
using Topogenia.Domain.Model;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;
using Topogenia.Persistence.Errors;
using Topogenia.Persistence.Loading;
using Topogenia.Persistence.Model;
using Topogenia.Persistence.Writing;

namespace Topogenia.Persistence.Assistant;

/// <summary>
/// <see cref="IAssistantSettingsStore"/> on a <see cref="StoreSession"/>, by LINQ only. The row is written only inside a
/// unit of work, with the event of the audit of the change in the same transaction; its version is the concurrency token,
/// so that two administrators who changed the settings at once do not overwrite each other silently: the second is
/// refused `CONFLIT_VERSION`, as a record of the domain (<see cref="ChangeSetWriter"/>).
/// </summary>
public sealed class AssistantSettingsStore : IAssistantSettingsStore
{
    private readonly StoreSession session;

    public AssistantSettingsStore(StoreSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        this.session = session;
    }

    public Task<StoredAssistantSettings?> ReadAsync(CancellationToken cancellationToken) =>
        ConsistentRead.RunAsync(session, async context =>
        {
            var row = await context.Set<AssistantSettingsRow>().AsNoTracking().SingleOrDefaultAsync(cancellationToken);
            return row is null ? null : Of(row);
        }, cancellationToken);

    public async Task WriteAsync(StoredAssistantSettings stored, AuditEvent audit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stored);
        ArgumentNullException.ThrowIfNull(audit);
        if (!session.InUnit)
            throw new InvalidOperationException("The settings of the assistant are written in a unit of work.");
        var context = session.Context;
        var row = RowOf(stored);
        var kept = await context.Set<AssistantSettingsRow>().SingleOrDefaultAsync(cancellationToken);
        if (kept is null)
        {
            context.Add(row);
        }
        else
        {
            var entry = context.Entry(kept);
            entry.CurrentValues.SetValues(row);
            // The version read is the original value of the concurrency token: a change written meanwhile is a conflict.
            entry.Property(setting => setting.Version).OriginalValue = stored.Settings.Version - 1;
        }
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw await ConflictAsync(context, cancellationToken);
        }
        catch (DbUpdateException failure) when (kept is null && session.Dialect.Classify(failure, context.Model) is { Kind: StoreErrorKind.Unique })
        {
            // The first settings saved by another administrator since they were read: the row is there now.
            throw await ConflictAsync(context, cancellationToken);
        }
        finally
        {
            context.ChangeTracker.Clear();
        }
        await ChangeSetWriter.WriteAsync(context, session.Dialect, new ChangeSet([new RecordChange(ChangeKind.Created, null, audit)]),
            session.LockedProjects, cancellationToken);
    }

    /// <summary>`CONFLIT_VERSION`, with the author and the time of the settings written meanwhile.</summary>
    private static async Task<Exception> ConflictAsync(DbContext context, CancellationToken cancellationToken)
    {
        var present = await context.Set<AssistantSettingsRow>().AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        return StoreRefusals.VersionConflict(TableNames.AssistantSettings, present?.UpdatedBy, present?.UpdatedAt);
    }

    private static StoredAssistantSettings Of(AssistantSettingsRow row)
    {
        var allowed = row.AllowedClassifications.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(code => Codes.Parse<Classification>(code, "allowed_classifications")).ToList();
        var settings = new AssistantSettings(row.Connector, row.Model, row.Region, row.AwsProfile, row.MaxOutputTokens, allowed, row.SecretHint,
            row.Version, row.UpdatedAt, row.UpdatedBy);
        return new StoredAssistantSettings(settings, row.ProtectedSecret);
    }

    private static AssistantSettingsRow RowOf(StoredAssistantSettings stored)
    {
        var settings = stored.Settings;
        return new AssistantSettingsRow(AssistantSettingsRow.SingleId, settings.Connector, settings.Model, settings.Region, settings.AwsProfile,
            settings.MaxOutputTokens, string.Join(',', settings.AllowedClassifications.Select(Codes.Of)), stored.ProtectedSecret, settings.SecretHint,
            settings.Version, settings.UpdatedAt, settings.UpdatedBy);
    }
}
