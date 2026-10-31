// AssistantSettings.cs
// The settings of the assistant of the installation: the connector chosen in the list, its model, region and profile, the ceiling of a reply, the classifications that may be sent, the hint of the secret (spec 3.0 section 4).

using System.Text.RegularExpressions;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Values;

namespace Topogenia.Application.Assistant;

/// <summary>
/// The connectors of the list of the settings (spec 3.0 section 4, decision of the owner of the product of 29/10/2026):
/// none by default, the copy and paste of the prompt, Claude by the API of Anthropic, Claude by AWS Bedrock. The list
/// grows without changing the dialog.
/// </summary>
public enum AssistantConnector { None, CopyPaste, Anthropic, Bedrock }

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
    string? Secret);

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

    public const int MinOutputTokens = 256;

    /// <summary>The output ceiling of the current models (128 000 tokens).</summary>
    public const int MaxOutputTokens = 128000;

    /// <summary>The characters of the last part of a secret shown to the administrator.</summary>
    public const int HintLength = 4;

    [GeneratedRegex("^[A-Za-z0-9._:/-]+$")]
    private static partial Regex ModelPattern();

    /// <summary>A region of AWS: `eu-west-3`, `us-east-1`, `us-gov-west-1`.</summary>
    [GeneratedRegex("^[a-z]{2}(-[a-z]+)+-[0-9]$")]
    private static partial Regex RegionPattern();

    [GeneratedRegex("^[A-Za-z0-9._-]+$")]
    private static partial Regex ProfilePattern();

    /// <summary>
    /// The settings that the change gives, from the current ones: VALEUR_INVALIDE on a connector, a model, a region, a
    /// profile, a ceiling or a classification outside its list or its form; CHAMP_OBLIGATOIRE for the region of Bedrock
    /// and its model; TEXTE_TROP_LONG past the length of a field. The fields a connector does not use are cleared.
    /// </summary>
    public static CheckedAssistantSettings Check(AssistantSettings current, AssistantSettingsChange change)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(change);
        var connector = Codes.Parse<AssistantConnector>(change.Connector, "connector");
        var model = Formed(TextRules.Optional(change.Model, "model", ModelMax), ModelPattern(), "model");
        var region = Formed(TextRules.Optional(change.Region, "region", 40), RegionPattern(), "region");
        var profile = Formed(TextRules.Optional(change.AwsProfile, "aws_profile", ProfileMax), ProfilePattern(), "aws_profile");
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
            _ => ((string?)null, (string?)null, (string?)null),
        };
        // Only the API of Anthropic takes a secret: Bedrock reads a named AWS profile or the identity of the host, and no
        // other connector calls a model; a secret kept for nothing is removed.
        if (connector is not AssistantConnector.Anthropic)
            secret = current.HasSecret ? new SecretChange(SecretKind.Remove, null) : SecretChange.Keep;
        var hint = secret.Kind switch
        {
            SecretKind.Remove => null,
            SecretKind.Replace => secret.Value![^Math.Min(HintLength, secret.Value!.Length)..],
            _ => current.SecretHint,
        };
        var settings = current with
        {
            Connector = connector, Model = model, Region = region, AwsProfile = profile, MaxOutputTokens = ceiling,
            AllowedClassifications = allowed, SecretHint = hint,
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
        if (before.MaxOutputTokens != after.MaxOutputTokens) fields.Add("max_output_tokens");
        if (!before.AllowedClassifications.SequenceEqual(after.AllowedClassifications)) fields.Add("allowed_classifications");
        if (secret.Kind != SecretKind.Keep) fields.Add("secret");
        return fields;
    }

    private static string? Formed(string? value, Regex pattern, string field) =>
        value is null || pattern.IsMatch(value) ? value : throw DomainException.InvalidValue(field);

    /// <summary>
    /// The secret given: null keeps it, an empty text removes it, a text of printable ASCII characters without blank
    /// replaces it (a key goes in a header of HTTP, and its hint in a column of ASCII).
    /// </summary>
    private static SecretChange SecretOf(string? secret)
    {
        if (secret is null)
            return SecretChange.Keep;
        if (secret.Length == 0)
            return new SecretChange(SecretKind.Remove, null);
        if (secret.Length > SecretMax)
            throw DomainException.Invalid(ErrorCodes.TextTooLong, "secret", SecretMax);
        if (secret.Any(c => c is <= ' ' or > '~'))
            throw DomainException.InvalidValue("secret");
        return new SecretChange(SecretKind.Replace, secret);
    }
}
