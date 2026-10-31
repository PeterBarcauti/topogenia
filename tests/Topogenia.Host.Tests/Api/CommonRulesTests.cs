// CommonRulesTests.cs
// The rules shared by every route, through the whole host on both databases: JSON, errors, language, rights, If-Match (annex A 1).

using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Topogenia.Application.Commands;
using Topogenia.Application.Maps;
using Topogenia.Application.Ports;
using Topogenia.Domain.Workspace;
using Topogenia.Persistence.Tests.Infrastructure;
using Topogenia.Host.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Api;

public sealed class CommonRulesTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private async Task<ApiProject> ProjectAsync(EngineKind kind) => await ApiProject.CreateAsync(await hosts.OfAsync(kind));

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Bodies_and_replies_use_snake_case_names_and_the_codes_of_the_closed_lists(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var reply = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/lanes", new { name = "Achats", type = "department", sort_order = 0 })).Ok();
        var lane = reply.GetProperty("map").GetProperty("lanes").EnumerateArray().Single(item => item.GetProperty("id").GetGuid() == reply.GetProperty("id").GetGuid());
        Assert.Equal(("department", 0, "Achats"), (lane.GetProperty("type").GetString(), lane.GetProperty("sort_order").GetInt32(), lane.GetProperty("name").GetString()));
        Assert.True(reply.TryGetProperty("structure_version", out _));
        Assert.True(reply.TryGetProperty("operation_id", out _));
        Assert.False(reply.TryGetProperty("StructureVersion", out _));
        Assert.Equal("facilitator", reply.GetProperty("map").GetProperty("rights").GetProperty("role").GetString());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_non_member_is_told_not_found_and_a_weaker_role_is_refused(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var stranger = project.Stranger;
        using var reader = project.Reader;
        (await stranger.GetAsync($"/api/maps/{project.RootMapId}")).Refused(404, "INTROUVABLE");
        (await stranger.PostAsync($"/api/maps/{project.RootMapId}/lanes", new { name = "Achats" })).Refused(404, "INTROUVABLE");
        (await reader.PostAsync($"/api/maps/{project.RootMapId}/lanes", new { name = "Achats" })).Refused(403, "ACCES_REFUSE");
        Assert.Single((await project.MapAsync()).GetProperty("lanes").EnumerateArray());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task An_anonymous_request_gets_a_bare_401_from_the_fallback_policy_without_reaching_a_handler(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var anonymous = project.Host.As(null);
        foreach (var response in new[]
        {
            await anonymous.GetAsync($"/api/maps/{project.RootMapId}"), await anonymous.GetAsync("/api/me"),
            await anonymous.GetAsync("/api/nowhere"), await anonymous.GetAsync("/index.html"),
            await anonymous.PostAsync($"/api/maps/{project.RootMapId}/lanes", new { name = "Achats" }),
        })
        {
            Assert.Equal((401, ""), (response.Status, response.Body));
        }
        Assert.Single((await project.MapAsync()).GetProperty("lanes").EnumerateArray());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_health_is_anonymous_and_minimal_in_server_mode_only_and_detailed_for_an_authenticated_user(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var anonymous = project.Host.As(null);
        using var reader = project.Reader;
        var health = await anonymous.GetAsync("/api/health");
        if (kind == EngineKind.Sqlite)
        {
            Assert.Equal((401, ""), (health.Status, health.Body));
        }
        else
        {
            Assert.Equal(200, health.Status);
            Assert.Equal(["status"], health.Json.EnumerateObject().Select(property => property.Name));
            Assert.Equal("ok", health.Json.GetProperty("status").GetString());
        }
        var detailed = (await reader.GetAsync("/api/health")).Ok();
        Assert.Equal(["status", "database", "version", "timestamp"], detailed.EnumerateObject().Select(property => property.Name));
        Assert.Equal("ok", detailed.GetProperty("database").GetString());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_message_is_in_the_language_of_the_profile_else_of_the_browser_else_french(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var stranger = project.Stranger;
        var url = $"/api/maps/{project.RootMapId}";
        static string Message(ApiResponse response) => response.Json.GetProperty("message").GetString()!;
        var french = Message(await stranger.GetAsync(url));
        var english = Message(await stranger.SendAsync(HttpMethod.Get, url, null, null, ("Accept-Language", "en-GB,en;q=0.8")));
        var unknown = Message(await stranger.SendAsync(HttpMethod.Get, url, null, null, ("Accept-Language", "eo-XX")));
        Assert.NotEqual(french, english);
        Assert.Equal(french, unknown);
        var me = (await stranger.GetAsync("/api/me")).Ok();
        (await stranger.PatchAsync("/api/me", new { language = "en" }, me.GetProperty("version").GetInt32())).Ok();
        Assert.Equal(english, Message(await stranger.SendAsync(HttpMethod.Get, url, null, null, ("Accept-Language", "fr-FR"))));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_body_that_is_not_json_or_of_the_wrong_shape_is_an_invalid_request(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var url = $"/api/maps/{project.RootMapId}/lanes";
        (await facilitator.PostAsync(url, "{ name: ")).Refused(400, "REQUETE_INVALIDE");
        (await facilitator.PostAsync(url, new { name = "Achats", sort_order = "first" })).Refused(400, "REQUETE_INVALIDE");
        (await facilitator.PutAsync($"/api/maps/{project.RootMapId}/lane-order", new { order = "x" })).Refused(400, "REQUETE_INVALIDE");
        (await facilitator.PutAsync($"/api/projects/{project.Id}/members", "[null]")).Refused(400, "REQUETE_INVALIDE");
        (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/paste", "{\"steps\": [null]}")).Refused(400, "REQUETE_INVALIDE");
        Assert.Single((await project.MapAsync()).GetProperty("lanes").EnumerateArray());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task An_unknown_route_of_the_api_is_not_found_in_the_format_of_the_errors(EngineKind kind)
    {
        using var client = (await hosts.OfAsync(kind)).As(Logins.Admin);
        (await client.GetAsync("/api/nowhere")).Refused(404, "INTROUVABLE");
        (await client.PostAsync($"/api/maps/{Guid.NewGuid()}/nothing", new { })).Refused(404, "INTROUVABLE");
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task If_match_accepts_quotes_and_refuses_an_unreadable_version(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var url = $"/api/lanes/{project.FirstLaneId}";
        var version = (await project.MapAsync()).GetProperty("lanes")[0].GetProperty("version").GetInt32();
        (await facilitator.SendAsync(HttpMethod.Patch, url, new { name = "Ventes" }, null, ("If-Match", version.ToString(System.Globalization.CultureInfo.InvariantCulture)))).Ok();
        (await facilitator.SendAsync(HttpMethod.Patch, url, new { name = "Achats" }, null, ("If-Match", "\"abc\""))).Refused(400, "VERSION_INVALIDE");
        Assert.Equal("Ventes", (await project.MapAsync()).GetProperty("lanes")[0].GetProperty("name").GetString());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_stale_version_is_a_conflict_that_names_the_author_and_says_how_long_ago(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var url = $"/api/lanes/{project.FirstLaneId}";
        var version = (await project.MapAsync()).GetProperty("lanes")[0].GetProperty("version").GetInt32();
        (await facilitator.PatchAsync(url, new { name = "Ventes" }, version)).Ok();
        var details = (await facilitator.PatchAsync(url, new { name = "Achats" }, version)).Refused(409, "CONFLIT_VERSION");
        Assert.Equal("anne", details.GetProperty("by").GetString());
        Assert.Equal("à l'instant", details.GetProperty("when").GetString());
        Assert.Equal(JsonValueKind.String, details.GetProperty("when_iso").ValueKind);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task An_unexpected_failure_is_an_internal_error_in_the_json_format_that_says_nothing_of_its_cause(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        await project.Host.WithServicesAsync(services => services.AddSingleton<IMapViewBuilder, BrokenViews>(), Logins.Facilitator, async facilitator =>
        {
            var response = await facilitator.PostAsync($"/api/maps/{project.RootMapId}/lanes", new { name = "Achats" });
            Assert.Empty(response.Refused(500, "ERREUR_INTERNE").EnumerateObject());
            Assert.DoesNotContain(BrokenViews.Secret, response.Body, StringComparison.Ordinal);
        });
        Assert.Single((await project.MapAsync()).GetProperty("lanes").EnumerateArray());
    }

    /// <summary>A builder of views that fails with a message naming a file, as a failure of an adapter could.</summary>
    public sealed class BrokenViews : IMapViewBuilder
    {
        public const string Secret = @"C:\Data\secret.db";

        public WorkspaceContent Content => WorkspaceContent.None;

        public MapView Build(ProjectWorkspace workspace, Guid mapId, MapViewer viewer) => throw new InvalidOperationException($"Cannot read {Secret}.");
    }
}
