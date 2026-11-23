// PmapInsertionReadTests.cs
// The reading of a file to insert as the detail of a step (3.3, F-208): a language that is not configured left out and listed, not a fault, the language of the file still required; the levels counted; the bound of the levels held equal to the one of the format.

using Topogenia.Application.Exchange;
using Topogenia.Domain.Errors;
using Topogenia.Interchange.Pmap;

namespace Topogenia.Interchange.Tests.Pmap;

public sealed class PmapInsertionReadTests
{
    /// <summary>A step translated in German and a comment with a line in Italian, neither configured, and a detail below the step.</summary>
    private const string Foreign = """
        { "format": "topogenia", "version": "1.5", "project": { "name": "Achats", "translations": { "en": "Purchasing", "de": "Einkauf" } },
          "map": { "lanes": [{ "id": "c1", "name": "Demandeur", "translations": { "de": "Antragsteller" } }],
                   "steps": [{ "id": "e1", "lane": "c1", "label": "Saisir", "translations": { "en": "Enter", "de": "Erfassen" },
                               "comment": { "text": "Tout part d'ici.", "translations": { "it": "Tutto parte da qui." } },
                               "sub_map": { "lanes": [{ "id": "c2", "name": "X" }], "steps": [{ "id": "e2", "lane": "c2", "label": "Fin", "type": "end" }] } }] } }
        """;

    private static readonly PmapFiles Format = new();

    [Fact]
    public void A_file_to_insert_leaves_out_the_languages_that_are_not_configured_and_lists_them_where_an_opening_refuses_them()
    {
        var file = Format.ReadInsertion(Foreign, PmapReaderTests.Languages, "ACI");
        Assert.Equal(["de", "it"], file.DroppedLanguages);
        Assert.Equal(("fr", 2), (file.Language, file.Depth));
        Assert.Equal(new Dictionary<string, string> { ["en"] = "Purchasing" }, file.NameTranslations);

        var refused = Assert.Throws<DomainException>(() => Format.Read(Foreign, PmapReaderTests.Languages, "ACI"));
        var paths = ((IEnumerable<FileError>)refused.Details[FileError.DetailsKey]!).Select(error => (error.Path, error.Code)).ToList();
        Assert.Contains(("map.steps[0].translations.de", FileErrorCodes.UnknownLanguage), paths);
        Assert.Contains(("map.steps[0].comment.translations.it", FileErrorCodes.UnknownLanguage), paths);
    }

    [Fact]
    public void A_file_whose_own_language_is_not_configured_is_refused_even_to_be_inserted()
    {
        var refused = Assert.Throws<DomainException>(() => Format.ReadInsertion(Foreign.Replace("\"name\": \"Achats\"", "\"name\": \"Achats\", \"language\": \"de\"",
            StringComparison.Ordinal), PmapReaderTests.Languages, "ACI"));
        Assert.Equal("project.language", ((IEnumerable<FileError>)refused.Details[FileError.DetailsKey]!).Single().Path);
    }

    [Fact]
    public void The_levels_an_insertion_leaves_are_bounded_by_those_a_file_carries() =>
        Assert.Equal(PmapFormat.DepthMax, PmapInsertionRules.DepthMax);
}
