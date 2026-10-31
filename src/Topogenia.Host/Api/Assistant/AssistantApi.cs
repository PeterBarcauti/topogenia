// AssistantApi.cs
// The routes of the settings of the assistant turned into their requests: read, changed with If-Match, the connection tested (spec 3.0 section 4).

using Topogenia.Application.Assistant;
using Topogenia.Application.Commands;
using Topogenia.Application.Ports;

namespace Topogenia.Host.Api.Assistant;

/// <summary>
/// `PUT /api/admin/assistant` and `POST /api/admin/assistant/test` (new in 3.0): the connector of the list, its model,
/// region and AWS profile, the ceiling of a reply, the classifications that may be sent; the secret null keeps the one
/// kept, an empty secret removes it, any other text replaces it. It is written, never read back.
/// </summary>
public sealed record AssistantSettingsBody(
    string? Connector,
    string? Model,
    string? Region,
    string? AwsProfile,
    int? MaxOutputTokens,
    List<string>? AllowedClassifications,
    string? Secret)
{
    public AssistantSettingsChange ToChange() => new(Connector, Model, Region, AwsProfile, MaxOutputTokens, AllowedClassifications, Secret);
}

/// <summary>The settings of the assistant, by the administrator only (<see cref="ReadAssistantSettingsQuery"/> and the commands beside it).</summary>
public sealed class AssistantApi(UserRunner users)
{
    public Task<AssistantSettingsView> ReadAsync(CancellationToken cancellationToken) => users.RunAsync(new ReadAssistantSettingsQuery(), cancellationToken);

    public Task<AssistantSettingsView> ChangeAsync(AssistantSettingsBody body, string? ifMatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        return users.RunAsync(ChangeAssistantSettingsCommand.WithVersion(body.ToChange(), ApiInput.Version(ifMatch)), cancellationToken);
    }

    public Task<AssistantTestOutcome> TestAsync(AssistantSettingsBody body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        return users.RunAsync(new TestAssistantConnectionCommand(body.ToChange()), cancellationToken);
    }
}
