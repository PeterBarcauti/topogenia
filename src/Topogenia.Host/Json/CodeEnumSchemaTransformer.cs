// CodeEnumSchemaTransformer.cs
// Describes an enumeration in OpenAPI as the text of its codes, the JSON that CodeEnumConverterFactory writes and reads (D09).

using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Topogenia.Domain.Values;

namespace Topogenia.Host.Json;

/// <summary>
/// An enumeration goes through <see cref="CodeEnumConverterFactory"/>, which the generator of the contract cannot read:
/// without this transformer its schema is empty and the client types it `unknown`. It is a string among the codes of
/// the enumeration, in their order (<see cref="Codes.All{T}"/>), so that the interface types each closed list (D09).
/// The generator does not visit the members of a nullable structure (the lead time of a sheet, the volume of a start,
/// ADR 085): the enumerations among them are described with the structure.
/// </summary>
public sealed class CodeEnumSchemaTransformer : IOpenApiSchemaTransformer
{
    private static readonly MethodInfo AllCodes = typeof(Codes).GetMethod(nameof(Codes.All))!;

    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(context);
        var type = context.JsonTypeInfo.Type;
        var underlying = Nullable.GetUnderlyingType(type);
        if ((underlying ?? type).IsEnum)
            Describe(schema, type);
        else if (underlying is not null)
            DescribeMembers(schema, context.JsonTypeInfo.Options.GetTypeInfo(underlying));
        return Task.CompletedTask;
    }

    /// <summary>The schema of an enumeration, nullable or not: a string among its codes.</summary>
    private static void Describe(OpenApiSchema schema, Type type)
    {
        var enumeration = Nullable.GetUnderlyingType(type) ?? type;
        var codes = (IReadOnlyList<string>)AllCodes.MakeGenericMethod(enumeration).Invoke(null, null)!;
        schema.Type = enumeration == type ? JsonSchemaType.String : JsonSchemaType.String | JsonSchemaType.Null;
        schema.Enum = [.. codes.Select(code => (JsonNode)JsonValue.Create(code))];
    }

    /// <summary>The enumerations among the members of a nullable structure, which the generator leaves empty.</summary>
    private static void DescribeMembers(OpenApiSchema schema, JsonTypeInfo structure)
    {
        foreach (var member in structure.Properties)
        {
            if ((Nullable.GetUnderlyingType(member.PropertyType) ?? member.PropertyType).IsEnum
                && schema.Properties?.TryGetValue(member.Name, out var property) == true && property is OpenApiSchema inline)
                Describe(inline, member.PropertyType);
        }
    }
}
