// CodeEnumConverterFactory.cs
// Writes and reads every enumeration as its stable code (`task`, `month_end_close`, `A`), the same code as the database and the formats.

using System.Text.Json;
using System.Text.Json.Serialization;
using Topogenia.Domain.Values;

namespace Topogenia.Host.Json;

/// <summary>
/// Converts the enumerations through <see cref="Codes"/>, so that the API writes the codes of the closed lists of the
/// model and never their numbers (the 1.x wrote the same codes, annex A 2.3 to 2.13). A code that is not one of the list
/// is an invalid body (REQUETE_INVALIDE); the bodies of the API carry the codes as text, which the domain checks itself.
/// </summary>
public sealed class CodeEnumConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        ArgumentNullException.ThrowIfNull(typeToConvert);
        return typeToConvert.IsEnum;
    }

    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        (JsonConverter?)Activator.CreateInstance(typeof(CodeEnumConverter<>).MakeGenericType(typeToConvert));

    public sealed class CodeEnumConverter<T> : JsonConverter<T>
        where T : struct, Enum
    {
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            reader.TokenType == JsonTokenType.String && Codes.TryParse<T>(reader.GetString(), out var value)
                ? value
                : throw new JsonException($"Not a code of {typeof(T).Name}.");

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            ArgumentNullException.ThrowIfNull(writer);
            writer.WriteStringValue(Codes.Of(value));
        }
    }
}
