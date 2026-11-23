// ProfileLinksRoutesTests.cs
// The links to the website and to the page of the Store that the profile gives (ADR 111 points 2 to 4): by default in both modes, none with the switch SiteLinks off, the Store said only in desktop mode run from its package.

using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Topogenia.Host.Api.Profile;
using Topogenia.Host.Composition;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Api;

public sealed class ProfileLinksRoutesTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    /// <summary>The two addresses, written here as the decision gives them (ADR 111 point 2), not read from the code under test.</summary>
    private static readonly (string?, string?) Expected = ("https://topogenia.com", "https://apps.microsoft.com/detail/9NZ56VV4N6QR");

    /// <summary>A family name of a package, fictitious: the shape `Name_publisherhash` that Windows gives.</summary>
    private const string Family = "Contoso.Maps_8wekyb3d8bbwe";

    /// <summary>The links of a profile, or null when it gives none (the property is always there).</summary>
    private static (string?, string?)? LinksOf(JsonElement profile)
    {
        var links = profile.GetProperty("links");
        return links.ValueKind == JsonValueKind.Null ? null : (links.GetProperty("site").GetString(), links.GetProperty("store_page").GetString());
    }

    /// <summary>The three replies that carry the profile: read, changed (a change that changes nothing), the last project remembered.</summary>
    private static async Task<IReadOnlyList<JsonElement>> RepliesAsync(ApiClient client, Guid projectId)
    {
        var me = (await client.GetAsync("/api/me")).Ok();
        var unchanged = (await client.PatchAsync("/api/me", new { }, me.GetProperty("version").GetInt32())).Ok();
        var remembered = (await client.PutAsync("/api/me/last-project", new { project_id = projectId })).Ok();
        return [me, unchanged, remembered];
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_profile_gives_the_site_and_the_store_page_by_default_in_both_modes(EngineKind kind)
    {
        var project = await ApiProject.CreateAsync(await hosts.OfAsync(kind));
        foreach (var client in new[] { project.Admin, project.Reader })
        {
            using var caller = client;
            foreach (var profile in await RepliesAsync(caller, project.Id))
            {
                Assert.Equal(Expected, LinksOf(profile));
                Assert.Equal(["site", "store_page"], profile.GetProperty("links").EnumerateObject().Select(property => property.Name));
                // The tests do not run from a package of the Store: the version is the portable one, or the server.
                Assert.False(profile.GetProperty("store").GetBoolean());
            }
        }
    }

    /// <summary>
    /// The switch is read by the host alone, which then gives no link on any reply of the profile, for the administrator as
    /// for anyone; the same host without the switch still gives them (the switch alone changed).
    /// </summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task With_SiteLinks_false_the_profile_gives_no_link_in_both_modes(EngineKind kind)
    {
        var project = await ApiProject.CreateAsync(await hosts.OfAsync(kind));
        await project.Host.WithVariantAsync(services => services.PostConfigure<ProductOptions>(options => options.SiteLinks = false), async clientOf =>
        {
            foreach (var login in new[] { Logins.Admin, Logins.Reader })
            {
                using var caller = clientOf(login);
                foreach (var profile in await RepliesAsync(caller, project.Id))
                {
                    Assert.Equal(JsonValueKind.Null, profile.GetProperty("links").ValueKind);
                    Assert.False(profile.GetProperty("store").GetBoolean());
                }
            }
        });
        using var admin = project.Admin;
        Assert.Equal(Expected, LinksOf((await admin.GetAsync("/api/me")).Ok()));
    }

    [Fact]
    public void The_store_is_said_only_in_desktop_mode_run_from_its_package()
    {
        var desktop = new ProductOptions { Mode = HostMode.Desktop };
        var server = new ProductOptions { Mode = HostMode.Server };
        Assert.True(ProfileLinks.Of(desktop, Family).Store);
        Assert.False(ProfileLinks.Of(desktop, null).Store);
        Assert.False(ProfileLinks.Of(server, Family).Store);
        Assert.False(ProfileLinks.Of(server, null).Store);

        // The links follow the switch alone, in both modes, with a package or without.
        foreach (var options in new[] { desktop, server })
        {
            foreach (var family in new[] { Family, null })
            {
                var links = ProfileLinks.Of(options, family).Links;
                Assert.Equal(Expected, (links?.Site, links?.StorePage));
            }
        }
        var off = new ProductOptions { Mode = HostMode.Desktop, SiteLinks = false };
        Assert.True(ProfileLinks.Of(off, Family).Store);
        Assert.Null(ProfileLinks.Of(off, Family).Links);
        Assert.Null(ProfileLinks.Of(new ProductOptions { Mode = HostMode.Server, SiteLinks = false }, null).Links);

        // The family name of the package is never given: it carries the pseudonym of the publisher (ADR 111 point 4).
        Assert.DoesNotContain("Contoso", JsonSerializer.Serialize(ProfileLinks.Of(desktop, Family)), StringComparison.Ordinal);
    }
}
