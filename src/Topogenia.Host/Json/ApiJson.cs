// ApiJson.cs
// The JSON of the API: snake_case names, closed lists written as their stable codes, optional fields of a partial update (spec 3.6, annex A 1).

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Topogenia.Host.Json;

/// <summary>
/// The settings shared by the bodies read and the replies written (A-1.1-01): property names in snake_case as in the
/// 1.x, keys of dictionaries kept as they are (languages, details of an error), enumerations written as the codes of
/// <c>Codes</c> (`task`, `needs_review`, `facilitator`), <see cref="Optional{T}"/> for the fields of a partial update, and
/// lists of the bodies without null items (<see cref="NonNullItemsConverterFactory"/>). The members of a structure (the
/// work time of a project, the lead time of a sheet, ADR 085) are required, as its constructor requires them.
/// </summary>
public static class ApiJson
{
    public static void Configure(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
        options.DictionaryKeyPolicy = null;
        options.NumberHandling = JsonNumberHandling.Strict;
        options.Converters.Add(new CodeEnumConverterFactory());
        options.Converters.Add(new OptionalConverterFactory());
        options.Converters.Add(new NonNullItemsConverterFactory());
        options.TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { RequireMembersOfStructures } };
    }

    /// <summary>
    /// A structure is read through its default constructor, which leaves each member at its default, and the generator
    /// of the contract describes it as optional: each settable member is required, in the body as in the contract.
    /// </summary>
    private static void RequireMembersOfStructures(JsonTypeInfo info)
    {
        if (info.Kind != JsonTypeInfoKind.Object || !info.Type.IsValueType)
            return;
        foreach (var property in info.Properties.Where(property => property.Set is not null))
            property.IsRequired = true;
    }
}
