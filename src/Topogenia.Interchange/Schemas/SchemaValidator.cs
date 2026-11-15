// SchemaValidator.cs
// Validation of a JSON document against the subset of JSON Schema 2020-12 that the published schemas of the formats use.

using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Topogenia.Interchange.Schemas;

/// <summary>A place of a document that breaks its schema: the JSON pointer of the value and the keyword it breaks.</summary>
public sealed record SchemaError(string Path, string Keyword);

/// <summary>A schema that uses a keyword this validator does not know, or a reference it cannot resolve.</summary>
public sealed class SchemaDefinitionException(string message) : Exception(message);

/// <summary>
/// A validator of the subset of JSON Schema 2020-12 written by the schemas of this solution (ADR 051): <c>type</c>,
/// <c>properties</c>, <c>required</c>, <c>additionalProperties</c>, <c>items</c>, <c>minItems</c>, <c>maxItems</c>,
/// <c>enum</c>, <c>const</c>, <c>minLength</c>, <c>maxLength</c> (in Unicode code points, as the standard counts),
/// <c>pattern</c> (searched, not anchored, as the standard says), <c>minimum</c>, <c>maximum</c>, <c>allOf</c>,
/// <c>not</c>, and <c>$ref</c> to a definition of <c>$defs</c>; the annotations <c>$schema</c>, <c>$id</c>,
/// <c>$comment</c>, <c>title</c> and <c>description</c> are read and ignored. Any other keyword is refused when the schema
/// is loaded, so that a schema never claims a check that nobody makes.
/// </summary>
public sealed class SchemaValidator
{
    private static readonly HashSet<string> Annotations = ["$schema", "$id", "$comment", "title", "description", "$defs"];

    private static readonly HashSet<string> Keywords =
    [
        "type", "properties", "required", "additionalProperties", "items", "minItems", "maxItems", "enum", "const",
        "minLength", "maxLength", "pattern", "minimum", "maximum", "allOf", "not", "$ref",
    ];

    /// <summary>The validation stops after this many errors: the report of a hostile document stays small.</summary>
    public const int MaxErrors = 100;

    private readonly JsonElement root;
    private readonly Dictionary<string, Regex> patterns = new(StringComparer.Ordinal);

    public SchemaValidator(JsonElement schema)
    {
        root = schema.Clone();
        Check(root, "#");
    }

    /// <summary>The errors of a document against the definition <c>#/$defs/{definition}</c>, or against the root when null.</summary>
    public IReadOnlyList<SchemaError> Validate(JsonElement document, string? definition = null)
    {
        var errors = new List<SchemaError>();
        var schema = definition is null ? root : Resolve("#/$defs/" + definition);
        Validate(schema, document, "", errors);
        return errors;
    }

    private void Check(JsonElement schema, string place)
    {
        if (schema.ValueKind is not JsonValueKind.Object and not JsonValueKind.True and not JsonValueKind.False)
            throw new SchemaDefinitionException($"{place} is not a schema.");
        if (schema.ValueKind != JsonValueKind.Object)
            return;
        foreach (var property in schema.EnumerateObject())
        {
            if (!Annotations.Contains(property.Name) && !Keywords.Contains(property.Name))
                throw new SchemaDefinitionException($"{place} uses the keyword {property.Name}, which the validator does not know.");
            switch (property.Name)
            {
                case "$defs" or "properties":
                    foreach (var child in property.Value.EnumerateObject())
                        Check(child.Value, place + "/" + property.Name + "/" + child.Name);
                    break;
                case "items" or "additionalProperties" or "not":
                    Check(property.Value, place + "/" + property.Name);
                    break;
                case "allOf":
                    var index = 0;
                    foreach (var child in property.Value.EnumerateArray())
                        Check(child, place + "/allOf/" + index++);
                    break;
                case "$ref":
                    Resolve(property.Value.GetString() ?? "");
                    break;
                case "pattern":
                    Pattern(property.Value.GetString() ?? "");
                    break;
            }
        }
    }

    private JsonElement Resolve(string reference)
    {
        const string prefix = "#/$defs/";
        if (reference.StartsWith(prefix, StringComparison.Ordinal)
            && root.TryGetProperty("$defs", out var definitions)
            && definitions.TryGetProperty(reference[prefix.Length..], out var target))
            return target;
        throw new SchemaDefinitionException($"The reference {reference} names no definition of $defs.");
    }

    private Regex Pattern(string pattern)
    {
        if (!patterns.TryGetValue(pattern, out var regex))
            patterns[pattern] = regex = new Regex(pattern, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        return regex;
    }

    private void Validate(JsonElement schema, JsonElement value, string path, List<SchemaError> errors)
    {
        if (errors.Count >= MaxErrors)
            return;
        if (schema.ValueKind == JsonValueKind.False)
        {
            errors.Add(new SchemaError(path, "false"));
            return;
        }
        if (schema.ValueKind == JsonValueKind.True)
            return;
        foreach (var keyword in schema.EnumerateObject())
        {
            if (!Holds(schema, keyword, value, path, errors))
                errors.Add(new SchemaError(path, keyword.Name));
        }
    }

    /// <summary>
    /// Whether the value holds one keyword. The keywords that contain schemas report the errors of their parts and hold;
    /// the others hold or not, and the caller writes their error.
    /// </summary>
    private bool Holds(JsonElement schema, JsonProperty keyword, JsonElement value, string path, List<SchemaError> errors)
    {
        var rule = keyword.Value;
        switch (keyword.Name)
        {
            case "type":
                return rule.ValueKind == JsonValueKind.Array ? rule.EnumerateArray().Any(type => IsOfType(value, type.GetString())) : IsOfType(value, rule.GetString());
            case "enum":
                return rule.EnumerateArray().Any(candidate => JsonElement.DeepEquals(candidate, value));
            case "const":
                return JsonElement.DeepEquals(rule, value);
            case "minLength":
                return value.ValueKind != JsonValueKind.String || CodePoints(value.GetString()!) >= rule.GetInt32();
            case "maxLength":
                return value.ValueKind != JsonValueKind.String || CodePoints(value.GetString()!) <= rule.GetInt32();
            case "pattern":
                return value.ValueKind != JsonValueKind.String || Pattern(rule.GetString()!).IsMatch(value.GetString()!);
            case "minimum":
                return value.ValueKind != JsonValueKind.Number || Compare(value, rule.GetDecimal()) >= 0;
            case "maximum":
                return value.ValueKind != JsonValueKind.Number || Compare(value, rule.GetDecimal()) <= 0;
            case "minItems":
                return value.ValueKind != JsonValueKind.Array || value.GetArrayLength() >= rule.GetInt32();
            case "maxItems":
                return value.ValueKind != JsonValueKind.Array || value.GetArrayLength() <= rule.GetInt32();
            case "not":
                var inner = new List<SchemaError>();
                Validate(rule, value, path, inner);
                return inner.Count > 0;
            default:
                Apply(schema, keyword, value, path, errors);
                return true;
        }
    }

    /// <summary>The keywords that hold schemas: their errors are those of the parts they check.</summary>
    private void Apply(JsonElement schema, JsonProperty keyword, JsonElement value, string path, List<SchemaError> errors)
    {
        switch (keyword.Name)
        {
            case "$ref":
                Validate(Resolve(keyword.Value.GetString()!), value, path, errors);
                break;
            case "allOf":
                foreach (var part in keyword.Value.EnumerateArray())
                    Validate(part, value, path, errors);
                break;
            case "items" when value.ValueKind == JsonValueKind.Array:
                var index = 0;
                foreach (var item in value.EnumerateArray())
                    Validate(keyword.Value, item, path + "/" + index++.ToString(CultureInfo.InvariantCulture), errors);
                break;
            case "properties" when value.ValueKind == JsonValueKind.Object:
                foreach (var property in value.EnumerateObject())
                {
                    if (keyword.Value.TryGetProperty(property.Name, out var child))
                        Validate(child, property.Value, Child(path, property.Name), errors);
                }
                break;
            case "additionalProperties" when value.ValueKind == JsonValueKind.Object:
                foreach (var property in value.EnumerateObject())
                {
                    var declared = schema.TryGetProperty("properties", out var properties) && properties.TryGetProperty(property.Name, out _);
                    if (declared)
                        continue;
                    if (keyword.Value.ValueKind == JsonValueKind.False)
                        errors.Add(new SchemaError(Child(path, property.Name), keyword.Name));
                    else
                        Validate(keyword.Value, property.Value, Child(path, property.Name), errors);
                }
                break;
            case "required" when value.ValueKind == JsonValueKind.Object:
                foreach (var name in keyword.Value.EnumerateArray().Select(name => name.GetString()!).Where(name => !value.TryGetProperty(name, out _)))
                    errors.Add(new SchemaError(Child(path, name), keyword.Name));
                break;
        }
    }

    private static bool IsOfType(JsonElement value, string? type) => type switch
    {
        "object" => value.ValueKind == JsonValueKind.Object,
        "array" => value.ValueKind == JsonValueKind.Array,
        "string" => value.ValueKind == JsonValueKind.String,
        "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
        "null" => value.ValueKind == JsonValueKind.Null,
        "number" => value.ValueKind == JsonValueKind.Number,
        "integer" => value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number) && decimal.Truncate(number) == number,
        _ => throw new SchemaDefinitionException($"The type {type} is not a type of JSON Schema."),
    };

    /// <summary>
    /// The order of a number and a bound: a number beyond the range of <see cref="decimal"/> (<c>1e30</c>) is above every
    /// bound, or below every bound when it is negative, instead of failing to read.
    /// </summary>
    private static int Compare(JsonElement value, decimal bound) =>
        value.TryGetDecimal(out var number) ? number.CompareTo(bound) : value.GetRawText().StartsWith('-') ? -1 : 1;

    private static int CodePoints(string text)
    {
        var count = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                i++;
            count++;
        }
        return count;
    }

    /// <summary>A JSON pointer to a property (RFC 6901: <c>~</c> and <c>/</c> escaped).</summary>
    private static string Child(string path, string name) => path + "/" + name.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);
}
