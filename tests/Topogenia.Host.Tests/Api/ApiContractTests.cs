// ApiContractTests.cs
// The contract the interface is generated from is the OpenAPI document the host serves, the same in both modes (D09, P6-T01).

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Topogenia.Domain.Errors;
using Topogenia.Host.Errors;
using Topogenia.Host.Json;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Api;

/// <summary>
/// web/src/api/generated/openapi.json is the document of <c>/openapi/v1.json</c>, written indented, without what names the
/// deployment and not the shape of the API: the address the request came to (<c>servers</c>) and the title made of the
/// product name (<c>info</c>), so that a new name stays one line (D13); the types of the client are generated from it (<c>npm run api</c>), and the
/// tests of the interface check that they are. To write it again after a change of the routes or of the bodies: set
/// CONTRACT_WRITE=1 and run this class, then <c>npm run api</c> in web/.
/// </summary>
public sealed class ApiContractTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private static string ContractPath => RepositoryPaths.Of("web", "src", "api", "generated", "openapi.json");

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_contract_of_the_interface_is_the_document_the_host_serves(EngineKind kind)
    {
        using var client = (await hosts.OfAsync(kind)).As(Logins.Reader);
        var reply = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(200, reply.Status);
        var served = Contract(reply.Body);
        if (Environment.GetEnvironmentVariable("CONTRACT_WRITE") == "1" && kind == EngineKind.Sqlite)
            await File.WriteAllTextAsync(ContractPath, served, TestContext.Current.CancellationToken);

        Assert.Equal(served, RepositoryPaths.Text("web", "src", "api", "generated", "openapi.json"));
    }

    /// <summary>
    /// The body of every refusal is in the contract, though no route declares its refusals: its three fields required,
    /// its code one of the codes of the catalogue of the messages, in their order, as a refusal of the API gives them.
    /// </summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_contract_describes_the_body_of_the_errors_with_every_code_that_has_a_message(EngineKind kind)
    {
        using var client = (await hosts.OfAsync(kind)).As(Logins.Reader);
        var body = (await client.GetAsync("/openapi/v1.json")).Ok().GetProperty("components").GetProperty("schemas").GetProperty(ErrorSchemaTransformer.SchemaName);
        var codes = ErrorMessages.Embedded().Of(ErrorMessages.FallbackLanguage).Keys.Order(StringComparer.Ordinal).ToList();

        Assert.Equal(["code", "details", "message"], body.GetProperty("required").EnumerateArray().Select(name => name.GetString()).Order(StringComparer.Ordinal));
        Assert.Equal(codes, body.GetProperty("properties").GetProperty("code").GetProperty("enum").EnumerateArray().Select(code => code.GetString()));
        Assert.Contains(ErrorCodes.NotFound, codes);
        Assert.Contains("PMAP_JSON", codes);
        (await client.GetAsync($"/api/maps/{Guid.NewGuid()}")).Refused(404, ErrorCodes.NotFound);
    }

    /// <summary>
    /// No schema of the contract is left empty, which the generator would type `unknown` (D09): the enumerations, written as
    /// their codes (<see cref="CodeEnumConverterFactory"/>), are strings of their codes in their order, and the lists of the
    /// bodies, read item by item (<see cref="NonNullItemsConverterFactory"/>), are arrays of their item.
    /// </summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Every_schema_of_the_contract_has_a_shape_the_enumerations_their_codes_and_the_lists_their_items(EngineKind kind)
    {
        using var client = (await hosts.OfAsync(kind)).As(Logins.Reader);
        var schemas = (await client.GetAsync("/openapi/v1.json")).Ok().GetProperty("components").GetProperty("schemas");
        string[] shapes = ["type", "properties", "enum", "oneOf", "items", "$ref"];
        Assert.Empty(schemas.EnumerateObject().Where(schema => !shapes.Any(shape => schema.Value.TryGetProperty(shape, out _))).Select(schema => schema.Name));

        var role = schemas.GetProperty("MemberRole");
        Assert.Equal("string", role.GetProperty("type").GetString());
        Assert.Equal(["reader", "contributor", "facilitator"], role.GetProperty("enum").EnumerateArray().Select(code => code.GetString()));
        Assert.Equal(["daily", "weekly", "monthly", "month_end_close", "quarterly", "yearly", "occasional", "per_case"],
            schemas.GetProperty("Frequency").GetProperty("enum").EnumerateArray().Select(code => code.GetString()));
        // The members of a nullable structure, which the generator does not visit (ADR 085): the volume of a start, the
        // lead time of a sheet.
        Assert.Equal(["day", "week", "month", "year"], schemas.GetProperty("VolumePeriod").GetProperty("enum").EnumerateArray().Select(code => code.GetString()));
        Assert.Equal(["minute", "hour", "day"], schemas.GetProperty("LeadTimeUnit").GetProperty("enum").EnumerateArray().Select(code => code.GetString()));
        // An optional field whose value is an object keeps its members: the volume of a PATCH of a step.
        var volume = schemas.GetProperty("OptionalOfVolumeBody");
        Assert.Equal(["period", "quantity"], volume.GetProperty("properties").EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal(["period", "quantity"], volume.GetProperty("required").EnumerateArray().Select(name => name.GetString()).Order(StringComparer.Ordinal));
        var members = schemas.GetProperty("ListOfMemberBody");
        Assert.Equal("array", members.GetProperty("type").GetString());
        var item = members.GetProperty("items");
        var itemSchema = item.TryGetProperty("$ref", out var reference) ? schemas.GetProperty(reference.GetString()!.Split('/')[^1]) : item;
        Assert.Equal(["is_key_contributor", "is_sponsor", "login", "role"], itemSchema.GetProperty("properties").EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
    }

    /// <summary>The document as the contract file holds it: indented, LF, without <c>servers</c> and <c>info</c>, ending with a line end.</summary>
    private static string Contract(string document)
    {
        var root = JsonNode.Parse(document)!.AsObject();
        root.Remove("servers");
        root.Remove("info");
        return root.ToJsonString(Indented).ReplaceLineEndings("\n") + "\n";
    }
}
