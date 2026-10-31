// AssistantSettingsRow.cs
// The one row of `assistant_settings`: the settings of the assistant of the installation, the secret as the host protected it (spec 3.0 section 4).

using Topogenia.Application.Assistant;

namespace Topogenia.Persistence.Assistant;

/// <summary>
/// A row of `assistant_settings`, a record of the persistence like `schema_history` (the closed core does not know it):
/// one row only (<see cref="Id"/> is always <see cref="SingleId"/>), its version moved on by each change. The secret is
/// the text protected by the host; the classifications that may be sent are their codes, separated by commas.
/// </summary>
public sealed record AssistantSettingsRow(
    int Id,
    AssistantConnector Connector,
    string? Model,
    string? Region,
    string? AwsProfile,
    int MaxOutputTokens,
    string AllowedClassifications,
    string? ProtectedSecret,
    string? SecretHint,
    int Version,
    DateTime? UpdatedAt,
    Guid? UpdatedBy)
{
    /// <summary>The identifier of the one row.</summary>
    public const int SingleId = 1;

    /// <summary>The width of the list of the classifications (the three codes and their commas).</summary>
    public const int ClassificationsLength = 60;

    /// <summary>The width of the secret as the host protects it (the data protection of ASP.NET Core).</summary>
    public const int ProtectedSecretLength = 2000;
}
