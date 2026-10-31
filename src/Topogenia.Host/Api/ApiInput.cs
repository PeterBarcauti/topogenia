// ApiInput.cs
// What the API reads besides the body: the If-Match header, the required fields, the codes of the query strings (annex A 1, A 2.16, A 2.18).

using System.Globalization;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Structure;
using Topogenia.Domain.Values;
using Topogenia.Host.Json;

namespace Topogenia.Host.Api;

/// <summary>
/// The readings of a request that the domain cannot make, since it receives typed values: the version of `If-Match`
/// (quotes tolerated, A-2.16-02; unreadable, VERSION_INVALIDE, A-2.16-03), a field absent from the body that the domain
/// needs as an identifier or a number (CHAMP_OBLIGATOIRE, never 0 nor an empty identifier by default, A-2.18-07), and the
/// code of a closed list in a query string (VALEUR_INVALIDE on its name).
/// </summary>
public static class ApiInput
{
    /// <summary>The version read by the client, or null without `If-Match` (the Server mode requires it, P4-T06).</summary>
    public static int? Version(string? ifMatch)
    {
        if (string.IsNullOrWhiteSpace(ifMatch))
            return null;
        var value = ifMatch.Trim();
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
            value = value[1..^1];
        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var version)
            ? version
            : throw DomainException.Invalid(ErrorCodes.InvalidVersion);
    }

    /// <summary>The value of a field the domain cannot do without, or CHAMP_OBLIGATOIRE on its name.</summary>
    public static T Required<T>(T? value, string field)
        where T : struct =>
        value ?? throw DomainException.Invalid(ErrorCodes.RequiredField, field);

    /// <summary>A required list, or CHAMP_OBLIGATOIRE on its name.</summary>
    public static IReadOnlyList<T> Required<T>(IReadOnlyList<T>? value, string field) =>
        value ?? throw DomainException.Invalid(ErrorCodes.RequiredField, field);

    /// <summary>The code of a closed list given in a query string or a body, or VALEUR_INVALIDE on its name; null when absent.</summary>
    public static T? Code<T>(string? code, string field)
        where T : struct, Enum =>
        string.IsNullOrEmpty(code) ? null : Codes.Parse<T>(code, field);

    /// <summary>The change of a field of a partial update: null when the field is absent from the body.</summary>
    public static FieldChange<T>? Change<T>(Optional<T> field) => field.IsPresent ? new FieldChange<T>(field.Value) : null;
}
