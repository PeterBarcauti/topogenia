// JsonText.cs
// Whether every text of a received JSON document reads as UTF-16: a half of a surrogate pair is valid JSON that no text holds.

using System.Text.Json;

namespace Topogenia.Interchange.Json;

/// <summary>
/// A received file may escape half of a surrogate pair (<c>"\ud800"</c>): the parser accepts it, the reading of the text
/// does not (<see cref="InvalidOperationException"/>), and neither database would keep it as written. Every name and
/// every string of a received document is read once here, right after the parsing, so that such a file is refused as
/// one that is not JSON instead of failing where a later rule reads the text.
/// </summary>
public static class JsonText
{
    /// <summary>True when each property name and each string of the element, at every depth, reads as a text.</summary>
    public static bool Reads(JsonElement element)
    {
        try
        {
            ReadAll(element);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static void ReadAll(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                _ = element.GetString();
                break;
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    _ = property.Name;
                    ReadAll(property.Value);
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                    ReadAll(item);
                break;
        }
    }
}
