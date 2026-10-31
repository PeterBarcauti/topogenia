// IAssistantPorts.cs
// Ports of the assistant: the settings kept with their protected secret, the protection of the secret, the connectors to the models (spec 3.0 sections 4, 5, 7).

using Topogenia.Application.Assistant;
using Topogenia.Domain.Model;

namespace Topogenia.Application.Ports;

/// <summary>The settings as kept: the record, and the secret protected by the host (null when none is kept).</summary>
public sealed record StoredAssistantSettings(AssistantSettings Settings, string? ProtectedSecret);

/// <summary>
/// The settings of the installation, one record (spec 3.0 section 4). Read without a unit of work; written in the unit of
/// the request that changes them, its version moved on by one.
/// </summary>
public interface IAssistantSettingsStore
{
    /// <summary>The settings kept, or null before the first change (the defaults then apply).</summary>
    Task<StoredAssistantSettings?> ReadAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Writes the settings and the protected secret as given (the version and the author are already set), with the event
    /// of the audit of the change, in the unit of work of the request.
    /// </summary>
    Task WriteAsync(StoredAssistantSettings stored, AuditEvent audit, CancellationToken cancellationToken);
}

/// <summary>
/// The protection of a secret by the host (the data protection of ASP.NET Core: keys of the folder of the product, DPAPI of
/// the account in desktop mode): the store keeps only the protected text; the secret is unprotected for a call only.
/// </summary>
public interface ISecretProtector
{
    string Protect(string secret);

    /// <summary>The secret, or null when the protected text can no longer be read (keys lost, another account).</summary>
    string? Unprotect(string protectedSecret);
}

/// <summary>What a connector needs to reach its model: the settings and the secret unprotected for the call.</summary>
public sealed record AssistantConnection(AssistantSettings Settings, string? Secret);

/// <summary>
/// The account of a test of the connection: the model that answered and the time it took, the tokens of the exchange; or
/// the reason it failed, as a code the interface names (<see cref="AssistantFailures"/>) and a detail of the provider
/// without any secret.
/// </summary>
public sealed record AssistantTestOutcome(
    bool Succeeded,
    string? Model,
    int DurationMilliseconds,
    int? InputTokens,
    int? OutputTokens,
    string? Failure,
    string? Detail)
{
    public static AssistantTestOutcome Failed(string failure, int durationMilliseconds, string? detail = null) =>
        new(false, null, durationMilliseconds, null, null, failure, detail);
}

/// <summary>The reasons a test of the connection fails, as codes the interface names in its language.</summary>
public static class AssistantFailures
{
    /// <summary>No connector that calls a model is chosen.</summary>
    public const string NoConnector = "no_connector";

    /// <summary>The API of Anthropic needs its key, and none is kept or given.</summary>
    public const string KeyMissing = "key_missing";

    /// <summary>The key kept can no longer be read by the host (its keys of protection changed): enter it again.</summary>
    public const string KeyUnreadable = "key_unreadable";

    /// <summary>The key or the identity is refused by the provider (401, 403).</summary>
    public const string Refused = "refused";

    /// <summary>The named AWS profile is absent on the machine, for the account that runs the host.</summary>
    public const string ProfileMissing = "profile_missing";

    /// <summary>The SSO session of the profile expired: `aws sso login --profile …`, no workaround.</summary>
    public const string SessionExpired = "session_expired";

    /// <summary>The model is unknown to the provider, or (Bedrock) not an inference profile.</summary>
    public const string ModelUnknown = "model_unknown";

    /// <summary>Too many requests, or the credit of the account is spent.</summary>
    public const string Limited = "limited";

    /// <summary>The provider could not be reached (network, proxy, time out).</summary>
    public const string Unreachable = "unreachable";

    /// <summary>Any other failure of the provider.</summary>
    public const string Other = "other";

    /// <summary>The model declined the request (`stop_reason` `refusal`); the detail gives its category when it has one.</summary>
    public const string Declined = "declined";

    /// <summary>The reply reached the ceiling of the output of the settings before its end.</summary>
    public const string Truncated = "truncated";

    /// <summary>The reply is not the JSON object asked, or breaks its rules.</summary>
    public const string InvalidReply = "invalid_reply";

    /// <summary>The reply has a form that the scope or the right of the user does not admit (a proposal for a reader).</summary>
    public const string FormRefused = "form_refused";

    /// <summary>The person stopped the exchange before its end.</summary>
    public const string Cancelled = "cancelled";
}

/// <summary>
/// The tokens of an exchange as the provider counts them: those read from the cache and those written to it apart from
/// the others, the four billed at different prices (spec 3.0 section 5: the cost shown to the administrator).
/// </summary>
public sealed record AssistantUsage(int InputTokens, int CacheReadTokens, int CacheWriteTokens, int OutputTokens)
{
    public static AssistantUsage None { get; } = new(0, 0, 0, 0);

    public AssistantUsage Plus(AssistantUsage other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return new(InputTokens + other.InputTokens, CacheReadTokens + other.CacheReadTokens, CacheWriteTokens + other.CacheWriteTokens,
            OutputTokens + other.OutputTokens);
    }
}

/// <summary>
/// A former turn of the panel given back to the model: the request of the person and the JSON the model replied; the files
/// joined to the request, sent again at their place so that the model reads them still (and the cache keeps them).
/// </summary>
public sealed record AssistantTurn(string Request, string Reply)
{
    public IReadOnlyList<AssistantAttachment> Attachments { get; init; } = [];
}

/// <summary>
/// What the model receives for one exchange (spec 3.0 sections 3.1, 3.4, 7), in the order of its cache: the instructions and
/// the documentation of the formats (the same for every project, kept by the connector), the schema of the project in the
/// format named, the former turns of the panel, then the instruction of the practice, the map open as a `.pmap`, the codes
/// selected and the request. The forms the reply may take are said to the model and checked again by the application.
/// </summary>
public sealed record AssistantPrompt(
    AssistantIntent Intent,
    AssistantScope Scope,
    IReadOnlyList<AssistantForm> Forms,
    string? Schema,
    AssistantSchemaFormat SchemaFormat,
    IReadOnlyList<AssistantTurn> History,
    string? MapName,
    string? OpenMap,
    IReadOnlyList<string> Selection,
    string Request,
    int MaxOutputTokens)
{
    /// <summary>The files joined to the request (lot 4): a PDF or an image sent as it is, a document as the text the host read.</summary>
    public IReadOnlyList<AssistantAttachment> Attachments { get; init; } = [];

    /// <summary>The language of origin of the project and the languages configured, which a translation takes (ADR 081).</summary>
    public AssistantLanguages? Languages { get; init; }
}

/// <summary>
/// The languages of an exchange (ADR 081): the language of origin of the project, in which its texts are written (null
/// for a project yet to create), and the languages configured, the only ones a translation may take.
/// </summary>
public sealed record AssistantLanguages(string? Origin, IReadOnlyList<string> Configured)
{
    /// <summary>The language a translation is asked into (practice `translate`, ADR 083); null otherwise.</summary>
    public string? Target { get; init; }
}

/// <summary>
/// What a connector tells while the model works (spec 3.0 section 5: the panel shows that the model works): the phase,
/// the last line of the summary of its thinking when the provider gives one, the characters of the reply written so far.
/// </summary>
public sealed record AssistantProgress(AssistantPhase Phase, string? Thought, int Characters);

/// <summary>
/// The account of an exchange: the text of the reply (the JSON asked) and the model that wrote it, or the reason it failed
/// as a code of <see cref="AssistantFailures"/> and a detail of the provider without any secret; the tokens and the time.
/// </summary>
public sealed record AssistantExchangeOutcome(
    bool Succeeded,
    string? Model,
    string? Text,
    AssistantUsage Usage,
    int DurationMilliseconds,
    string? Failure,
    string? Detail)
{
    public static AssistantExchangeOutcome Failed(string failure, int durationMilliseconds, AssistantUsage? usage = null, string? detail = null) =>
        new(false, null, null, usage ?? AssistantUsage.None, durationMilliseconds, failure, detail);
}

/// <summary>A connector of the list (spec 3.0 section 4): one call that checks it can reach its model, and the exchanges of the panel.</summary>
public interface IAssistantConnector
{
    AssistantConnector Kind { get; }

    /// <summary>
    /// A minimal exchange with the model, its time measured by the connector; never throws for a failure of the provider,
    /// which the outcome says.
    /// </summary>
    Task<AssistantTestOutcome> TestAsync(AssistantConnection connection, CancellationToken cancellationToken);

    /// <summary>
    /// One exchange of the panel (spec 3.0 section 3.4): the prompt sent with the instructions of the product, the reply
    /// constrained to the JSON object of the reply; its progress told as it comes. Never throws for a failure of the
    /// provider, which the outcome says; a cancellation of the caller is thrown as it is.
    /// </summary>
    Task<AssistantExchangeOutcome> ExchangeAsync(AssistantConnection connection, AssistantPrompt prompt, IProgress<AssistantProgress> progress,
        CancellationToken cancellationToken);
}

/// <summary>
/// The instructions of the product (spec 3.0 section 7: a common one and one per tile, with the documentation of the
/// formats, texts of the project of the connectors): the whole prompt of an exchange written as one text, for the copy and paste
/// into the AI of the person (spec 3.0 section 5), the same that a connector sends.
/// </summary>
public interface IAssistantInstructions
{
    string Render(AssistantPrompt prompt);
}

/// <summary>
/// The reading of the text of a Word, Excel or Visio file joined to a request (spec 3.0 section 3.4: by the host, on the
/// content of the file, never by Office): the archive and its XML bounded, no DTD; the text bounded by
/// <see cref="AssistantBounds.FileTextMax"/>, cut with a line that says so beyond.
/// </summary>
public interface IDocumentTextReader
{
    /// <summary>The text of the file, or null when its content cannot be read as its kind (not an archive of the format, an XML refused, a bound passed).</summary>
    string? Read(AttachmentKind kind, byte[] content);
}

/// <summary>The ports of the assistant, given together to the requests of a user.</summary>
public sealed record AssistantPorts(IAssistantSettingsStore Settings, ISecretProtector Protector, IEnumerable<IAssistantConnector> Connectors)
{
    /// <summary>The connector of a kind, or null when the installation has none for it.</summary>
    public IAssistantConnector? Of(AssistantConnector kind) => Connectors.FirstOrDefault(connector => connector.Kind == kind);
}
