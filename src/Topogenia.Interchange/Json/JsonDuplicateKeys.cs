// JsonDuplicateKeys.cs
// Where a key is written a second time in one object of a JSON text: the parser refuses such a text without saying where.

using System.Text;
using System.Text.Json;

namespace Topogenia.Interchange.Json;

/// <summary>
/// The parser checks the keys written twice once the text is parsed, and its fault carries no line: the text is read
/// again here, object by object, to give the place of the second key to the person who corrects the file.
/// </summary>
public static class JsonDuplicateKeys
{
    /// <summary>The line and the column (from 1, the column in bytes as the parser counts it) of the first key written twice in one object; null when none is, or when the text does not read.</summary>
    public static (long Line, long Column)? Place(string text, JsonReaderOptions options)
    {
        ArgumentNullException.ThrowIfNull(text);
        var bytes = Encoding.UTF8.GetBytes(text);
        var reader = new Utf8JsonReader(bytes, options);
        var names = new Stack<HashSet<string>?>();
        try
        {
            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.StartObject:
                        names.Push(new HashSet<string>(StringComparer.Ordinal));
                        break;
                    case JsonTokenType.StartArray:
                        names.Push(null);
                        break;
                    case JsonTokenType.EndObject or JsonTokenType.EndArray:
                        names.Pop();
                        break;
                    case JsonTokenType.PropertyName when names.Peek() is { } seen && !seen.Add(reader.GetString()!):
                        return PlaceOf(bytes, reader.TokenStartIndex);
                }
            }
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            return null;
        }
        return null;
    }

    private static (long Line, long Column) PlaceOf(byte[] bytes, long index)
    {
        var before = bytes.AsSpan(0, (int)index);
        return (before.Count((byte)'\n') + 1, index - before.LastIndexOf((byte)'\n'));
    }
}
