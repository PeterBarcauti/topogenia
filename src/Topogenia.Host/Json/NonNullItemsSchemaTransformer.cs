// NonNullItemsSchemaTransformer.cs
// Describes a list of a body of the API in OpenAPI as an array of its item, the JSON that NonNullItemsConverterFactory reads (D09).

using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Topogenia.Host.Json;

/// <summary>
/// A list of a body record of `Api/` goes through <see cref="NonNullItemsConverterFactory"/> (a null item refused), which
/// the generator of the contract cannot read: without this transformer its schema is empty and the client types it
/// `unknown`. It is an array of the schema of its item, so that the interface types the members, the cells, the rows.
/// </summary>
public sealed class NonNullItemsSchemaTransformer : IOpenApiSchemaTransformer
{
    private static readonly NonNullItemsConverterFactory Lists = new();

    public async Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(context);
        var type = context.JsonTypeInfo.Type;
        if (!Lists.CanConvert(type))
            return;
        schema.Type = JsonSchemaType.Array;
        schema.Items = await context.GetOrCreateSchemaAsync(type.GetGenericArguments()[0], null, cancellationToken);
    }
}
