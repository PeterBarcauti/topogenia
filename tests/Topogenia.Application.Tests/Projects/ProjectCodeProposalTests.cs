// ProjectCodeProposalTests.cs
// The code proposed from the name of a new project (3.4, lot 3, ADR 111 point 7): the table of cases, the other scripts and the short words.

using Topogenia.Application.Projects;

namespace Topogenia.Application.Tests.Projects;

public sealed class ProjectCodeProposalTests
{
    /// <summary>
    /// The table of section 3.6 of the framing of the lot: one word, its first six letters; two words, three letters of
    /// each; three to six words, their initials; more, the initials of the first six; folded first (accents, ligatures,
    /// ß, ł, ı); digits left out; a name without a letter, or whose code would have fewer than three letters, gives none.
    /// </summary>
    [Theory]
    [InlineData("Achats", "ACHATS")]
    [InlineData("Recrutement", "RECRUT")]
    [InlineData("Demande de congés", "DEMCON")]
    [InlineData("Gestion des achats", "GDA")]
    [InlineData("Traitement des factures fournisseurs", "TDFF")]
    [InlineData("Clôture mensuelle", "CLOMEN")]
    [InlineData("Straße", "STRASS")]
    [InlineData("Œuvre", "OEUVRE")]
    [InlineData("Łódź", "LODZ")]
    [InlineData("İşe alım", "ISEALI")]
    [InlineData("La paie", "PAIE")]
    [InlineData("Order-to-cash 2026", "ORDCAS")]
    [InlineData("RH", null)]
    [InlineData("Analyse besoins choix devis envoi facture gestion", "ABCDEF")]
    [InlineData("  achats  ", "ACHATS")]
    [InlineData("2026", null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("— / …", null)]
    public void Every_case_of_the_table_gives_its_code_or_none(string name, string? code) =>
        Assert.Equal(code, ProjectCodeProposal.FromName(name));

    [Theory]
    [InlineData("発注", null)]
    [InlineData("Закупки", null)]
    [InlineData("Αγορές", null)]
    [InlineData("発注 Achats", "ACHATS")]
    [InlineData("Закупки et achats", "ACHATS")]
    public void A_name_of_another_script_than_latin_gives_no_code(string name, string? code) =>
        Assert.Equal(code, ProjectCodeProposal.FromName(name));

    /// <summary>
    /// Arbitration 8 of the lot: the words of fewer than three letters are left out while a longer one remains; when none
    /// remains, the short words make the code by the same rule (« La RH », three letters of each); a code of fewer than
    /// three letters is never proposed, the creation would refuse it (« RH », « A »): the person types the code.
    /// </summary>
    [Theory]
    [InlineData("La paie", "PAIE")]
    [InlineData("Plan de la paie", "PLAPAI")]
    [InlineData("Paie de la RH", "PAIE")]
    [InlineData("RH", null)]
    [InlineData("La RH", "LARH")]
    [InlineData("A", null)]
    [InlineData("Of to", "OFTO")]
    [InlineData("Of to by", "OTB")]
    public void Short_words_are_left_out_unless_none_remains(string name, string? code) =>
        Assert.Equal(code, ProjectCodeProposal.FromName(name));
}
