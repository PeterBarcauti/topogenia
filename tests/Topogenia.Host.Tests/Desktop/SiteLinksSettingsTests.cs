// SiteLinksSettingsTests.cs
// The switch SiteLinks of the operation (ADR 111 point 3): in settings.json of the desktop mode, true or false of JSON alone, any other value stops the start with the property; in the example of the server mode, a line in a comment.

using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Topogenia.Host.Api.Profile;
using Topogenia.Host.Composition;
using Topogenia.Host.Desktop;
using Topogenia.Host.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Desktop;

public sealed class SiteLinksSettingsTests
{
    /// <summary>The options of the host once the settings file is laid over a configuration that says nothing of the switch.</summary>
    private static ProductOptions OptionsOf(string json) =>
        ProductOptions.Of(new ConfigurationBuilder().AddInMemoryCollection(DesktopSettings.Read(json)).Build());

    /// <summary>A file and what it gives: the switch on or off, or null for a file that stops the start on the property.</summary>
    public static TheoryData<string, bool?> Files() => new()
    {
        { "{}", true },
        { """{ "SiteLinks": true }""", true },
        { """{ "SiteLinks": false }""", false },
        { """{ "siteLinks": false, "Port": 0 }""", false },
        { """{ "SiteLinks": "false" }""", null },
        { """{ "SiteLinks": "true" }""", null },
        { """{ "SiteLinks": 0 }""", null },
        { """{ "SiteLinks": 1 }""", null },
        { """{ "SiteLinks": null }""", null },
        { """{ "SiteLinks": "no" }""", null },
        { """{ "SiteLinks": [] }""", null },
        { """{ "SiteLinks": {} }""", null },
        { """{ "SiteLinks": "" }""", null },
    };

    [Theory]
    [MemberData(nameof(Files))]
    public void The_settings_file_takes_SiteLinks_as_true_or_false_and_refuses_any_other_value(string json, bool? expected)
    {
        if (expected is { } on)
        {
            var options = OptionsOf(json);
            Assert.Equal(on, options.SiteLinks);
            // The switch off, the profile gives no link: the whole chain from the file to the profile.
            Assert.Equal(on, ProfileLinks.Of(options, null).Links is not null);
            return;
        }
        var refusal = Assert.Throws<DesktopSettingsException>(() => DesktopSettings.Read(json));
        Assert.Equal(("SiteLinks", DesktopSettings.InvalidSettings), (refusal.Property, DesktopSettingsException.Code));
    }

    /// <summary>
    /// The server mode reads the switch from its settings file (the section of the product): the example that ships names
    /// it in a comment, so that the links stay shown until the operator writes it.
    /// </summary>
    [Fact]
    public void The_example_of_the_server_settings_names_the_switch_in_a_comment()
    {
        var example = RepositoryPaths.Text("build", "server", "appsettings.Production.example.json");
        Assert.Contains("// \"SiteLinks\": false", example, StringComparison.Ordinal);
        var section = JsonDocument.Parse(example, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip }).RootElement.GetProperty("{{PRODUCT}}");
        Assert.False(section.TryGetProperty("SiteLinks", out _));
        var server = ProductOptions.Of(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"{ProductOptions.Section}:Mode"] = nameof(HostMode.Server),
            [$"{ProductOptions.Section}:SiteLinks"] = "false",
        }).Build());
        Assert.False(server.SiteLinks);
        Assert.Null(ProfileLinks.Of(server, null).Links);
    }
}
