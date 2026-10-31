// AssistantRequests.cs
// `/api/admin/assistant`: the settings of the assistant read, changed (versioned, audited) and the connection tested, by the administrator only (spec 3.0 section 4).

using System.Text.Json;
using Topogenia.Application.Audit;
using Topogenia.Application.Authorization;
using Topogenia.Application.Commands;
using Topogenia.Application.Ports;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Values;

namespace Topogenia.Application.Assistant;

/// <summary>
/// The settings as the dialog shows them: never the secret, only whether one is kept and its last characters; the codes
/// of the connectors of the list and of the classifications.
/// </summary>
public sealed record AssistantSettingsView(
    string Connector,
    string? Model,
    string? Region,
    string? AwsProfile,
    int MaxOutputTokens,
    IReadOnlyList<string> AllowedClassifications,
    bool HasSecret,
    string? SecretHint,
    int Version,
    DateTime? UpdatedAt,
    IReadOnlyList<string> Connectors,
    AssistantFileBoundsView Files)
{
    public static AssistantSettingsView Of(AssistantSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new AssistantSettingsView(Codes.Of(settings.Connector), settings.Model, settings.Region, settings.AwsProfile, settings.MaxOutputTokens,
            settings.AllowedClassifications.Select(Codes.Of).ToList(), settings.HasSecret, settings.SecretHint, settings.Version, settings.UpdatedAt,
            Codes.All<AssistantConnector>(), AssistantAttachmentRules.ViewOf(AssistantFileBounds.Of(settings.Connector)));
    }
}

/// <summary>The settings kept, or the defaults of a new installation.</summary>
internal static class AssistantSettingsReading
{
    public static async Task<StoredAssistantSettings> ReadAsync(UserScope scope, CancellationToken cancellationToken) =>
        await scope.Ports.Assistant.Settings.ReadAsync(cancellationToken) ?? new StoredAssistantSettings(AssistantSettings.Default, null);
}

/// <summary>`GET /api/admin/assistant` (new in 3.0, administrator): the settings, without the secret.</summary>
[RequiresAdmin]
public sealed class ReadAssistantSettingsQuery : UserRequest<AssistantSettingsView>
{
    public override async Task<AssistantSettingsView> RunAsync(UserScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        return AssistantSettingsView.Of((await AssistantSettingsReading.ReadAsync(scope, cancellationToken)).Settings);
    }
}

/// <summary>
/// `PUT /api/admin/assistant` with If-Match (new in 3.0, administrator): the settings checked by
/// <see cref="AssistantRules.Check"/>, the secret protected by the host before it is kept, the version moved on by one and
/// an event of the audit naming the fields changed (never a value of the secret). Unchanged settings write nothing.
/// </summary>
[RequiresAdmin]
public sealed class ChangeAssistantSettingsCommand(AssistantSettingsChange change) : UserRequest<AssistantSettingsView>
{
    public int? Version { get; init; }

    public override bool InUnitOfWork => true;

    public override async Task<AssistantSettingsView> RunAsync(UserScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var stored = await AssistantSettingsReading.ReadAsync(scope, cancellationToken);
        var current = stored.Settings;
        if (Version is { } read && read != current.Version)
        {
            throw DomainException.Conflict(ErrorCodes.VersionConflict, new Dictionary<string, object?>
            {
                ["object"] = "assistant_settings", ["updated_by"] = current.UpdatedBy, ["updated_at"] = current.UpdatedAt,
            });
        }
        var checkedSettings = AssistantRules.Check(current, change);
        var fields = AssistantRules.ChangedFields(current, checkedSettings.Settings, checkedSettings.Secret);
        if (fields.Count == 0)
            return AssistantSettingsView.Of(current);
        var protectedSecret = checkedSettings.Secret.Kind switch
        {
            SecretKind.Replace => scope.Ports.Assistant.Protector.Protect(checkedSettings.Secret.Value!),
            SecretKind.Remove => null,
            _ => stored.ProtectedSecret,
        };
        var settings = checkedSettings.Settings with
        {
            Version = current.Version + 1, UpdatedAt = scope.Operation.Now, UpdatedBy = scope.User.Id,
        };
        var details = JsonSerializer.Serialize(new { connector = Codes.Of(settings.Connector), fields });
        var audit = AuditTrail.Event(scope.Operation, AuditKinds.AssistantSettingsChanged, scope.User.Id, null, details);
        await scope.Ports.Assistant.Settings.WriteAsync(new StoredAssistantSettings(settings, protectedSecret), audit, cancellationToken);
        return AssistantSettingsView.Of(settings);
    }

    /// <summary>The If-Match of the request on the settings, when the client sent one.</summary>
    public static ChangeAssistantSettingsCommand WithVersion(AssistantSettingsChange change, int? version) => new(change) { Version = version };
}

/// <summary>
/// `POST /api/admin/assistant/test` (new in 3.0, administrator): one minimal exchange with the model of the settings of the
/// dialog, kept or not yet (the secret kept is used when none is given); nothing is written but the event of the audit
/// (connector, model, outcome, tokens, time; never the secret). A failure of the provider is an outcome, not an error.
/// </summary>
[RequiresAdmin]
public sealed class TestAssistantConnectionCommand(AssistantSettingsChange change) : UserRequest<AssistantTestOutcome>
{
    public override async Task<AssistantTestOutcome> RunAsync(UserScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var stored = await AssistantSettingsReading.ReadAsync(scope, cancellationToken);
        var checkedSettings = AssistantRules.Check(stored.Settings, change);
        var outcome = await RunTestAsync(scope.Ports.Assistant, stored, checkedSettings, cancellationToken);
        var settings = checkedSettings.Settings;
        var details = JsonSerializer.Serialize(new
        {
            connector = Codes.Of(settings.Connector), model = settings.Model, succeeded = outcome.Succeeded, failure = outcome.Failure,
            duration_ms = outcome.DurationMilliseconds, input_tokens = outcome.InputTokens, output_tokens = outcome.OutputTokens,
        });
        await scope.Ports.Audit.AppendAsync(AuditTrail.Event(scope.Operation, AuditKinds.AssistantConnectionTested, scope.User.Id, null, details),
            cancellationToken);
        return outcome;
    }

    private static async Task<AssistantTestOutcome> RunTestAsync(AssistantPorts ports, StoredAssistantSettings stored, CheckedAssistantSettings checkedSettings,
        CancellationToken cancellationToken)
    {
        var settings = checkedSettings.Settings;
        if (ports.Of(settings.Connector) is not { } connector)
            return AssistantTestOutcome.Failed(AssistantFailures.NoConnector, 0);
        string? secret = null;
        if (settings.Connector == AssistantConnector.Anthropic)
        {
            secret = checkedSettings.Secret.Kind switch
            {
                SecretKind.Replace => checkedSettings.Secret.Value,
                SecretKind.Keep when stored.ProtectedSecret is { } kept => ports.Protector.Unprotect(kept) ?? string.Empty,
                _ => null,
            };
            if (secret is null)
                return AssistantTestOutcome.Failed(AssistantFailures.KeyMissing, 0);
            if (secret.Length == 0)
                return AssistantTestOutcome.Failed(AssistantFailures.KeyUnreadable, 0);
        }
        return await connector.TestAsync(new AssistantConnection(settings, secret), cancellationToken);
    }
}
