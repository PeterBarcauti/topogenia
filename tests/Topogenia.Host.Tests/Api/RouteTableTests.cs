// RouteTableTests.cs
// The routes of the host are exactly those of the table 1.x -> 2.0, and the OpenAPI document of the host describes each (spec 3.6, D09).

using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Api;

public sealed partial class RouteTableTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    /// <summary>
    /// The table of `docs/couverture.md` (routes 1.x -> 2.0), the `.pmap` (P5-T04) and the package (P5-T01b, P5-T02)
    /// included; plus the routes the host names (ADR 043, ADR 065, gate P6: classification and sample project).
    /// </summary>
    internal static readonly string[] ExpectedRoutes =
    [
        "GET /api/me", "PATCH /api/me", "PUT /api/me/last-project",
        "GET /api/projects", "POST /api/projects", "GET /api/projects/{id}", "DELETE /api/projects/{id}",
        "PUT /api/projects/{id}/members", "GET /api/projects/{id}/participants", "PUT /api/projects/{id}/sharing-group-label",
        "PUT /api/projects/{id}/classification", "PUT /api/projects/{id}/translation-glossary", "PUT /api/projects/{id}/work-time",
        "POST /api/projects/{id}/export",
        "POST /api/projects/import/preview", "POST /api/projects/import", "POST /api/projects/{id}/import/preview", "POST /api/projects/{id}/import",
        "GET /api/projects/{id}/pmap", "POST /api/projects/import-pmap", "POST /api/projects/{id}/duplicate",
        "GET /api/projects/{id}/translations", "PUT /api/projects/{id}/translations",
        "GET /api/trash", "POST /api/trash/{id}/restore", "DELETE /api/trash/{id}",
        "GET /api/maps/{id}", "DELETE /api/maps/{id}", "GET /api/maps/{id}/history", "POST /api/maps/{id}/undo", "POST /api/maps/{id}/redo",
        "POST /api/maps/{id}/lanes", "PUT /api/maps/{id}/lane-order", "POST /api/maps/{id}/steps", "PUT /api/maps/{id}/layout",
        "POST /api/maps/{id}/move", "POST /api/maps/{id}/flows", "POST /api/maps/{id}/command", "POST /api/maps/{id}/deletion",
        "POST /api/maps/{id}/reattach", "POST /api/maps/{id}/paste", "POST /api/maps/{id}/transfer", "POST /api/maps/{id}/group",
        "PATCH /api/lanes/{id}", "DELETE /api/lanes/{id}",
        "PATCH /api/steps/{id}", "DELETE /api/steps/{id}", "POST /api/steps/{id}/detail", "PUT /api/steps/{id}/raci",
        "POST /api/steps/{id}/insert", "POST /api/steps/{id}/dissolve", "PUT /api/steps/{id}/translations",
        "GET /api/steps/{id}/sheet", "PUT /api/steps/{id}/sheet", "POST /api/steps/{id}/sheet/validate", "POST /api/steps/{id}/sheet/clarification", "PUT /api/steps/{id}/sheet/clarification", "DELETE /api/steps/{id}/sheet/clarification",
        "POST /api/steps/{id}/documents", "PUT /api/steps/{id}/documents/{documentId}", "DELETE /api/steps/{id}/documents/{documentId}",
        "GET /api/steps/{id}/documents/available", "PUT /api/steps/{id}/uses/{documentId}", "DELETE /api/steps/{id}/uses/{documentId}",
        "POST /api/dissolutions/{id}/assign", "POST /api/dissolutions/{id}/remove", "POST /api/dissolutions/{id}/finish",
        "PATCH /api/flows/{id}", "PUT /api/flows/{id}/share", "POST /api/flows/{id}/reverse", "DELETE /api/flows/{id}", "PUT /api/flows/{id}/translations",
        "GET /api/health", "POST /api/desktop/quit", "POST /api/desktop/sample-project", "GET /api/admin/audit",
        "GET /api/admin/assistant", "PUT /api/admin/assistant", "POST /api/admin/assistant/test",
        "GET /api/maps/{id}/assistant", "POST /api/maps/{id}/assistant/exchanges", "POST /api/maps/{id}/assistant/prompt", "POST /api/maps/{id}/assistant/replies",
        "POST /api/assistant/exchanges", "POST /api/assistant/prompt", "POST /api/assistant/replies",
        "POST /api/maps/{id}/assistant/preview", "POST /api/maps/{id}/assistant/apply",
    ];

    [GeneratedRegex(@":[a-z]+\}")]
    private static partial Regex Constraint();

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_routes_of_the_api_are_exactly_those_of_the_table(EngineKind kind)
    {
        var host = await hosts.OfAsync(kind);
        var routes = host.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText is { } text && text.StartsWith("/api/", StringComparison.Ordinal) && !text.Contains("**", StringComparison.Ordinal))
            .SelectMany(endpoint => endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods
                .Select(method => $"{method} {Constraint().Replace(endpoint.RoutePattern.RawText!.TrimEnd('/'), "}")}"))
            .Order(StringComparer.Ordinal);
        Assert.Equal(ExpectedRoutes.Order(StringComparer.Ordinal), routes);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_openapi_document_describes_every_route_with_snake_case_bodies(EngineKind kind)
    {
        using var client = (await hosts.OfAsync(kind)).As(Logins.Reader);
        var document = (await client.GetAsync("/openapi/v1.json")).Ok();
        var paths = document.GetProperty("paths");
        var described = paths.EnumerateObject()
            .SelectMany(path => path.Value.EnumerateObject().Select(operation => $"{operation.Name.ToUpperInvariant()} {path.Name}"))
            .Order(StringComparer.Ordinal);
        Assert.Equal(ExpectedRoutes.Order(StringComparer.Ordinal), described);
        var lane = document.GetProperty("components").GetProperty("schemas").GetProperty("UpdateLaneBody").GetProperty("properties");
        Assert.Equal(["actor_user_id", "color", "headcount", "name", "translations", "type"], lane.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal(JsonValueKind.Undefined, lane.GetProperty("name").TryGetProperty("properties", out var inner) ? inner.ValueKind : JsonValueKind.Undefined);
    }

    /// <summary>
    /// The routes of the exchange whose body or reply is a file (D09: the client is generated from the contract): the four
    /// imports take a package as binary content, the export gives a ZIP archive, the copy a text.
    /// </summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_openapi_document_describes_the_files_that_the_exchange_receives_and_gives(EngineKind kind)
    {
        using var client = (await hosts.OfAsync(kind)).As(Logins.Reader);
        var document = (await client.GetAsync("/openapi/v1.json")).Ok();
        var paths = document.GetProperty("paths");
        var schemas = document.GetProperty("components").GetProperty("schemas");
        (string?, string?) Binary(JsonElement content, string mediaType)
        {
            var schema = content.GetProperty(mediaType).GetProperty("schema");
            if (schema.TryGetProperty("$ref", out var reference))
                schema = schemas.GetProperty(reference.GetString()!.Split('/')[^1]);
            return (schema.GetProperty("type").GetString(), schema.GetProperty("format").GetString());
        }

        foreach (var path in new[] { "/api/projects/import/preview", "/api/projects/import", "/api/projects/{id}/import/preview", "/api/projects/{id}/import" })
        {
            var body = paths.GetProperty(path).GetProperty("post").GetProperty("requestBody");
            Assert.True(body.GetProperty("required").GetBoolean());
            Assert.Equal(("string", "binary"), Binary(body.GetProperty("content"), "application/octet-stream"));
        }
        Assert.Equal(("string", "binary"), Binary(paths.GetProperty("/api/projects/{id}/export").GetProperty("post").GetProperty("responses").GetProperty("200")
            .GetProperty("content"), "application/zip"));
        Assert.Equal(("string", "binary"), Binary(paths.GetProperty("/api/projects/{id}/pmap").GetProperty("get").GetProperty("responses").GetProperty("200")
            .GetProperty("content"), "text/plain"));
    }
}
