// CommonHeadersTests.cs
// The rules of spec 5.4 shared by both modes: security headers on every reply, a restrictive permissions policy, and the limit of the body applied before reading it.

using System.Text;
using Topogenia.Host.Security;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Api;

public sealed class CommonHeadersTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private async Task<ApiProject> ProjectAsync(EngineKind kind) => await ApiProject.CreateAsync(await hosts.OfAsync(kind));

    private static void AssertSecurityHeaders(ApiResponse response)
    {
        Assert.Equal(SecurityHeaders.Policy, response.Headers["Content-Security-Policy"]);
        Assert.Equal(
            (SecurityHeaders.FrameOptions, "nosniff", "no-referrer", SecurityHeaders.Permissions, "same-origin", "same-origin"),
            (response.Headers["X-Frame-Options"], response.Headers["X-Content-Type-Options"], response.Headers["Referrer-Policy"],
                response.Headers["Permissions-Policy"], response.Headers["Cross-Origin-Opener-Policy"], response.Headers["Cross-Origin-Resource-Policy"]));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Every_reply_carries_the_security_headers_success_refusal_error_and_anonymous_alike(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var reader = project.Reader;
        using var stranger = project.Stranger;
        using var anonymous = project.Host.As(null);
        foreach (var response in new[]
        {
            await reader.GetAsync($"/api/maps/{project.RootMapId}"), await stranger.GetAsync($"/api/maps/{project.RootMapId}"),
            await reader.PostAsync($"/api/maps/{project.RootMapId}/lanes", new { name = "Achats" }), await reader.GetAsync("/api/nowhere"),
            await anonymous.GetAsync("/api/me"), await reader.SendAsync(HttpMethod.Put, "/api/me/last-project", "{", null),
        })
        {
            AssertSecurityHeaders(response);
        }
    }

    /// <summary>ASVS V8.2.1 (F-36): no reply of the API is kept by a browser or a proxy, a refusal and an anonymous one alike.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task No_reply_of_the_api_may_be_kept_in_a_cache(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var reader = project.Reader;
        using var stranger = project.Stranger;
        using var anonymous = project.Host.As(null);
        foreach (var response in new[]
        {
            await reader.GetAsync($"/api/maps/{project.RootMapId}"), await reader.GetAsync("/api/projects"), await stranger.GetAsync($"/api/maps/{project.RootMapId}"),
            await anonymous.GetAsync("/api/me"), await reader.GetAsync("/api/nowhere"),
        })
        {
            Assert.Equal(SecurityHeaders.NoStore, response.Headers["Cache-Control"]);
        }
    }

    /// <summary>
    /// The permissions policy that a reply carries is restrictive (spec 5.4): every directive closes its feature with the
    /// empty allow list `()`, and the powerful features of a page are all named. Read from the replies, not the constant.
    /// </summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_permissions_policy_closes_each_powerful_feature(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var reader = project.Reader;
        using var anonymous = project.Host.As(null);
        foreach (var response in new[] { await reader.GetAsync($"/api/maps/{project.RootMapId}"), await anonymous.GetAsync("/api/me") })
        {
            var directives = response.Headers["Permissions-Policy"].Split(',', StringSplitOptions.TrimEntries)
                .Select(directive => directive.Split('=', 2)).ToDictionary(pair => pair[0], pair => pair.Length == 2 ? pair[1] : "");
            Assert.All(directives, directive => Assert.Equal("()", directive.Value));
            Assert.Superset(new HashSet<string>(["camera", "microphone", "geolocation", "payment", "usb", "display-capture", "serial", "hid", "bluetooth"]),
                new HashSet<string>(directives.Keys));
        }
    }

    [Fact]
    public void The_policy_allows_no_inline_script_nor_eval_nor_framing_and_the_images_of_the_exports()
    {
        var directives = SecurityHeaders.Policy.Split(';', StringSplitOptions.TrimEntries).ToDictionary(directive => directive.Split(' ')[0], directive => directive);
        Assert.Equal("default-src 'self'", directives["default-src"]);
        Assert.Equal("script-src 'self'", directives["script-src"]);
        Assert.Equal("img-src 'self' data: blob:", directives["img-src"]);
        Assert.Equal("frame-ancestors 'none'", directives["frame-ancestors"]);
        Assert.DoesNotContain("unsafe", SecurityHeaders.Policy, StringComparison.Ordinal);
    }

    /// <summary>
    /// ADR 014 and ADR 111 point 1: the page reaches the application alone. `connect-src` (fetch, XHR, WebSocket,
    /// EventSource, sendBeacon) is `'self'`, and so is `default-src`; neither host of the links that the profile gives (the
    /// site, the page of the Store) is in the policy: they are opened by the browser in a tab of its own, never called by
    /// the page, which sends no referrer and shares no window with them. Read from the replies, in both modes.
    /// </summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_page_reaches_the_application_only_connect_src_self(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var reader = project.Reader;
        using var anonymous = project.Host.As(null);
        var links = (await reader.GetAsync("/api/me")).Ok().GetProperty("links");
        var hosts = new[] { links.GetProperty("site").GetString()!, links.GetProperty("store_page").GetString()! }.Select(address => new Uri(address).Host).ToList();
        Assert.Equal(2, hosts.Distinct(StringComparer.Ordinal).Count());
        foreach (var response in new[] { await reader.GetAsync($"/api/maps/{project.RootMapId}"), await anonymous.GetAsync("/api/me"), await reader.GetAsync("/api/nowhere") })
        {
            var policy = response.Headers["Content-Security-Policy"];
            var directives = policy.Split(';', StringSplitOptions.TrimEntries).ToDictionary(directive => directive.Split(' ')[0], directive => directive);
            Assert.Equal("connect-src 'self'", directives["connect-src"]);
            Assert.Equal("default-src 'self'", directives["default-src"]);
            Assert.All(hosts, host => Assert.DoesNotContain(host, policy, StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain("*", policy, StringComparison.Ordinal);
            Assert.Equal(("no-referrer", "same-origin"), (response.Headers["Referrer-Policy"], response.Headers["Cross-Origin-Opener-Policy"]));
        }
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_body_longer_than_one_megabyte_is_refused_413_before_it_is_read_and_nothing_is_written(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var name = new string('a', (int)BodySizeLimit.Default);
        var refused = await facilitator.PostAsync($"/api/maps/{project.RootMapId}/lanes", new { name });
        Assert.Equal(1, refused.Refused(413, "REQUETE_TROP_GROSSE").GetProperty("max").GetInt32());
        AssertSecurityHeaders(refused);
        Assert.Single((await project.MapAsync()).GetProperty("lanes").EnumerateArray());
        using var anonymous = project.Host.As(null);
        var body = Encoding.UTF8.GetString(new byte[BodySizeLimit.Default + 1]);
        Assert.Equal(413, (await anonymous.SendAsync(HttpMethod.Post, $"/api/maps/{project.RootMapId}/lanes", body, null)).Status);
        var justUnder = await facilitator.PostAsync($"/api/maps/{project.RootMapId}/lanes", new { name = "Achats", type = new string(' ', 1000) });
        Assert.NotEqual(413, justUnder.Status);
    }
}
