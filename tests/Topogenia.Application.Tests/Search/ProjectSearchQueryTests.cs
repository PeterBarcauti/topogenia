// ProjectSearchQueryTests.cs
// The search in the whole project (ADR 110): what is found on every level and in which order, the fold and the places, the languages, the tools under the rights of the sheets, the bounds and the refusals.

using System.Text.Json;
using Topogenia.Application.Ports;
using Topogenia.Application.Search;
using Topogenia.Application.Sheets;
using Topogenia.Application.Tests.Support;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;
using Topogenia.Domain.Sheets;
using Topogenia.Domain.Values;
using static Topogenia.Application.Tests.Search.SearchFixture;

namespace Topogenia.Application.Tests.Search;

public sealed class ProjectSearchQueryTests
{
    [Fact]
    public async Task A_word_finds_steps_by_name_and_code_lanes_documents_tools_pain_points_and_actions_on_every_level()
    {
        var project = new TestProject();
        RenameLane(project, project.LaneId, "Service facturation");
        var invoice = Named(project, project.Step(0), "Facturer le client");
        var pay = Named(project, project.Step(1), "Payer", "FACT-01");
        Sheets.SheetFixture.Flow(project, invoice, pay, "Facture");
        await project.Runner.RunAsync(new SaveSheetCommand(pay, Sheets.SheetFixture.Purpose("Facture à payer")), Token);
        var (detail, detailLane) = project.SubMap(invoice);
        RenameMap(project, detail, "Factures détaillées");
        var check = Named(project, project.Step(0, laneId: detailLane, mapId: detail), "Contrôler", "PRJ-E010");
        AddDocument(project, check, "Facture fournisseur");
        await DraftAsync(project, check, "Logiciel de facturation", "Outil de facture", "Tableur");
        var (deeper, deeperLane) = project.SubMap(check);
        var archive = Named(project, project.Step(0, laneId: deeperLane, mapId: deeper), "Archiver", "PRJ-E020");
        AddPainPoint(project, archive, "PRJ-I001", "Facture perdue");
        AddPainPoint(project, archive, "PRJ-I002", "Retard", PainPointStatus.Implemented, "Relancer la facturation", "Service facture");
        AddPainPoint(project, archive, "PRJ-I003", "Lenteur", owner: "Facturier");

        var view = await SearchAsync(project, "fact");

        Assert.Equal(
            [(1, SearchKind.Lane, "Service facturation"), (1, SearchKind.Step, "Facturer le client"), (1, SearchKind.Step, "FACT-01"),
             (2, SearchKind.Document, "Facture fournisseur"), (2, SearchKind.Tool, "Logiciel de facturation"), (2, SearchKind.Tool, "Outil de facture"),
             (3, SearchKind.PainPoint, "Facture perdue"), (3, SearchKind.Action, "Relancer la facturation")],
            Found(view));
        Assert.Equal(8, view.Total);
        Assert.Equal([project.RootMapId, detail, deeper], view.Maps.Select(map => map.MapId));
        Assert.Equal("Factures détaillées", view.Maps[1].Name.Text);
        var hits = Hits(view);
        Assert.Equal((null, project.LaneId, "fr"), (hits[0].StepId, hits[0].LaneId, hits[0].Language));
        Assert.Equal((invoice, "fr", 0, 4), (hits[1].StepId!.Value, hits[1].Language, hits[1].Start, hits[1].Length));
        Assert.Equal((pay, (string?)null, 0, 4), (hits[2].StepId!.Value, hits[2].Language, hits[2].Start, hits[2].Length));
        Assert.All(hits.Take(3), hit => Assert.Null(hit.Step));
        Assert.Equal((check, "PRJ-E010", "Contrôler", "fr"), (hits[3].StepId!.Value, hits[3].StepCode, hits[3].Step!.Text, hits[3].Language));
        Assert.Equal(("PRJ-E010", 12), (hits[4].StepCode, hits[4].Start));
        Assert.Equal((archive, "PRJ-E020", PainPointStatus.Reported), (hits[6].StepId!.Value, hits[6].StepCode, hits[6].Status!.Value));
        Assert.Equal((PainPointStatus.Implemented, 12, 4), (hits[7].Status!.Value, hits[7].Start, hits[7].Length));
    }

    [Fact]
    public async Task The_map_asked_comes_first_then_the_maps_below_the_root_level_by_level_each_in_reading_order()
    {
        var project = new TestProject();
        RenameLane(project, project.LaneId, "Lot A");
        var second = AddLane(project, project.RootMapId, "Lot B");
        var first = Named(project, project.Step(0), "Lot 1");
        var later = Named(project, project.Step(1), "Lot 2");
        Named(project, project.Step(0, laneId: second), "Lot 3");
        var (left, leftLane) = project.SubMap(first);
        var (right, rightLane) = project.SubMap(later);
        var below = Named(project, project.Step(0, laneId: leftLane, mapId: left), "Lot 11");
        Named(project, project.Step(0, laneId: rightLane, mapId: right), "Lot 21");
        var (deepest, deepestLane) = project.SubMap(below);
        Named(project, project.Step(0, laneId: deepestLane, mapId: deepest), "Lot 111");
        Guid[] subtreeOrder = [project.RootMapId, left, right, deepest];

        var plain = await SearchAsync(project, "lot");
        Assert.Equal(subtreeOrder, plain.Maps.Select(map => map.MapId));
        Assert.Equal(["Lot A", "Lot B", "Lot 1", "Lot 3", "Lot 2"], plain.Maps[0].Hits.Select(hit => hit.Text));
        Assert.Equal([1, 2, 2, 3], plain.Maps.Select(map => map.Depth));
        var asked = await SearchAsync(project, "lot", mapId: right);
        Assert.Equal([right, .. subtreeOrder.Where(id => id != right)], asked.Maps.Select(map => map.MapId));
        Assert.Equal(["Lot 21"], asked.Maps[0].Hits.Select(hit => hit.Text));
        var deep = await SearchAsync(project, "lot", mapId: deepest);
        Assert.Equal([deepest, .. subtreeOrder.Where(id => id != deepest)], deep.Maps.Select(map => map.MapId));
        var unknown = await SearchAsync(project, "lot", mapId: project.NextId());
        Assert.Equal(subtreeOrder, unknown.Maps.Select(map => map.MapId));
        Assert.Equal(8, unknown.Total);
    }

    [Fact]
    public async Task Accents_case_and_ligatures_do_not_matter_and_the_place_is_given_in_the_original_text()
    {
        var project = new TestProject();
        RenameLane(project, project.LaneId, "Straße du Nord");
        var step = Named(project, project.Step(0), "Prévenir l’équipe");
        AddDocument(project, step, "Cœur de métier", 0);
        AddDocument(project, step, "Équipe mobile", 1);

        var team = Hits(await SearchAsync(project, "EQUIPE"));
        Assert.Equal([("Prévenir l’équipe", 11, 6), ("Équipe mobile", 0, 7)], team.Select(hit => (hit.Text, hit.Start, hit.Length)));
        var heart = Assert.Single(Hits(await SearchAsync(project, "coeur")));
        Assert.Equal((SearchKind.Document, 0, 4), (heart.Kind, heart.Start, heart.Length));
        var north = Assert.Single(Hits(await SearchAsync(project, "nord")));
        Assert.Equal((SearchKind.Lane, 10, 4), (north.Kind, north.Start, north.Length));
        var street = Assert.Single(Hits(await SearchAsync(project, "  STRASSE ")));
        Assert.Equal((0, 6), (street.Start, street.Length));
        Assert.Empty(Hits(await SearchAsync(project, "strase")));
    }

    [Fact]
    public async Task Several_words_are_found_together_in_one_text_never_spread_over_a_name_a_code_a_document_or_two_tools()
    {
        var project = new TestProject();
        var site = Named(project, project.Step(0), "Site web Livres à jour", "WEB-E010");
        AddDocument(project, site, "Catalogue mobile");
        await DraftAsync(project, site, "Tableur", "Logiciel de livres");

        var both = await SearchAsync(project, "livre web");
        Assert.Equal([(1, SearchKind.Step, "Site web Livres à jour")], Found(both));
        Assert.Equal((5, 3), (Hits(both)[0].Start, Hits(both)[0].Length));
        // Each word found is marked (R9b): « web » and « Livres », in the order of the text; the place is the first mark.
        Assert.Equal([new TextMatch(5, 3), new TextMatch(9, 5)], Hits(both)[0].Marks);
        Assert.Equal(JsonSerializer.Serialize(both), JsonSerializer.Serialize(await SearchAsync(project, "WEB  Livre")));
        // Marks that overlap are merged, never drawn twice: « site sit » marks « Site » once.
        Assert.Equal([new TextMatch(0, 4)], Assert.Single(Hits(await SearchAsync(project, "site sit"))).Marks);
        // A word alone has one mark, its place.
        Assert.All(Hits(await SearchAsync(project, "livre")), hit => Assert.Equal([new TextMatch(hit.Start, hit.Length)], hit.Marks));
        Assert.Equal([(1, SearchKind.Tool, "Logiciel de livres")], Found(await SearchAsync(project, "livres logiciel")));
        foreach (var spread in new[] { "livre mobile", "catalogue site", "e010 site", "tableur logiciel", "livre catalogue" })
            Assert.Equal(0, (await SearchAsync(project, spread)).Total);
        Assert.Equal(2, (await SearchAsync(project, "livre")).Total);
    }

    [Fact]
    public async Task The_language_shown_and_the_language_of_origin_are_searched_and_no_other()
    {
        var project = new TestProject();
        RenameLane(project, project.LaneId, "Comptabilité");
        Translate(project, TranslationObject.Lane, project.LaneId, "en", "Accounting");
        Translate(project, TranslationObject.Map, project.RootMapId, "en", "Purchasing");
        Translate(project, TranslationObject.Map, project.RootMapId, "de", "Einkauf");
        var invoice = Named(project, project.Step(0), "Facturer");
        Translate(project, TranslationObject.Step, invoice, "en", "Invoice");
        Translate(project, TranslationObject.Step, invoice, "de", "Rechnung");
        var factoring = Named(project, project.Step(1), "Facturation");
        Translate(project, TranslationObject.Step, factoring, "en", "Factoring");
        AddDocument(project, invoice, "Bon de commande");

        var shown = Assert.Single(Hits(await SearchAsync(project, "invoice", "en")));
        Assert.Equal((SearchKind.Step, "Invoice", "en"), (shown.Kind, shown.Text, shown.Language));
        var origin = Assert.Single(Hits(await SearchAsync(project, "facturer", "en")));
        Assert.Equal(("Facturer", "fr"), (origin.Text, origin.Language));
        Assert.Empty(Hits(await SearchAsync(project, "rechnung", "en")));
        Assert.Empty(Hits(await SearchAsync(project, "invoice")));
        Assert.Equal("de", Assert.Single(Hits(await SearchAsync(project, "rechnung", "de"))).Language);
        Assert.Equal("Accounting", Assert.Single(Hits(await SearchAsync(project, "accounting", "en"))).Text);
        Assert.Equal([("Facturer", "fr"), ("Factoring", "en")], Hits(await SearchAsync(project, "fact", "en")).Select(hit => (hit.Text, hit.Language)));
        var view = await SearchAsync(project, "commande", "en");
        var document = Assert.Single(Hits(view));
        Assert.Equal(("Facturer", "fr"), (document.Step!.Text, document.Step.SourceLanguage));
        Assert.Equal(new Dictionary<string, string> { ["en"] = "Invoice" }, document.Step.Translations);
        Assert.Equal(new Dictionary<string, string> { ["en"] = "Purchasing" }, view.Maps[0].Name.Translations);
        Assert.Empty((await SearchAsync(project, "commande")).Maps[0].Name.Translations);
    }

    [Fact]
    public async Task A_reader_finds_the_tools_of_the_validated_version_never_those_of_a_draft()
    {
        var project = new TestProject();
        var validated = project.Step(0);
        await DraftAsync(project, validated, "Tableur", "Scanner");
        await ValidateAsync(project, validated);
        await DraftAsync(project, validated, "Logiciel", "Messagerie");
        var never = project.Step(1);
        await DraftAsync(project, never, "Logiciel");

        project.ActingAs(project.Reader);
        var spreadsheet = Assert.Single(Hits(await SearchAsync(project, "tableur")));
        Assert.Equal((SearchKind.Tool, validated, "Tableur"), (spreadsheet.Kind, spreadsheet.StepId!.Value, spreadsheet.Text));
        Assert.Equal("Scanner", Assert.Single(Hits(await SearchAsync(project, "scanner"))).Text);
        Assert.Empty(Hits(await SearchAsync(project, "logiciel")));
        Assert.Empty(Hits(await SearchAsync(project, "messagerie")));
        project.ActingAs(project.Facilitator);
        Assert.Equal([validated, never], Hits(await SearchAsync(project, "logiciel")).Select(hit => hit.StepId!.Value));
        Assert.Empty(Hits(await SearchAsync(project, "tableur")));
    }

    [Fact]
    public async Task A_contributor_finds_the_draft_tools_of_the_steps_he_may_fill_and_the_validated_ones_elsewhere()
    {
        var project = new TestProject();
        project.Database.Add(project.Database.Get<Lane>(Lane.KeyOf(project.LaneId)) with { ActorUserId = project.Contributor.Id });
        var other = AddLane(project, project.RootMapId, "Comptabilité");
        var assigned = project.Step(0, assignee: project.Contributor.Id, laneId: other);
        var held = project.Step(1);
        var elsewhere = project.Step(2, laneId: other);
        foreach (var step in new[] { assigned, held, elsewhere })
            await ValidatedThenDraftAsync(project, step, "Ancien outil", "Nouvel outil");

        project.ActingAs(project.Contributor);
        Assert.Equal([assigned, held], Hits(await SearchAsync(project, "nouvel")).Select(hit => hit.StepId!.Value));
        Assert.Equal([elsewhere], Hits(await SearchAsync(project, "ancien")).Select(hit => hit.StepId!.Value));
        project.ActingAs(project.Reader);
        Assert.Equal([assigned, held, elsewhere], Hits(await SearchAsync(project, "ancien")).Select(hit => hit.StepId!.Value));
        Assert.Empty(Hits(await SearchAsync(project, "nouvel")));
    }

    [Fact]
    public async Task The_facilitator_and_the_administrator_find_the_tools_of_every_draft()
    {
        var project = new TestProject();
        var other = AddLane(project, project.RootMapId, "Comptabilité");
        var assigned = project.Step(0, assignee: project.Contributor.Id);
        var free = project.Step(1, laneId: other);
        await DraftAsync(project, assigned, "Logiciel");
        await ValidatedThenDraftAsync(project, free, "Tableur", "Logiciel comptable");

        foreach (var user in new[] { project.Facilitator, project.Admin })
        {
            project.ActingAs(user);
            Assert.Equal([assigned, free], Hits(await SearchAsync(project, "logiciel")).Select(hit => hit.StepId!.Value));
            Assert.Empty(Hits(await SearchAsync(project, "tableur")));
        }
        project.ActingAs(project.Reader);
        Assert.Empty(Hits(await SearchAsync(project, "logiciel")));
    }

    [Fact]
    public async Task A_step_without_content_has_no_tool_whatever_its_former_sheet_held()
    {
        var project = new TestProject();
        var start = Named(project, project.Step(0), "Début du tableur");
        await ValidatedThenDraftAsync(project, start, "Tableur", "Tableur neuf");
        var decision = project.Step(1);
        await DraftAsync(project, decision, "Tableur de décision");
        await ValidateAsync(project, decision);
        Retype(project, start, StepType.Start);
        Retype(project, decision, StepType.Decision);

        foreach (var user in new[] { project.Facilitator, project.Reader })
        {
            project.ActingAs(user);
            Assert.Equal([(SearchKind.Step, "Début du tableur"), (SearchKind.Tool, "Tableur de décision")],
                Hits(await SearchAsync(project, "tableur")).Select(hit => (hit.Kind, hit.Text)));
        }
        foreach (var type in new[] { StepType.End, StepType.Parallel, StepType.Inclusive })
        {
            Retype(project, decision, type);
            Assert.Equal([SearchKind.Step], Hits(await SearchAsync(project, "tableur")).Select(hit => hit.Kind));
        }
    }

    [Fact]
    public async Task Deleted_steps_maps_out_of_the_tree_and_pain_points_without_step_are_not_found()
    {
        var project = new TestProject();
        Named(project, project.Step(0), "Facture A");
        var gone = Named(project, project.Step(1), "Facture B");
        AddDocument(project, gone, "Facture du B");
        AddPainPoint(project, gone, "PRJ-I001", "Facture du B perdue");
        var (orphan, orphanLane) = project.SubMap(gone);
        Named(project, project.Step(0, laneId: orphanLane, mapId: orphan), "Facture sous B");
        var kept = Named(project, project.Step(2), "Contrôler");
        var (dropped, droppedLane) = project.SubMap(kept);
        Named(project, project.Step(0, laneId: droppedLane, mapId: dropped), "Facture sous C");
        DeleteMap(project, dropped);
        DeleteStep(project, gone);
        AddPainPoint(project, null, "PRJ-I002", "Facture sans étape");
        var lane = AddLane(project, project.RootMapId, "Facture couloir");
        project.Database.Add(project.Database.Get<Lane>(Lane.KeyOf(lane)) with { DeletedAt = TestProject.Now, DeletionBatchId = project.NextId() });

        foreach (Guid? asked in new Guid?[] { null, dropped, orphan })
        {
            var view = await SearchAsync(project, "facture", mapId: asked);
            Assert.Equal([(1, SearchKind.Step, "Facture A")], Found(view));
            Assert.Equal(1, view.Total);
        }
    }

    [Fact]
    public async Task An_empty_project_and_a_word_found_nowhere_give_no_result()
    {
        var empty = new TestProject();
        var nothing = await SearchAsync(empty, "facture");
        Assert.Equal((0, 0), (nothing.Total, nothing.Maps.Count));
        var project = new TestProject();
        Named(project, project.Step(0), "Facturer");
        Assert.Empty((await SearchAsync(project, "zzz")).Maps);
        var mark = await SearchAsync(project, "́");
        Assert.Equal((0, 0), (mark.Total, mark.Maps.Count));
    }

    [Fact]
    public async Task A_project_of_five_thousand_steps_of_ten_versions_gives_two_hundred_results_at_most_with_the_total()
    {
        var project = new TestProject();
        var older = SheetJson.Freeze(new SheetContent(null, "Traiter", "Registre papier", null, null, [new ProcedureTask("Saisir", "Registre", null)], null));
        var latest = SheetJson.Freeze(new SheetContent(null, "Traiter", "Classeur", null, null, [new ProcedureTask("Saisir", "Classeur", null)], null));
        for (var column = 0; column < 5000; column++)
        {
            var step = project.Step(column);
            var sheet = project.NextId();
            project.Database.Add(new StepSheet(sheet, step, SheetStatus.Draft, null, null, "Tableur", null, null, null, "fr", null,
                TestProject.Now, project.Facilitator.Id, null, null, 1));
            // The latest version written first: the order of the records does not make the latest one.
            foreach (var number in new[] { 10, 1, 2, 3, 4, 5, 6, 7, 8, 9 })
                project.Database.Add(new StepSheetVersion(project.NextId(), sheet, number, number == 10 ? latest : older, null, null,
                    project.Facilitator.Id, TestProject.Now));
        }

        var steps = await SearchAsync(project, "etape");
        Assert.Equal(5000, steps.Total);
        var hits = Hits(steps);
        Assert.Equal(ProjectSearchQuery.HitsMax, hits.Count);
        Assert.Equal(("Étape 0", "Étape 199"), (hits[0].Text, hits[^1].Text));
        var tools = await SearchAsync(project, "tableur");
        Assert.Equal((5000, 200), (tools.Total, Hits(tools).Count));
        Assert.All(Hits(tools), hit => Assert.Equal(SearchKind.Tool, hit.Kind));
        Assert.Equal(0, (await SearchAsync(project, "registre")).Total);
        project.ActingAs(project.Reader);
        Assert.Equal(0, (await SearchAsync(project, "tableur")).Total);
        var validated = await SearchAsync(project, "classeur");
        Assert.Equal((5000, 200), (validated.Total, Hits(validated).Count));
        Assert.Equal(0, (await SearchAsync(project, "registre")).Total);
    }

    [Fact]
    public async Task The_search_loads_once_writes_nothing_and_logs_nothing()
    {
        var project = new TestProject();
        var step = Named(project, project.Step(0), "Facturer");
        await ValidatedThenDraftAsync(project, step, "Tableur", "Logiciel");
        var calls = project.Database.Calls.Count;
        var commits = project.Database.Commits;
        var entries = project.Database.Entries.Count;
        var records = project.Database.All<object>().Count();

        project.ActingAs(project.Reader);
        Assert.Equal(2, (await SearchAsync(project, "facturer")).Total + (await SearchAsync(project, "tableur")).Total);
        Assert.Equal(["access", "load", "access", "load"], project.Database.Calls.Skip(calls));
        Assert.Equal((commits, entries, records), (project.Database.Commits, project.Database.Entries.Count, project.Database.All<object>().Count()));
        Assert.Equal(WorkspaceContent.Sheets | WorkspaceContent.Documents | WorkspaceContent.Translations, project.Database.LoadedContents[^1]);
        Assert.False(new ProjectSearchQuery(project.ProjectId, "x", null, null).ReadsMapView);
    }

    [Fact]
    public async Task A_refused_text_or_language_loads_nothing_after_the_right()
    {
        var project = new TestProject();
        project.ActingAs(project.Reader);
        foreach (var (text, language) in new (string?, string?)[] { (null, null), ("", null), ("   ", null), (new string('a', 101), null),
            ("facture", "EN"), ("facture", new string('a', 17)) })
        {
            var calls = project.Database.Calls.Count;
            await Assert.ThrowsAsync<DomainException>(() => SearchAsync(project, text, language));
            Assert.Equal(["access"], project.Database.Calls.Skip(calls));
        }
        var accepted = project.Database.Calls.Count;
        Assert.Equal(0, (await SearchAsync(project, "facture", "en")).Total);
        Assert.Equal(["access", "load"], project.Database.Calls.Skip(accepted));
    }

    [Fact]
    public async Task A_text_empty_or_longer_than_100_characters_and_a_language_out_of_its_form_are_refused()
    {
        var project = new TestProject();
        project.ActingAs(project.Reader);
        foreach (var text in new[] { null, "", "   ", new string('a', 101) })
        {
            var refusal = await Assert.ThrowsAsync<DomainException>(() => SearchAsync(project, text));
            Assert.Equal((ErrorCodes.InvalidValue, "text"), (refusal.Code, refusal.Details["field"]));
        }
        Assert.Equal(0, (await SearchAsync(project, new string('a', 100))).Total);
        foreach (var language in new[] { "", "EN", "fr_FR", "e n", "é", new string('a', 17) })
        {
            var refusal = await Assert.ThrowsAsync<DomainException>(() => SearchAsync(project, "facture", language));
            Assert.Equal((ErrorCodes.InvalidValue, "language"), (refusal.Code, refusal.Details["field"]));
        }
        Assert.Equal(0, (await SearchAsync(project, "facture", "pt-br")).Total);
        Assert.Equal(0, (await SearchAsync(project, "facture", new string('a', 16))).Total);
        project.ActingAs(project.Stranger);
        Assert.Equal(ErrorCodes.NotFound, (await Assert.ThrowsAsync<DomainException>(() => SearchAsync(project, ""))).Code);
        project.ActingAs(project.Reader);
        project.PutInTrash();
        Assert.Equal(ErrorCodes.NotFound, (await Assert.ThrowsAsync<DomainException>(() => SearchAsync(project, "facture"))).Code);
    }
}
