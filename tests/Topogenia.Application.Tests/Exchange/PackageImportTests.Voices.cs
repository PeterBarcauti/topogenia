// PackageImportTests.Voices.cs
// The import of an exchange package that carries the voices of its project (ADR 103): written beside the new project, refused whole when invalid, kept by a replaced project.

using Topogenia.Application.Presentation;
using Topogenia.Application.Tests.Support;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;

namespace Topogenia.Application.Tests.Exchange;

public sealed partial class PackageImportTests
{
    /// <summary>The voices of a package of the format 2.4 (ADR 103) are written beside the new project, by the importer, in version 1.</summary>
    [Fact]
    public async Task An_import_writes_the_voices_of_its_project_and_leaves_out_those_of_a_language_not_configured_here()
    {
        var (fixture, _, import) = Arrange(new FakeImporter { Voices = [("fr", "Hortense", true), ("de", "Voix allemande", true), ("en", "Julie", false)] });
        fixture.ActingAs(fixture.Project.Admin);

        var imported = await import.ImportAsync([1], null, withMembers: false, Token);

        Assert.Equal([(imported.Id, "en", "Julie", false), (imported.Id, "fr", "Hortense", true)],
            fixture.Database.Voices.Values.Select(voice => (voice.ProjectId, voice.Language, voice.Name, voice.Local)).Order());
        Assert.All(fixture.Database.Voices.Values, voice => Assert.Equal((1, TestProject.Now, fixture.Project.Admin.Id), (voice.Version, voice.UpdatedAt, voice.UpdatedBy)));
    }

    /// <summary>The schema of the package already refuses such a name: the rule of the entry is the last guard, on the name as it is written.</summary>
    [Theory]
    [InlineData("fr", "<b>Hortense</b>")]
    [InlineData("fr", " Hortense ")]
    [InlineData("fr", "")]
    [InlineData("fr", "Hortense\nJulie")]
    [InlineData("en", "Deux fois dans une langue")]
    public async Task A_voice_that_its_rule_refuses_or_given_twice_for_a_language_refuses_the_package_and_nothing_is_written(string language, string name)
    {
        var (fixture, _, import) = Arrange(new FakeImporter { Voices = [("en", "Julie", false), (language, name, true)] });
        fixture.ActingAs(fixture.Project.Admin);

        var refusal = await Assert.ThrowsAsync<DomainException>(() => import.ImportAsync([1], null, withMembers: false, Token));

        Assert.Equal((ErrorCodes.InvalidPackage, (object?)"values"), (refusal.Code, refusal.Details["reason"]));
        Assert.Equal(["presentationVoices"], (IEnumerable<string>)refusal.Details["fields"]!);
        Assert.DoesNotContain(fixture.Database.All<Project>(), project => project.Code == "VIS");
        Assert.Empty(fixture.Database.Voices);
    }

    /// <summary>A name of 200 characters is the longest kept; one more refuses the package.</summary>
    [Fact]
    public async Task A_voice_named_by_200_characters_is_imported_and_one_of_201_refuses_the_package()
    {
        var (fixture, _, import) = Arrange(new FakeImporter { Voices = [("fr", new string('v', PresentationVoiceRules.NameMax), true)] });
        fixture.ActingAs(fixture.Project.Admin);
        await import.ImportAsync([1], null, withMembers: false, Token);
        Assert.Equal(200, Assert.Single(fixture.Database.Voices.Values).Name.Length);

        var (other, _, tooLong) = Arrange(new FakeImporter { Voices = [("fr", new string('v', PresentationVoiceRules.NameMax + 1), true)] });
        other.ActingAs(other.Project.Admin);
        var refusal = await Assert.ThrowsAsync<DomainException>(() => tooLong.ImportAsync([1], null, withMembers: false, Token));
        Assert.Equal(["presentationVoices"], (IEnumerable<string>)refusal.Details["fields"]!);
    }

    /// <summary>The preview of an import validates as the import does and writes nothing.</summary>
    [Fact]
    public async Task The_preview_of_a_package_refuses_its_invalid_voices_and_writes_none_of_the_valid_ones()
    {
        var (fixture, _, import) = Arrange(new FakeImporter { Voices = [("fr", "Hortense", true)] });
        fixture.ActingAs(fixture.Project.Admin);
        await import.PreviewAsync([1], null, withMembers: false, Token);
        Assert.Empty(fixture.Database.Voices);

        var (other, _, twice) = Arrange(new FakeImporter { Voices = [("fr", "Hortense", true), ("fr", "Julie", true)] });
        other.ActingAs(other.Project.Admin);
        var refusal = await Assert.ThrowsAsync<DomainException>(() => twice.PreviewAsync([1], null, withMembers: false, Token));
        Assert.Equal((ErrorCodes.InvalidPackage, (object?)"values"), (refusal.Code, refusal.Details["reason"]));
    }

    [Fact]
    public async Task A_replacement_writes_the_voices_of_the_new_project_and_the_replaced_one_keeps_its_own_in_the_trash()
    {
        var (fixture, _, import) = Arrange(new FakeImporter("NEUF") { Voices = [("fr", "Julie", false)] });
        var old = fixture.Project.ProjectId;
        fixture.Database.AddVoice(new PresentationVoice(old, "fr", "Hortense", true, 3, TestProject.Now, fixture.Project.Facilitator.Id));
        fixture.ActingAs(fixture.Project.Facilitator);

        var imported = await import.ReplaceAsync(old, [1], withMembers: false, version: null, Token);

        Assert.NotEqual(old, imported.Id);
        Assert.Equal(("Hortense", true, 3), (fixture.Database.Voices[(old, "fr")].Name, fixture.Database.Voices[(old, "fr")].Local, fixture.Database.Voices[(old, "fr")].Version));
        Assert.Equal(("Julie", false, 1, fixture.Project.Facilitator.Id), (fixture.Database.Voices[(imported.Id, "fr")].Name, fixture.Database.Voices[(imported.Id, "fr")].Local,
            fixture.Database.Voices[(imported.Id, "fr")].Version, fixture.Database.Voices[(imported.Id, "fr")].UpdatedBy));
        Assert.Equal(2, fixture.Database.Voices.Count);
    }
}
