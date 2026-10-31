// PmapProposalTests.cs
// A proposal of the assistant read by the format (ADR 082): the state wanted with its sub-maps and the sheets of its steps, and a proposal opened as a new project with the sheets and translations of its steps of origin.

using Topogenia.Domain.Sheets;
using Topogenia.Interchange.Pmap;
using static Topogenia.Interchange.Tests.Pmap.PmapReaderTests;

namespace Topogenia.Interchange.Tests.Pmap;

public sealed class PmapProposalTests
{
    [Fact]
    public void The_state_wanted_holds_the_sub_maps_and_the_sheets_of_the_steps()
    {
        var format = new PmapFiles();
        var desired = format.Desired(format.Read(Doc($$"""
            { "lanes": [{{TwoLanes}}],
              "steps": [
                { "id": "e1", "lane": "c1", "label": "Saisir", "column": 0,
                  "sheet": { "purpose": "Payer", "outputs": [{ "id": "o1", "name": "Bon" }] } },
                { "id": "e2", "lane": "c1", "label": "Utiliser", "column": 1, "sheet": { "uses": [{ "document": "o1", "wait_hours": 2 }] } },
                { "id": "e3", "lane": "c2", "label": "Acheter", "column": 2,
                  "sub_map": { "lanes": [{ "id": "c3", "name": "Acheteur" }], "steps": [{ "id": "s1", "lane": "c3", "label": "Choisir" }] } }] }
            """), Languages, "ACI"));

        var first = desired.Steps[0].Sheet!;
        Assert.Equal("Payer", first.Input.Fields![SheetContentReader.PurposeKey]!.Text);
        Assert.Equal(("o1", "Bon"), (first.Outputs.Single().Id, first.Outputs[0].Input.Name));
        Assert.Equal(("o1", 2m), (desired.Steps[1].Sheet!.Uses.Single().Document, desired.Steps[1].Sheet!.Uses[0].Input.WaitHours));
        Assert.Null(desired.Steps[2].Sheet);
        var sub = desired.Steps[2].SubMap!;
        Assert.Equal(("s1", "c3", 0), (sub.Steps.Single().Id, sub.Steps[0].LaneId, sub.Steps[0].Column));
        Assert.Null(desired.Steps[0].SubMap);
    }

    /// <summary>
    /// A map of origin in two levels: ABC-E001 translated, with a sheet; ABC-E002 detailed by ABC-E003 (an output) and
    /// ABC-E004 (its use), and ABC-E005 that uses an output of ABC-E003 from another map once brought up.
    /// </summary>
    private static PmapDocument Origin() => Read(Doc("""
        { "lanes": [{ "id": "ABC-C01", "name": "Demandeur" }],
          "steps": [
            { "id": "ABC-E001", "lane": "ABC-C01", "label": "Saisir", "column": 0, "translations": { "en": "Enter" }, "sheet": { "purpose": "Payer" } },
            { "id": "ABC-E002", "lane": "ABC-C01", "label": "Valider", "column": 1,
              "sub_map": { "lanes": [{ "id": "ABC-C02", "name": "Responsable" }],
                "steps": [
                  { "id": "ABC-E003", "lane": "ABC-C02", "label": "Relire", "column": 0, "sheet": { "outputs": [{ "id": "ABC-E003-S1", "name": "Avis" }] } },
                  { "id": "ABC-E004", "lane": "ABC-C02", "label": "Signer", "column": 1, "translations": { "en": "Sign" },
                    "sheet": { "uses": [{ "document": "ABC-E003-S1" }] } }] } }] }
        """));

    [Fact]
    public void A_proposal_opened_as_a_new_project_carries_the_sheets_and_translations_of_its_steps_of_origin()
    {
        var proposal = Read(Doc("""
            { "lanes": [{ "id": "l1", "name": "Demandeur" }],
              "steps": [
                { "id": "ABC-E001", "lane": "l1", "label": "Saisir", "column": 0 },
                { "id": "ABC-E003", "lane": "l1", "label": "Relire", "column": 1 },
                { "id": "ABC-E004", "lane": "l1", "label": "Signer le bon", "column": 2 },
                { "id": "n1", "lane": "l1", "label": "Archiver", "column": 3, "sheet": { "purpose": "Garder" } }] }
            """));
        var carried = PmapCarry.Of(proposal, Origin()).Map.Steps.ToDictionary(step => step.Id);

        Assert.Equal(("Payer", "Enter"), (carried["ABC-E001"].Sheet!.Purpose, carried["ABC-E001"].Translations["en"]));
        Assert.Equal("ABC-E003-S1", carried["ABC-E003"].Sheet!.Outputs.Single().Id);
        // Brought up on one map, the use of ABC-E004 finds its output beside it; its label changed: no translation of origin.
        Assert.Equal("ABC-E003-S1", carried["ABC-E004"].Sheet!.Uses.Single().Document);
        Assert.Empty(carried["ABC-E004"].Translations);
        Assert.Equal("Garder", carried["n1"].Sheet!.Purpose);
    }

    [Fact]
    public void A_sheet_or_a_translation_the_proposal_gives_wins_and_a_use_whose_output_is_not_on_its_map_is_dropped()
    {
        var proposal = Read(Doc("""
            { "lanes": [{ "id": "l1", "name": "Demandeur" }],
              "steps": [
                { "id": "ABC-E001", "lane": "l1", "label": "Saisir", "column": 0, "translations": { "en": "Key in" }, "sheet": { "purpose": "Autre" } },
                { "id": "ABC-E004", "lane": "l1", "label": "Signer", "column": 1 }] }
            """));
        var carried = PmapCarry.Of(proposal, Origin()).Map.Steps.ToDictionary(step => step.Id);

        Assert.Equal(("Autre", "Key in"), (carried["ABC-E001"].Sheet!.Purpose, carried["ABC-E001"].Translations["en"]));
        Assert.Equal("Sign", carried["ABC-E004"].Translations["en"]);
        Assert.Empty(carried["ABC-E004"].Sheet!.Uses);
    }
}
