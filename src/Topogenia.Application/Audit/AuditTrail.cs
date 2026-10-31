// AuditTrail.cs
// The events of the audit trail: their kinds, how one is built with identifiers only, its details, its retention (spec 5.5, annex A 4).

using System.Text.Json;
using Topogenia.Domain.Model;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;

namespace Topogenia.Application.Audit;

/// <summary>The kinds of the audit events (spec 5.5).</summary>
public static class AuditKinds
{
    /// <summary>A login seen for the first time: the user created at his first request (A-2.1-24).</summary>
    public const string UserCreated = "user.created";

    public const string ProjectCreated = "project.created";

    /// <summary>The members or their roles replaced (A-4-27); the details give the users added, removed and changed.</summary>
    public const string MembersChanged = "project.members_changed";

    /// <summary>
    /// The classification of a project changed (spec 4.1: it governs how its exports are handled, spec 5.1 TISAX); the
    /// details give the level before and after, by their codes.
    /// </summary>
    public const string ClassificationChanged = "project.classification_changed";

    public const string ProjectTrashed = "project.trashed";

    public const string ProjectRestored = "project.restored";

    /// <summary>A project purged for good: at once by an administrator, or by the daily purge past the retention (no user).</summary>
    public const string ProjectPurged = "project.purged";

    /// <summary>
    /// The package of a project exported (spec 7.2), the details saying whether it holds the people, or its `.pmap` copy
    /// (ADR 054), the details giving the format and whether it holds the sheets.
    /// </summary>
    public const string ProjectExported = "project.exported";

    /// <summary>
    /// A project imported from a package (spec 7.3), the details giving the mode (`new` or `replace`) and whether the
    /// members were taken, or opened from a `.pmap` file, the mode `pmap` (ADR 054).
    /// </summary>
    public const string ProjectImported = "project.imported";

    /// <summary>A project duplicated (spec 5.5, S-5.5-03): the event of the copy, its details give the identifier of the source.</summary>
    public const string ProjectDuplicated = "project.duplicated";

    /// <summary>A user refused <see cref="AuditTrail.RefusalThreshold"/> times within <see cref="AuditTrail.RefusalWindow"/>.</summary>
    public const string RepeatedRefusals = "access.repeated_refusals";

    /// <summary>The settings of the assistant changed (3.0): the details give the connector and the names of the fields changed, never a secret.</summary>
    public const string AssistantSettingsChanged = "assistant.settings_changed";

    /// <summary>The connection of the assistant tested (3.0): connector, model, outcome, time and tokens.</summary>
    public const string AssistantConnectionTested = "assistant.connection_tested";

    /// <summary>
    /// An exchange of the panel of the assistant (3.0, spec section 6): connector, model, scope, practice, form, outcome,
    /// tokens and time; never the request, the map sent nor the reply.
    /// </summary>
    public const string AssistantExchanged = "assistant.exchanged";
}

/// <summary>
/// Builds the audit events: who (the user, none for a job of the host), when, what (the kind and the project), and details
/// made of identifiers and counts only (spec 5.5: no name, no login, no text of the user). The details hold at most
/// <see cref="DetailsMax"/> characters, the length of their column: a list too long is given by its counts.
/// </summary>
public static class AuditTrail
{
    /// <summary>The length of `audit_event.details`.</summary>
    public const int DetailsMax = 4000;

    /// <summary>The retention of the trail when the configuration names none: one year (spec 5.5).</summary>
    public const int DefaultRetentionDays = 365;

    /// <summary>
    /// The retention in days of a setting: <see cref="DefaultRetentionDays"/> when the setting is not positive, as the
    /// retention of the trash (`TrashRules.RetentionDays`): a zero or a negative value never erases the whole trail.
    /// </summary>
    public static int RetentionDays(int setting) => setting > 0 ? setting : DefaultRetentionDays;

    /// <summary>The refusals of one user that make an event, within <see cref="RefusalWindow"/>.</summary>
    public const int RefusalThreshold = 10;

    public static readonly TimeSpan RefusalWindow = TimeSpan.FromMinutes(10);

    public static AuditEvent Event(OperationContext context, string kind, Guid? userId, Guid? projectId, string? details = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        if (details is { Length: > DetailsMax })
            throw new ArgumentException("The details of an audit event exceed their column.", nameof(details));
        return new AuditEvent(context.NewId(), context.Now, userId, kind, projectId, details);
    }

    /// <summary>An event of a job of the host (the daily purge): no user, an identifier made at its time (ADR 007).</summary>
    public static AuditEvent OfJob(DateTime now, string kind, Guid? projectId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        return new AuditEvent(Guid.CreateVersion7(), now, null, kind, projectId, null);
    }

    /// <summary>The event of a series of refusals of access of a user, with the threshold and the window that made it.</summary>
    public static AuditEvent OfRefusals(DateTime now, Guid userId) =>
        new(Guid.CreateVersion7(), now, userId, AuditKinds.RepeatedRefusals, null,
            JsonSerializer.Serialize(new { refusals = RefusalThreshold, window_minutes = (int)RefusalWindow.TotalMinutes }));

    /// <summary>
    /// The event of an export: a read, so written alone outside any unit of work, with an identifier made at its time (ADR
    /// 007); its details say whether the package holds the people (spec 5.4).
    /// </summary>
    public static AuditEvent OfExport(DateTime now, Guid userId, Guid projectId, bool withoutPeople) =>
        new(Guid.CreateVersion7(), now, userId, AuditKinds.ProjectExported, projectId,
            JsonSerializer.Serialize(new { without_people = withoutPeople }));

    /// <summary>The event of the `.pmap` copy of a project, written alone like the export of a package; its details say whether it holds the sheets.</summary>
    public static AuditEvent OfPmapCopy(DateTime now, Guid userId, Guid projectId, bool withSheets) =>
        new(Guid.CreateVersion7(), now, userId, AuditKinds.ProjectExported, projectId,
            JsonSerializer.Serialize(new { format = "pmap", with_sheets = withSheets }));

    /// <summary>
    /// The event of an exchange of the assistant, written alone like the event of an export (it changes nothing); the project
    /// is null for a project yet to create.
    /// </summary>
    public static AuditEvent OfAssistantExchange(DateTime now, Guid userId, Guid? projectId, string details) =>
        new(Guid.CreateVersion7(), now, userId, AuditKinds.AssistantExchanged, projectId, details);

    /// <summary>The event of a change of a project by its actor, or null when nothing changed.</summary>
    public static AuditEvent? OfChange(OperationContext context, string kind, Guid projectId, ChangeSet changes, string? details = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(changes);
        return changes.IsEmpty ? null : Event(context, kind, context.ActorId, projectId, details);
    }

    /// <summary>The details of a change of the classification: the codes of the level before and after; null when the project did not change.</summary>
    public static string? ClassificationDetails(ChangeSet changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        return changes.Modified<Project>().Select(change => JsonSerializer.Serialize(new
        {
            from = Codes.Of(change.Before.Classification),
            to = Codes.Of(change.After.Classification),
        })).SingleOrDefault();
    }

    /// <summary>
    /// The details of a change of the members: the users added, removed and whose role or flags changed, by identifier;
    /// their counts when the identifiers would not fit.
    /// </summary>
    public static string MembersDetails(ChangeSet changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        var members = changes.Changes.Where(change => (change.After ?? change.Before) is ProjectMember).ToList();
        Guid[] Of(ChangeKind kind) => [.. members.Where(change => change.Kind == kind).Select(change => ((ProjectMember)(change.After ?? change.Before)!).UserId).Order()];
        var added = Of(ChangeKind.Created);
        var removed = Of(ChangeKind.Removed);
        var changed = Of(ChangeKind.Modified);
        var full = JsonSerializer.Serialize(new { added, removed, changed });
        return full.Length <= DetailsMax ? full : JsonSerializer.Serialize(new { added = added.Length, removed = removed.Length, changed = changed.Length });
    }
}
