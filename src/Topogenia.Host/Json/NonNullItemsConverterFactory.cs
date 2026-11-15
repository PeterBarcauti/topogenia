// NonNullItemsConverterFactory.cs
// Refuses a null item in a list of a body of the API (`[null]`): an invalid request, never a failure of the handler (A-2.18-06).

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Topogenia.Host.Json;

/// <summary>
/// Reads the lists of the bodies of the API (`List&lt;T&gt;`, `IReadOnlyList&lt;T&gt;` of a body record of `Api/`) item by
/// item and refuses a null item: the handlers read every item, so `[null]` is a body of the wrong shape
/// (REQUETE_INVALIDE), as a number written as a text is. A null list stays null: the handler says whether it is required.
/// </summary>
public sealed class NonNullItemsConverterFactory : JsonConverterFactory
{
    private static readonly string BodiesNamespace = $"{typeof(NonNullItemsConverterFactory).Namespace![..^".Json".Length]}.Api";

    public override bool CanConvert(Type typeToConvert)
    {
        ArgumentNullException.ThrowIfNull(typeToConvert);
        if (!typeToConvert.IsGenericType)
            return false;
        var definition = typeToConvert.GetGenericTypeDefinition();
        var item = typeToConvert.GetGenericArguments()[0];
        return (definition == typeof(List<>) || definition == typeof(IReadOnlyList<>))
            && item.IsClass && item.Namespace is { } name && name.StartsWith(BodiesNamespace, StringComparison.Ordinal);
    }

    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(typeToConvert);
        var item = typeToConvert.GetGenericArguments()[0];
        var converter = typeToConvert.GetGenericTypeDefinition() == typeof(List<>)
            ? typeof(ListConverter<>).MakeGenericType(item)
            : typeof(ReadOnlyListConverter<>).MakeGenericType(item);
        return (JsonConverter?)Activator.CreateInstance(converter);
    }

    private static List<T> ReadItems<T>(ref Utf8JsonReader reader, JsonSerializerOptions options)
        where T : class
    {
        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException("A list is expected.");
        var items = new List<T>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            items.Add(JsonSerializer.Deserialize<T>(ref reader, options) ?? throw new JsonException("A list holds a null item."));
        return items;
    }

    private static void WriteItems<T>(Utf8JsonWriter writer, IEnumerable<T> items, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var item in items)
            JsonSerializer.Serialize(writer, item, options);
        writer.WriteEndArray();
    }

    public sealed class ListConverter<T> : JsonConverter<List<T>>
        where T : class
    {
        public override List<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => ReadItems<T>(ref reader, options);

        public override void Write(Utf8JsonWriter writer, List<T> value, JsonSerializerOptions options) => WriteItems(writer, value, options);
    }

    public sealed class ReadOnlyListConverter<T> : JsonConverter<IReadOnlyList<T>>
        where T : class
    {
        public override IReadOnlyList<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            ReadItems<T>(ref reader, options);

        public override void Write(Utf8JsonWriter writer, IReadOnlyList<T> value, JsonSerializerOptions options) => WriteItems(writer, value, options);
    }
}
