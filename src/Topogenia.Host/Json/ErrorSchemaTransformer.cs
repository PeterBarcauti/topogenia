// ErrorSchemaTransformer.cs
// Describes in OpenAPI the body of every error of the API, its code one of those that have a message, so that the client types its errors (D09, spec 3.6).

using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Topogenia.Host.Errors;

namespace Topogenia.Host.Json;

/// <summary>
/// Adds the schema <c>ErrorBody</c> (<see cref="Errors.ErrorBody"/>: <c>{code, message, details}</c>) to the components
/// of the document: no route declares its refusals, yet the client generated from the contract (D09) reads each of them
/// with this type. Its code is one of the codes of the catalogue of the messages, which holds every code a refusal names
/// (spec 3.6, the messages tests).
/// </summary>
public sealed class ErrorSchemaTransformer(ErrorMessages messages) : IOpenApiDocumentTransformer
{
    public const string SchemaName = "ErrorBody";

    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        var codes = messages.Of(ErrorMessages.FallbackLanguage).Keys.Order(StringComparer.Ordinal).Select(code => (JsonNode)JsonValue.Create(code));
        document.Components ??= new OpenApiComponents();
        document.Components.Schemas ??= new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal);
        document.Components.Schemas[SchemaName] = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Description = "The body of every error of the API: the stable code, the message in the language of the user, the values.",
            Required = new HashSet<string>(["code", "message", "details"], StringComparer.Ordinal),
            Properties = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal)
            {
                ["code"] = new OpenApiSchema { Type = JsonSchemaType.String, Enum = [.. codes] },
                ["message"] = new OpenApiSchema { Type = JsonSchemaType.String },
                ["details"] = new OpenApiSchema { Type = JsonSchemaType.Object, AdditionalProperties = new OpenApiSchema() },
            },
        };
        return Task.CompletedTask;
    }
}
