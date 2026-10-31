// IfMatchRequirementTests.cs
// The If-Match required in server mode, optional in desktop mode: every write route classified, in which order with the rights, and nothing written (spec 5.3, A-2.16-04).

using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Topogenia.Host.Api;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Api;

public sealed partial class IfMatchRequirementTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    [GeneratedRegex(@":[a-z]+\}")]
    private static partial Regex Constraint();

    private const string Creation = "creates its object: there is no version to have read yet";

    private const string Exchange = "an exchange with the model of the assistant: no record, only its event of the audit (spec 3.0 section 6)";

    private const string SeveralObjects =
        "acts on several objects named by its body, each checked live by the domain, as one structure operation serialized by the lock of the project and undone as a whole";

    /// <summary>
    /// The write routes that read no `If-Match`, each with the reason why no version read by the client has to be checked
    /// (ADR 045, revision of 25/10/2026). Every other write route reads it: a new route fails the test until it reads the
    /// header or is added here with its reason.
    /// </summary>
    private static readonly Dictionary<string, string> Exemptions = new(StringComparer.Ordinal)
    {
        ["PUT /api/me/last-project"] = "a trace of the navigation written by the client when a project opens (AR-49), not an edit: no content read is replaced",
        ["POST /api/projects"] = Creation,
        ["POST /api/projects/{id}/export"] = "a read of the whole project: it writes only its audit event, no content read is replaced",
        ["POST /api/projects/import/preview"] = "the preview of an import: everything but the writing, nothing is written (AR-38)",
        ["POST /api/projects/import"] = Creation,
        ["POST /api/projects/{id}/import/preview"] = "the preview of a replacement: everything but the writing, nothing is written (AR-38)",
        ["POST /api/projects/import-pmap"] = Creation,
        ["POST /api/projects/{id}/duplicate"] = "the creation of a new project from a read of the source, which it does not change",
        ["PUT /api/projects/{id}/translations"] = "the import of a workbook in one pass (A-2.14-16): one row per text of any object, under the lock of the project",
        ["POST /api/trash/{id}/restore"] = "a project in the trash is frozen, no route changes it (A-2.15-14); a second restore or a purge finds it gone (INTROUVABLE)",
        ["DELETE /api/trash/{id}"] = "a project in the trash is frozen (A-2.15-14); a restore between the plan and the purge stops it (CONFLIT_VERSION, PurgeProjectCommand)",
        ["POST /api/maps/{id}/undo"] = "the operation log checks that every object touched still has the content the operation left (AR-81, OPERATION_DEPASSEE)",
        ["POST /api/maps/{id}/redo"] = "the operation log checks that every object touched still has the content the undo left (AR-81, OPERATION_DEPASSEE)",
        ["POST /api/maps/{id}/lanes"] = Creation,
        ["PUT /api/maps/{id}/lane-order"] = "the complete list of the lanes of the map, refused ORDRE_COULOIRS_INVALIDE when a lane was added or deleted since",
        ["POST /api/maps/{id}/steps"] = Creation,
        ["POST /api/maps/{id}/flows"] = Creation,
        ["POST /api/maps/{id}/command"] = "creates the steps and flows of the command bar, all or nothing (A-2.7)",
        ["PUT /api/maps/{id}/layout"] = SeveralObjects,
        ["POST /api/maps/{id}/move"] = SeveralObjects,
        ["POST /api/maps/{id}/deletion"] = SeveralObjects,
        ["POST /api/maps/{id}/paste"] = "creates a copy of the steps and flows of its body",
        ["POST /api/maps/{id}/transfer"] = SeveralObjects,
        ["POST /api/maps/{id}/group"] = SeveralObjects,
        ["POST /api/steps/{id}/insert"] = "creates a step next to the step of the route, which is an anchor and not the object changed",
        ["POST /api/steps/{id}/documents"] = Creation,
        ["POST /api/dissolutions/{id}/assign"] = "moves one element of the remainder, found in the open frame or refused (INTROUVABLE, REMONTEE_TERMINEE)",
        ["POST /api/dissolutions/{id}/remove"] = "removes one element of the remainder, found in the open frame or refused (INTROUVABLE, REMONTEE_TERMINEE)",
        ["POST /api/dissolutions/{id}/finish"] = "stores what is left of the remainder and closes the frame, idempotent on a closed frame (annex A 1)",
        ["POST /api/desktop/quit"] = "stops the application of the desktop mode: no record",
        ["POST /api/admin/assistant/test"] = "tests the connection to the model: no record, only its event of the audit (spec 3.0 section 4)",
        ["POST /api/maps/{id}/assistant/exchanges"] = Exchange,
        ["POST /api/assistant/exchanges"] = Exchange,
        ["POST /api/maps/{id}/assistant/prompt"] = "writes the prompt of an exchange for the copy and paste: no record (spec 3.0 section 5)",
        ["POST /api/assistant/prompt"] = "writes the prompt of an exchange for the copy and paste: no record (spec 3.0 section 5)",
        ["POST /api/maps/{id}/assistant/replies"] = "reads a reply pasted from the AI of the person: no record (spec 3.0 section 5)",
        ["POST /api/assistant/replies"] = "reads a reply pasted from the AI of the person: no record (spec 3.0 section 5)",
        ["POST /api/maps/{id}/assistant/preview"] = "compares a proposal with the map: no record, the structure version it read returned for the application (ADR 079)",
        ["POST /api/desktop/sample-project"] = Creation,
        ["* /api/{**rest}"] = "an unknown route of the API, refused INTROUVABLE: no record",
    };

    private sealed record Route(string Name, bool ReadsIfMatch);

    /// <summary>Every route of the host, one per method; a route that takes any method is named `*`.</summary>
    private static List<Route> Routes(ApiHost host) =>
        [.. host.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .SelectMany(endpoint => (endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? ["*"])
                .Select(method => new Route($"{method} {Constraint().Replace(endpoint.RoutePattern.RawText!.TrimEnd('/'), "}")}",
                    endpoint.Metadata.GetMetadata<MethodInfo>() is { } handler && IfMatchRequirement.ReadsIfMatch(handler))))];

    private async Task<ApiProject> ProjectAsync(EngineKind kind) => await ApiProject.CreateAsync(await hosts.OfAsync(kind));

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Every_write_route_reads_if_match_unless_it_is_exempted_with_its_reason(EngineKind kind)
    {
        var routes = Routes(await hosts.OfAsync(kind));
        var writes = routes.Where(route => route.Name.Split(' ')[0] is not ("GET" or "HEAD")).ToList();
        Assert.Empty(writes.Where(route => !route.ReadsIfMatch && !Exemptions.ContainsKey(route.Name)).Select(route => route.Name));
        Assert.Empty(writes.Where(route => route.ReadsIfMatch && Exemptions.ContainsKey(route.Name)).Select(route => route.Name));
        Assert.Empty(Exemptions.Keys.Except(writes.Select(route => route.Name), StringComparer.Ordinal));
        Assert.All(Exemptions.Values, reason => Assert.False(string.IsNullOrWhiteSpace(reason)));
        Assert.Empty(routes.Except(writes).Where(route => route.ReadsIfMatch).Select(route => route.Name));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_change_without_if_match_is_refused_428_in_server_mode_only_after_the_rights_and_writes_nothing(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        using var reader = project.Reader;
        using var stranger = project.Stranger;
        var lane = $"/api/lanes/{project.FirstLaneId}";
        (await stranger.PatchAsync(lane, new { name = "Achats" })).Refused(404, "INTROUVABLE");
        (await reader.PatchAsync(lane, new { name = "Achats" })).Refused(403, "ACCES_REFUSE");
        var withoutVersion = await facilitator.PatchAsync(lane, new { name = "Achats" });
        var name = (await project.MapAsync()).GetProperty("lanes")[0].GetProperty("name").GetString();
        if (kind == EngineKind.SqlServer)
        {
            withoutVersion.Refused(428, "VERSION_REQUISE");
            Assert.Equal("Acteur 1", name);
            var version = (await project.MapAsync()).GetProperty("lanes")[0].GetProperty("version").GetInt32();
            (await facilitator.PatchAsync(lane, new { name = "Achats" }, version)).Ok();
        }
        else
        {
            withoutVersion.Ok();
            Assert.Equal("Achats", name);
        }
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_profile_needs_its_version_in_server_mode_and_a_blank_if_match_counts_as_none(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var reader = project.Reader;
        var blank = await reader.SendAsync(HttpMethod.Patch, "/api/me", new { first_visit_done = true }, null, ("If-Match", " "));
        var me = (await reader.GetAsync("/api/me")).Ok();
        if (kind == EngineKind.SqlServer)
        {
            blank.Refused(428, "VERSION_REQUISE");
            Assert.False(me.GetProperty("first_visit_done").GetBoolean());
        }
        else
        {
            blank.Ok();
            Assert.True(me.GetProperty("first_visit_done").GetBoolean());
        }
    }
}
