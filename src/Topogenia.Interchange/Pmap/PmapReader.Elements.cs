// PmapReader.Elements.cs
// The elements of a .pmap file: known properties, objects, lists, texts under the rules of every entered text, codes, numbers.

using System.Text.Json;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Values;

namespace Topogenia.Interchange.Pmap;

internal sealed partial class PmapReading
{
    /// <summary>An unknown property is a fault that lists the possible ones: a typing error (`lable`) would be lost otherwise.</summary>
    private void Properties(JsonElement element, string path, string[] known)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (!known.Contains(property.Name, StringComparer.Ordinal))
                Fault(Join(path, property.Name), FileErrorCodes.UnknownProperty, ("name", property.Name),
                    ("possible", string.Join(", ", known.Where(name => name != "$schema"))));
        }
    }

    private static string Join(string path, string name) => path.Length == 0 ? name : $"{path}.{name}";

    /// <summary>The identifier of a lane, a step or a document: 1 to 64 characters, no surrounding space, once in the file.</summary>
    private string? Identifier(JsonElement element, string path, string kind, bool required = true)
    {
        var id = Text(element, "id", path, required);
        if (id is null)
            return null;
        if (id.Length == 0)
        {
            Fault($"{path}.id", FileErrorCodes.EmptyId);
            return null;
        }
        if (id.Length > PmapFormat.IdMax || id.Any(char.IsControl) || id != id.Trim())
        {
            Fault($"{path}.id", FileErrorCodes.InvalidId, ("id", id), ("max", PmapFormat.IdMax));
            return null;
        }
        if (!ids.TryAdd(id, kind))
        {
            Fault($"{path}.id", FileErrorCodes.DuplicateId, ("id", id));
            return null;
        }
        return id;
    }

    private JsonElement? Object(JsonElement parent, string name, string path, bool required)
    {
        if (!parent.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            if (required)
                Fault(Join(path, name), FileErrorCodes.Required, ("name", name));
            return null;
        }
        if (value.ValueKind == JsonValueKind.Object)
            return value;
        Fault(Join(path, name), FileErrorCodes.NotAnObject, ("name", name));
        return null;
    }

    private JsonElement? List(JsonElement parent, string name, string path, bool required)
    {
        if (!parent.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            if (required)
                Fault(Join(path, name), FileErrorCodes.Required, ("name", name));
            return null;
        }
        if (value.ValueKind == JsonValueKind.Array)
            return value;
        Fault(Join(path, name), FileErrorCodes.NotAList, ("name", name));
        return null;
    }

    /// <summary>A text as written, null when absent (a fault when required) or when it is not a text.</summary>
    private string? Text(JsonElement parent, string name, string path, bool required)
    {
        if (!parent.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            if (required)
                Fault(Join(path, name), FileErrorCodes.Required, ("name", name));
            return null;
        }
        if (value.ValueKind == JsonValueKind.String)
            return value.GetString();
        Fault(Join(path, name), FileErrorCodes.NotAText, ("name", name));
        return null;
    }

    /// <summary>An entered text: trimmed, an empty one is absent (a fault when required), then the rules of every entered text.</summary>
    private string? Entered(JsonElement parent, string name, string path, int max, bool required)
    {
        var value = Text(parent, name, path, required)?.Trim();
        if (value is null)
            return null;
        if (value.Length == 0)
        {
            if (required)
                Fault(Join(path, name), FileErrorCodes.EmptyText, ("name", name));
            return null;
        }
        return Checked(value, Join(path, name), name, max);
    }

    /// <summary>
    /// The rules of every entered text (`TextRules`, A-2.18-08): one implementation for the API and the formats, the 1.x
    /// had two copies of them. A refused text is a fault with the code of the rule and the property as its field.
    /// </summary>
    private string? Checked(string? text, string path, string field, int max)
    {
        try
        {
            return TextRules.Optional(text, field, max);
        }
        catch (DomainException refusal)
        {
            Fault(path, refusal);
            return null;
        }
    }

    /// <summary>A code of a closed list, null when absent or out of the list (a fault that lists the possible values).</summary>
    private string? Enumeration(JsonElement parent, string name, string path, IReadOnlyList<string> known)
    {
        var value = Text(parent, name, path, required: false);
        if (value is null || known.Contains(value, StringComparer.Ordinal))
            return value;
        Fault(Join(path, name), FileErrorCodes.InvalidValue, ("value", value), ("possible", string.Join(", ", known)));
        return null;
    }

    /// <summary>A number, null when absent or not a number (a fault); its bounds are those of the rule that reads it.</summary>
    private decimal? Number(JsonElement parent, string name, string path)
    {
        if (!parent.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
            return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number))
            return number;
        Fault(Join(path, name), FileErrorCodes.NotANumber, ("name", name));
        return null;
    }

    /// <summary>A whole number (a headcount), null when absent or not a whole number (a fault); its bounds are those of the domain.</summary>
    private int? Whole(JsonElement parent, string name, string path)
    {
        if (!parent.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
            return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
            return number;
        Fault(Join(path, name), FileErrorCodes.NotANumber, ("name", name));
        return null;
    }

    /// <summary>A boolean, false when absent; another value is a fault.</summary>
    private bool Flag(JsonElement parent, string name, string path)
    {
        if (!parent.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
            return false;
        if (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
            return value.GetBoolean();
        Fault(Join(path, name), FileErrorCodes.InvalidValue, ("value", value.ToString()), ("possible", "true, false"));
        return false;
    }

    /// <summary>
    /// The texts of an object in the other configured languages, `{language: text}`, each under the rules of every
    /// entered text and the length of the original; an empty text is left out.
    /// </summary>
    private Dictionary<string, string> Translations(JsonElement parent, string path, int max)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (Object(parent, "translations", path, required: false) is not { } translations)
            return result;
        foreach (var property in translations.EnumerateObject())
        {
            var place = $"{Join(path, "translations")}.{property.Name}";
            if (!settings.Languages.IsSupported(property.Name))
            {
                Fault(place, FileErrorCodes.UnknownLanguage, ("language", property.Name), ("possible", string.Join(", ", settings.Languages.Languages)));
                continue;
            }
            if (property.Value.ValueKind != JsonValueKind.String)
            {
                Fault(place, FileErrorCodes.TranslationNotText);
                continue;
            }
            if (Checked(property.Value.GetString(), place, "translations", max) is { } text)
                result[property.Name] = text;
        }
        return result;
    }
}
