// Optional.cs
// A field of a partial update (PATCH): absent from the body, or present with a value that may be null (annex A 1, PATCH routes).

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Topogenia.Host.Json;

/// <summary>
/// A field of a PATCH body: absent (nothing changes), or present with its value, null included (the value is removed).
/// The 1.x told both apart by reading the JSON by hand; here the converter is called only for a field present in the
/// body, so an absent field keeps the default, not present.
/// </summary>
public readonly struct Optional<T> : IEquatable<Optional<T>>
{
    public Optional(T value)
    {
        Value = value;
        IsPresent = true;
    }

    public T Value { get; }

    public bool IsPresent { get; }

    public bool Equals(Optional<T> other) => IsPresent == other.IsPresent && EqualityComparer<T>.Default.Equals(Value, other.Value);

    public override bool Equals(object? obj) => obj is Optional<T> other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(IsPresent, Value);

    public static bool operator ==(Optional<T> left, Optional<T> right) => left.Equals(right);

    public static bool operator !=(Optional<T> left, Optional<T> right) => !left.Equals(right);
}

/// <summary>Reads an <see cref="Optional{T}"/> present in the body, null included, and writes its value.</summary>
public sealed class OptionalConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        ArgumentNullException.ThrowIfNull(typeToConvert);
        return typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(Optional<>);
    }

    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(typeToConvert);
        return (JsonConverter?)Activator.CreateInstance(typeof(OptionalConverter<>).MakeGenericType(typeToConvert.GetGenericArguments()[0]));
    }

    public sealed class OptionalConverter<T> : JsonConverter<Optional<T>>
    {
        public override bool HandleNull => true;

        public override Optional<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            new(JsonSerializer.Deserialize<T>(ref reader, options)!);

        public override void Write(Utf8JsonWriter writer, Optional<T> value, JsonSerializerOptions options) =>
            JsonSerializer.Serialize(writer, value.IsPresent ? value.Value : default, options);
    }
}
