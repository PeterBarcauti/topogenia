// LanguagesTests.cs
// The languages of the host (ADR 083): the eighteen languages by default with French first chosen, the workbook of the translations on the target languages chosen, its columns the languages of origin and those targets.

using Topogenia.Host.Composition;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Maps;

public sealed class LanguagesTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    [Fact]
    public void Without_configuration_the_eighteen_languages_of_the_application_in_the_order_of_their_names_French_by_default()
    {
        var settings = new LanguageOptions().ToSettings();
        Assert.Equal(["cs", "de", "en", "es", "fr", "it", "hu", "nl", "pl", "pt", "ro", "sv", "tr", "el", "ru", "uk", "zh", "ja"], settings.Languages);
        Assert.Equal("fr", settings.DefaultLanguage);
        Assert.Equal(("Ator 1", "担当 1", "Acteur 1"), (settings.FirstLaneName("pt"), settings.FirstLaneName("ja"), settings.FirstLaneName("fr")));
        // A list configured without a default takes its first language, as before.
        Assert.Equal("de", new LanguageOptions { Supported = ["de", "en"] }.ToSettings().DefaultLanguage);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_workbook_gives_the_texts_missing_in_the_languages_chosen_its_columns_the_origin_and_those_languages(EngineKind kind)
    {
        var host = await hosts.OfAsync(kind);
        var project = await ApiProject.CreateAsync(host);
        using var facilitator = project.Facilitator;
        foreach (var (label, column) in new[] { ("Saisir", 0), ("Envoyer", 1) })
            (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/steps", new { lane_id = project.FirstLaneId, column, label })).Ok();
        var chosen = (await facilitator.GetAsync($"/api/projects/{project.Id}/translations?scope=missing&languages=pt,de")).Ok();
        // The language of origin first, the column read from, then the targets in the order of the configuration.
        Assert.Equal(["fr", "de", "pt"], chosen.GetProperty("languages").EnumerateArray().Select(language => language.GetString()));
        // The project, its first lane and its two steps (the root map bears the name of the project).
        Assert.Equal(4, chosen.GetProperty("texts").GetArrayLength());
        var all = (await facilitator.GetAsync($"/api/projects/{project.Id}/translations?scope=missing")).Ok();
        Assert.Equal(18, all.GetProperty("languages").GetArrayLength());
        (await facilitator.GetAsync($"/api/projects/{project.Id}/translations?scope=missing&languages=pt,eo")).Refused(400, "LANGUE_INVALIDE");
    }
}
