// ExportFileNameTests.cs
// The name of an exported file (A-1.2-16), the rule of the 1.x suggested folder name (T-UDOM-04) on fictional labels.

using Topogenia.Domain.Model;
using Topogenia.Domain.Values;
using Topogenia.Interchange.Files;
using Topogenia.Interchange.Packages;

namespace Topogenia.Interchange.Tests.Files;

public sealed class ExportFileNameTests
{
    [Theory]
    [InlineData("CONGES-E017", "Tableau récapitulatif", "CONGES-E017_Tableau-recapitulatif")]
    [InlineData("CONGES-E001", "Relevé des soldes : congés / RTT", "CONGES-E001_Releve-des-soldes-conges-RTT")]
    [InlineData("CONGES-E002", "Complétion du fichier des absences de l'équipe", "CONGES-E002_Completion-du-fichier-des-abse")]
    [InlineData("CONGES-E003", "Œuvre sociale", "CONGES-E003_OEuvre-sociale")]
    [InlineData("CONGES-E004", "!!!", "CONGES-E004")]
    [InlineData("CONGES-E005", "  _-Straße - Ærø-_  ", "CONGES-E005_Strasse-AEr")]
    public void SuggestedFolderName_CodeAndSanitisedLabel(string code, string label, string expected)
    {
        var name = ExportFileName.ShortName(code, label);

        Assert.Equal(expected, name);
        Assert.True(name.Length <= code.Length + 1 + ExportFileName.ShortNameMax);
    }

    [Fact]
    public void A_short_name_cut_at_thirty_characters_never_ends_with_a_dash()
    {
        Assert.Equal("CONGES_Demande-de-conges-annuels-suit", ExportFileName.ShortName("CONGES", "Demande de congés annuels - suite"));
        Assert.Equal("CONGES_aaaaaaaaaaaaaaaaaaaaaaaaaaaaa", ExportFileName.ShortName("CONGES", new string('a', 29) + " b"));
    }

    [Fact]
    public void The_file_of_a_package_is_named_by_the_code_the_short_name_of_the_project_and_the_utc_day_of_the_export()
    {
        var project = new Project(Guid.Empty, "CONGES", "Demande de congés", "fr", null, null, Classification.Internal, null, null, 0,
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), Guid.Empty, null, null, null, null, 1);

        var name = new PackageExporter().FileName(project, new DateTime(2026, 10, 25, 23, 59, 0, DateTimeKind.Utc));

        Assert.Equal("CONGES_Demande-de-conges_2026-10-25.topogenia", name);
    }
}
