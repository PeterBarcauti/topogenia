// AssistantSettings.cs
// The settings of the assistant of the installation: the connector chosen in the list, its model, region and profile, the resource of Azure or the port of this machine, the ceiling of a reply, the classifications that may be sent, the hint of the secret (spec 3.0 section 4, ADR 098).

using System.Text.RegularExpressions;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Values;

namespace Topogenia.Application.Assistant;

/// <summary>
/// The connectors of the list of the settings (spec 3.0 section 4, decision of the owner of the product of 29/10/2026;
/// the great providers added on 11/11/2026, ADR 098), in the order of the list, family after family: none and the copy
/// and paste; a model on this machine (Ollama, LM Studio); the clouds of a company (AWS Bedrock, Microsoft Azure
/// OpenAI); the American makers (Anthropic, OpenAI, Google Gemini, xAI Grok); the European one (Mistral AI); the Chinese
/// ones (DeepSeek, Moonshot Kimi, Alibaba Qwen). Stored by their code, never by their rank.
/// </summary>
public enum AssistantConnector
{
    None, CopyPaste, Ollama, LmStudio, Bedrock, AzureOpenAi, Anthropic, OpenAi, Gemini, Grok, Mistral, DeepSeek, Kimi, Qwen,
}

/// <summary>
/// The settings, one record for the installation. The secret (the key of the API of Anthropic) is never in it: it is
/// kept protected beside it, and only its last characters are shown (<see cref="SecretHint"/>).
/// </summary>
public sealed record AssistantSettings(
    AssistantConnector Connector,
    string? Model,
    string? Region,
    string? AwsProfile,
    int MaxOutputTokens,
    IReadOnlyList<Classification> AllowedClassifications,
    string? SecretHint,
    int Version,
    DateTime? UpdatedAt,
    Guid? UpdatedBy)
{
    /// <summary>
    /// The ceiling of the output of one reply, when the administrator sets none: 64 000 tokens, room for the translation of
    /// a whole process (decision of the owner of the product, 30/10/2026: a translation is rare and worth its cost).
    /// </summary>
    public const int DefaultMaxOutputTokens = 64000;

    /// <summary>The settings of a new installation: no connector, nothing goes out (D14), only an internal project may go once one is chosen.</summary>
    public static AssistantSettings Default { get; } =
        new(AssistantConnector.None, null, null, null, DefaultMaxOutputTokens, [Classification.Internal], null, 0, null, null);

    public bool HasSecret => SecretHint is not null;

    /// <summary>The resource of Microsoft Azure OpenAI (`contoso` for contoso.openai.azure.com), its address built, never typed.</summary>
    public string? Resource { get; init; }

    /// <summary>The port of a model on this machine (Ollama, LM Studio), null for the one of the program.</summary>
    public int? Port { get; init; }
}

/// <summary>
/// A change of the settings asked by the administrator: every field given, but the classifications and the secret: a null
/// list keeps the classifications kept; a null secret keeps the one kept, an empty secret removes it, any other text
/// replaces it.
/// </summary>
public sealed record AssistantSettingsChange(
    string? Connector,
    string? Model,
    string? Region,
    string? AwsProfile,
    int? MaxOutputTokens,
    IReadOnlyList<string>? AllowedClassifications,
    string? Secret)
{
    /// <summary>The resource of Azure, for its connector only.</summary>
    public string? Resource { get; init; }

    /// <summary>The port of a model on this machine, for Ollama and LM Studio only.</summary>
    public int? Port { get; init; }
}

/// <summary>The settings checked and what becomes of the secret: kept, removed, or replaced by the text given.</summary>
public sealed record CheckedAssistantSettings(AssistantSettings Settings, SecretChange Secret);

public enum SecretKind { Keep, Remove, Replace }

public sealed record SecretChange(SecretKind Kind, string? Value)
{
    public static SecretChange Keep { get; } = new(SecretKind.Keep, null);
}

/// <summary>The rules of the settings (spec 3.0 section 4): each field checked, the defaults of each connector.</summary>
public static partial class AssistantRules
{
    /// <summary>The default model of the API of Anthropic (spec 3.0 section 5).</summary>
    public const string DefaultAnthropicModel = "claude-opus-5-5";

    public const int ModelMax = 200;

    public const int ProfileMax = 100;

    public const int SecretMax = 500;

    /// <summary>
    /// The least length of a key: no provider of the list gives one shorter than 32 characters; below 8, what is typed
    /// is no key, and nothing of it is kept, shown nor sent.
    /// </summary>
    public const int SecretMin = 8;

    public const int MinOutputTokens = 256;

    /// <summary>The output ceiling of the current models (128 000 tokens).</summary>
    public const int MaxOutputTokens = 128000;

    /// <summary>The most characters of the end of a secret shown to the administrator, and never more than a quarter of it.</summary>
    public const int HintLength = 4;

    [GeneratedRegex("^[A-Za-z0-9._:/-]+$")]
    private static partial Regex ModelPattern();

    /// <summary>A region of AWS: `eu-central-1`, `us-east-1`, `us-gov-west-1`.</summary>
    [GeneratedRegex("^[a-z]{2}(-[a-z]+)+-[0-9]$")]
    private static partial Regex RegionPattern();

    [GeneratedRegex("^[A-Za-z0-9._-]+$")]
    private static partial Regex ProfilePattern();

    /// <summary>A resource of Azure, the first part of its host name: lower case letters, digits and hyphens, 2 to 64.</summary>
    [GeneratedRegex("^[a-z0-9][a-z0-9-]{1,63}$")]
    private static partial Regex ResourcePattern();

    /// <summary>
    /// The settings that the change gives, from the current ones: VALEUR_INVALIDE on a connector, a model, a region, a
    /// profile, a resource, a port, a ceiling or a classification outside its list or its form; CHAMP_OBLIGATOIRE for the
    /// region of Bedrock, the resource of Azure and the model of every connector but Anthropic (which has its default);
    /// TEXTE_TROP_LONG past the length of a field. The fields a connector does not use are cleared.
    /// </summary>
    public static CheckedAssistantSettings Check(AssistantSettings current, AssistantSettingsChange change)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(change);
        var connector = Codes.Parse<AssistantConnector>(change.Connector, "connector");
        var model = Formed(TextRules.Optional(change.Model, "model", ModelMax), ModelPattern(), "model");
        var region = Formed(TextRules.Optional(change.Region, "region", 40), RegionPattern(), "region");
        var profile = Formed(TextRules.Optional(change.AwsProfile, "aws_profile", ProfileMax), ProfilePattern(), "aws_profile");
        var resource = Formed(TextRules.Optional(change.Resource, "resource", 64), ResourcePattern(), "resource");
        if (change.Port is < 1 or > 65535)
            throw DomainException.InvalidValue("port");
        var ceiling = change.MaxOutputTokens ?? AssistantSettings.DefaultMaxOutputTokens;
        if (ceiling is < MinOutputTokens or > MaxOutputTokens)
            throw DomainException.InvalidValue("max_output_tokens");
        var allowed = change.AllowedClassifications is { } codes
            ? codes.Select(code => Codes.Parse<Classification>(code, "allowed_classifications")).Distinct().Order().ToList()
            : current.AllowedClassifications;
        var secret = SecretOf(change.Secret);
        (model, region, profile) = connector switch
        {
            AssistantConnector.Anthropic => (model ?? DefaultAnthropicModel, null, null),
            AssistantConnector.Bedrock => (model ?? throw DomainException.Invalid(ErrorCodes.RequiredField, "model"),
                region ?? throw DomainException.Invalid(ErrorCodes.RequiredField, "region"), profile),
            _ when AssistantConnectors.CallsModel(connector) => (model ?? throw DomainException.Invalid(ErrorCodes.RequiredField, "model"), null, null),
            _ => ((string?)null, (string?)null, (string?)null),
        };
        resource = connector == AssistantConnector.AzureOpenAi ? resource ?? throw DomainException.Invalid(ErrorCodes.RequiredField, "resource") : null;
        var port = AssistantConnectors.OnThisMachine(connector) ? change.Port : null;
        // A key belongs to its provider: the one kept for another connector, or for another resource of Azure, is never
        // given to the one chosen (ADR 098).
        if ((connector != current.Connector || resource != current.Resource) && secret.Kind == SecretKind.Keep && current.HasSecret)
            secret = new SecretChange(SecretKind.Remove, null);
        // A key for the connectors that take one: Bedrock reads a named AWS profile or the identity of the host, a model on
        // this machine takes none; a secret kept for nothing is removed.
        if (!AssistantConnectors.TakesKey(connector))
            secret = current.HasSecret ? new SecretChange(SecretKind.Remove, null) : SecretChange.Keep;
        var hint = secret.Kind switch
        {
            SecretKind.Remove => null,
            SecretKind.Replace => HintOf(secret.Value!),
            _ => current.SecretHint,
        };
        var settings = current with
        {
            Connector = connector, Model = model, Region = region, AwsProfile = profile, MaxOutputTokens = ceiling,
            AllowedClassifications = allowed, SecretHint = hint, Resource = resource, Port = port,
        };
        return new CheckedAssistantSettings(settings, secret);
    }

    /// <summary>The names of the fields that differ between two settings (the details of the audit, never a value of a secret).</summary>
    public static IReadOnlyList<string> ChangedFields(AssistantSettings before, AssistantSettings after, SecretChange secret)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        ArgumentNullException.ThrowIfNull(secret);
        var fields = new List<string>();
        if (before.Connector != after.Connector) fields.Add("connector");
        if (before.Model != after.Model) fields.Add("model");
        if (before.Region != after.Region) fields.Add("region");
        if (before.AwsProfile != after.AwsProfile) fields.Add("aws_profile");
        if (before.Resource != after.Resource) fields.Add("resource");
        if (before.Port != after.Port) fields.Add("port");
        if (before.MaxOutputTokens != after.MaxOutputTokens) fields.Add("max_output_tokens");
        if (!before.AllowedClassifications.SequenceEqual(after.AllowedClassifications)) fields.Add("allowed_classifications");
        if (secret.Kind != SecretKind.Keep) fields.Add("secret");
        return fields;
    }

    private static string? Formed(string? value, Regex pattern, string field) =>
        value is null || pattern.IsMatch(value) ? value : throw DomainException.InvalidValue(field);

    /// <summary>
    /// The hint of a key: its last characters, a quarter of it at most and <see cref="HintLength"/> at most, so that the
    /// hint never says the key nor most of it (four characters from sixteen on, as every key of a provider has).
    /// </summary>
    private static string HintOf(string secret) => secret[^Math.Min(HintLength, secret.Length / 4)..];

    /// <summary>
    /// The secret given: null keeps it, an empty text removes it, a text of printable ASCII characters without blank
    /// replaces it (a key goes in a header of HTTP, and its hint in a column of ASCII), of <see cref="SecretMin"/>
    /// characters at least (VALEUR_INVALIDE below).
    /// </summary>
    private static SecretChange SecretOf(string? secret)
    {
        if (secret is null)
            return SecretChange.Keep;
        if (secret.Length == 0)
            return new SecretChange(SecretKind.Remove, null);
        if (secret.Length > SecretMax)
            throw DomainException.Invalid(ErrorCodes.TextTooLong, "secret", SecretMax);
        if (secret.Length < SecretMin || secret.Any(c => c is <= ' ' or > '~'))
            throw DomainException.InvalidValue("secret");
        return new SecretChange(SecretKind.Replace, secret);
    }
}
