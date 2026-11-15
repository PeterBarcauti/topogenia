// ProjectsAndSecurityInfrastructurePortedTests.cs
// The 1.x unit tests of the infrastructure read through the whole host on both databases: language of the browser, unreadable If-Match (T-UINF-05, T-UINF-16).

using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Ported;

public sealed class ProjectsAndSecurityInfrastructurePortedTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    /// <summary>
    /// `Accept-Language` and the effective language it gives to a user without a language in his profile: the configured
    /// languages of the tests are French and English, French by default (the 1.x fell back on English).
    /// </summary>
    private static readonly (string? Header, string Expected)[] BrowserLanguages =
    [
        ("fr-FR,fr;q=0.9,en;q=0.8", "fr"),
        ("en-GB,en;q=0.9", "en"),
        ("eo-XX,eo;q=0.9,en;q=0.5", "en"),
        ("eo-XX", "fr"),
        ("eo;q=0.9,en;q=0", "fr"),
        ("pt-PT,pt;q=0.9,en;q=0.5", "pt"),
        ("fr;q=0.5,en;q=0.8", "en"),
        ("en;q=0.5,fr;q=0.8", "fr"),
        ("", "fr"),
        (null, "fr"),
    ];

    // T-UINF-05
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_language_of_the_browser_is_the_first_configured_one_by_weight_else_the_default_one(EngineKind kind)
    {
        using var user = (await hosts.OfAsync(kind)).As($@"TEST\nav{Guid.NewGuid():N}"[..20]);
        var read = new List<(string?, string?)>();
        foreach (var (header, _) in BrowserLanguages)
        {
            var me = header is null
                ? (await user.GetAsync("/api/me")).Ok()
                : (await user.SendAsync(HttpMethod.Get, "/api/me", null, null, ("Accept-Language", header))).Ok();
            Assert.Equal(System.Text.Json.JsonValueKind.Null, me.GetProperty("language").ValueKind);
            read.Add((header, me.GetProperty("effective_language").GetString()));
        }
        Assert.Equal(BrowserLanguages.Select(entry => (entry.Header, (string?)entry.Expected)), read);
    }

    // T-UINF-16
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task An_unreadable_if_match_is_refused_and_writes_nothing(EngineKind kind)
    {
        var project = await ApiProject.CreateAsync(await hosts.OfAsync(kind));
        using var facilitator = project.Facilitator;
        var url = $"/api/lanes/{project.FirstLaneId}";
        foreach (var unreadable in new[] { "pas-du-base64", "AAAA", "-1", "1.5", "99999999999", "\"\"", "W/\"1\"", "\"1" })
            (await facilitator.SendAsync(HttpMethod.Patch, url, new { name = "Achats" }, null, ("If-Match", unreadable))).Refused(400, "VERSION_INVALIDE");
        var lane = (await project.MapAsync()).GetProperty("lanes")[0];
        Assert.Equal("Acteur 1", lane.GetProperty("name").GetString());
        (await facilitator.PatchAsync(url, new { name = "Achats" }, lane.GetProperty("version").GetInt32())).Ok();
    }
}
