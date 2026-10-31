// OptionalSchemaTransformer.cs
// Describes an optional field of a partial update in OpenAPI as the value it carries, not as the wrapper of the host (D09).

using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Topogenia.Host.Json;

/// <summary>
/// The JSON of an <see cref="Optional{T}"/> is its value (<see cref="OptionalConverterFactory"/>): the contract generated
/// for the client (D09) gives the schema of that value, nullable, so that a field of a PATCH body reads as an ordinary field;
/// an object value (the volume of a start, ADR 085) keeps its members, so that the client types them.
/// </summary>
public sealed class OptionalSchemaTransformer : IOpenApiSchemaTransformer
{
    public async Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(context);
        var type = context.JsonTypeInfo.Type;
        if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(Optional<>))
            return;
        var value = await context.GetOrCreateSchemaAsync(type.GetGenericArguments()[0], null, cancellationToken);
        schema.Properties?.Clear();
        schema.Required?.Clear();
        schema.Type = (value.Type ?? JsonSchemaType.String) | JsonSchemaType.Null;
        schema.Format = value.Format;
        schema.Pattern = value.Pattern;
        schema.Enum = value.Enum;
        if (value.Properties is not { Count: > 0 } members)
            return;
        schema.Properties = new Dictionary<string, IOpenApiSchema>(members, StringComparer.Ordinal);
        schema.Required = value.Required is null ? null : new HashSet<string>(value.Required, StringComparer.Ordinal);
    }
}
