// PmapInsertionTests.cs
// A .pmap file inserted as a detailed step of the map open, or replacing the detail of a step, through the whole host on both databases (3.3, F-208): its preview, its copy whole in one operation undone by one undo, its sheets on option, its languages, its depth, a stale preview, the rights.

using System.Text.Json;
using Topogenia.Application.Audit;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Host.Tests.Steps;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Exchange;

public sealed class PmapInsertionTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    /// <summary>
    /// A sub-process of two levels described in another project: a coloured lane, a RACI, a comment of presentation, a
    /// sheet whose output the next step uses, a detail under its second step, a translation in English (configured) and
    /// one in Esperanto (not configured), and a point to check.
    /// </summary>
    private const string Achats = """
        {
          "format": "topogenia",
          "version": "1.5",
          "project": { "code": "ACHA", "name": "Achats", "translations": { "en": "Purchasing", "eo": "Aĉetado" } },
          "map": {
            "lanes": [{ "id": "c1", "name": "Demandeur", "color": "#E3F2FD" }, { "id": "c2", "name": "Acheteur", "type": "department" }],
            "steps": [
              { "id": "e1", "lane": "c1", "label": "Saisir la demande", "column": 0, "raci": { "c2": "C" },
                "translations": { "en": "Enter the request", "eo": "Enigi la peton" },
                "comment": { "text": "Tout part d'ici.", "translations": { "en": "It all starts here.", "eo": "Ĉio komenciĝas ĉi tie." } },
                "sheet": { "purpose": "Exprimer le besoin", "outputs": [{ "id": "d1", "name": "Demande d'achat" }] } },
              { "id": "e2", "lane": "c2", "label": "Passer la commande", "column": 1,
                "sheet": { "uses": [{ "document": "d1", "wait_hours": 8 }] },
                "sub_map": {
                  "lanes": [{ "id": "c3", "name": "Acheteur" }],
                  "steps": [{ "id": "e3", "lane": "c3", "label": "Choisir le fournisseur", "column": 0 }, { "id": "e4", "lane": "c3", "label": "Envoyer le bon", "column": 1 }],
                  "flows": [{ "source": "e3", "target": "e4" }]
                } }
            ],
            "flows": [{ "source": "e1", "target": "e2", "label": "valide" }]
          },
          "uncertainties": ["Le délai est à confirmer."]
        }
        """;

    /// <summary>A newer version of the same sub-process: one lane, one step.</summary>
    private const string AchatsV2 = """
        {
          "format": "topogenia",
          "version": "1.1",
          "project": { "code": "ACHA", "name": "Achats" },
          "map": { "lanes": [{ "id": "c1", "name": "Acheteur" }], "steps": [{ "id": "e1", "lane": "c1", "label": "Commander en ligne", "column": 0 }] }
        }
        """;

    private static string Insert(ApiProject project) => $"/api/maps/{project.RootMapId}/insert-pmap";

    private static string[] Labels(JsonElement map) => [.. map.GetProperty("steps").EnumerateArray().Select(step => step.GetProperty("label").GetString()!).Order()];

    private static JsonElement StepOf(JsonElement map, string label) =>
        map.GetProperty("steps").EnumerateArray().Single(step => step.GetProperty("label").GetString() == label);

    private static async Task<JsonElement> InsertedAsync(ApiProject project, ApiClient facilitator, string content, bool withSheets, string? label = null)
    {
        var preview = (await facilitator.PostAsync($"{Insert(project)}/preview", new { content })).Ok();
        return (await facilitator.PostAsync(Insert(project), new { content, lane_id = project.FirstLaneId, column = 0, row = 0, label, with_sheets = withSheets },
            preview.GetProperty("structure_version").GetInt32())).Ok();
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_file_is_previewed_then_copied_whole_as_a_detailed_step_and_undone_by_one_undo(EngineKind kind)
    {
        var host = await hosts.OfAsync(kind);
        var project = await ApiProject.CreateAsync(host);
        using var facilitator = project.Facilitator;
        (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/steps", new { lane_id = project.FirstLaneId, column = 0, label = "Recevoir le besoin" })).Ok();

        var preview = (await facilitator.PostAsync($"{Insert(project)}/preview", new { content = Achats })).Ok();
        Assert.Equal(("ACHA", "Achats", "fr", 2, 4, 2, 2), (preview.GetProperty("project_code").GetString(), preview.GetProperty("label").GetString(),
            preview.GetProperty("language").GetString(), preview.GetProperty("maps").GetInt32(), preview.GetProperty("steps").GetInt32(),
            preview.GetProperty("depth").GetInt32(), preview.GetProperty("sheets").GetInt32()));
        Assert.Equal(11, preview.GetProperty("room").GetInt32());
        Assert.Equal(["eo"], preview.GetProperty("dropped_languages").EnumerateArray().Select(language => language.GetString()));
        Assert.Equal(["Le délai est à confirmer."], preview.GetProperty("uncertainties").EnumerateArray().Select(text => text.GetString()));
        // The preview wrote nothing.
        Assert.Equal(["Recevoir le besoin"], Labels(await project.MapAsync()));

        var reply = (await facilitator.PostAsync(Insert(project), new { content = Achats, lane_id = project.FirstLaneId, column = 0, row = 0, with_sheets = true },
            preview.GetProperty("structure_version").GetInt32())).Ok();
        Assert.Equal("inserer_fichier", reply.GetProperty("history").GetProperty("undo")[0].GetProperty("kind").GetString());
        var root = reply.GetProperty("map");
        // The new step takes the cell chosen, the step that was there moved on (RG-C04), under the name of the file and its translation.
        Assert.Equal(["Achats", "Recevoir le besoin"], Labels(root));
        var inserted = StepOf(root, "Achats");
        Assert.Equal(0, inserted.GetProperty("column").GetInt32());
        Assert.Equal("Purchasing", inserted.GetProperty("translations").GetProperty("en").GetString());
        Assert.False(inserted.GetProperty("translations").TryGetProperty("eo", out _));
        var detailId = inserted.GetProperty("sub_map_id").GetGuid();
        Assert.Equal(detailId, reply.GetProperty("ids")[0].GetGuid());

        var detail = await project.MapAsync(detailId);
        Assert.Equal("Achats", detail.GetProperty("name").GetString());
        Assert.Equal(["Saisir la demande", "Passer la commande"], detail.GetProperty("steps").EnumerateArray().OrderBy(step => step.GetProperty("column").GetInt32())
            .Select(step => step.GetProperty("label").GetString()));
        Assert.Equal([("Demandeur", "#E3F2FD"), ("Acheteur", detail.GetProperty("lanes")[1].GetProperty("color").GetString())],
            detail.GetProperty("lanes").EnumerateArray().Select(lane => (lane.GetProperty("name").GetString(), lane.GetProperty("color").GetString())));
        var first = StepOf(detail, "Saisir la demande");
        Assert.Equal("Enter the request", first.GetProperty("translations").GetProperty("en").GetString());
        Assert.False(first.GetProperty("translations").TryGetProperty("eo", out _));
        Assert.Equal("draft", first.GetProperty("sheet").GetProperty("status").GetString());
        Assert.Single(detail.GetProperty("raci").EnumerateArray());
        Assert.Equal("valide", Assert.Single(detail.GetProperty("flows").EnumerateArray()).GetProperty("label").GetString());
        var lower = await project.MapAsync(StepOf(detail, "Passer la commande").GetProperty("sub_map_id").GetGuid());
        Assert.Equal(["Choisir le fournisseur", "Envoyer le bon"], Labels(lower));

        // The comment of presentation follows, in the configured languages only.
        var comments = (await facilitator.GetAsync($"/api/maps/{detailId}/presentation-comments")).Ok();
        var lines = comments.EnumerateArray().Select(line => (line.GetProperty("language").GetString(), line.GetProperty("text").GetString())).Order().ToList();
        Assert.Equal([("en", "It all starts here."), ("fr", "Tout part d'ici.")], lines);

        (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/undo")).Ok();
        Assert.Equal(["Recevoir le besoin"], Labels(await project.MapAsync()));
        Assert.Equal(0, StepOf(await project.MapAsync(), "Recevoir le besoin").GetProperty("column").GetInt32());
        (await facilitator.GetAsync($"/api/maps/{detailId}")).Refused(404, "INTROUVABLE");
    }

    /// <summary>A sub-process written in English, its summary translated into French, a document and a pain point in its sheets.</summary>
    private const string Purchasing = """
        {
          "format": "topogenia",
          "version": "1.5",
          "project": { "code": "BUY", "name": "Purchasing", "language": "en" },
          "map": {
            "summary": { "text": "The request goes to the buyer.", "translations": { "fr": "La demande va à l'acheteur." } },
            "lanes": [{ "id": "c1", "name": "Requester" }],
            "steps": [
              { "id": "e1", "lane": "c1", "label": "Enter the request", "column": 0, "sheet": { "purpose": "State the need", "outputs": [{ "id": "d1", "name": "Purchase request" }] } },
              { "id": "e2", "lane": "c1", "label": "Order", "column": 1, "sheet": { "uses": [{ "document": "d1", "pain_point": "Incomplete" }] } }
            ],
            "flows": [{ "source": "e1", "target": "e2" }]
          }
        }
        """;

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_file_in_another_language_than_the_project_keeps_its_language_on_its_summary_documents_and_pain_points_and_the_translation_of_its_summary(EngineKind kind)
    {
        // Audit of the copy 64, P1-04: the rules of the core write the language of the project (French here); the texts of
        // the file are English, and its French summary is a translation, not lost.
        var host = await hosts.OfAsync(kind);
        var project = await ApiProject.CreateAsync(host);
        using var facilitator = project.Facilitator;
        var root = (await InsertedAsync(project, facilitator, Purchasing, withSheets: true)).GetProperty("map");
        var detail = await project.MapAsync(StepOf(root, "Purchasing").GetProperty("sub_map_id").GetGuid());
        var summary = detail.GetProperty("summary");
        Assert.Equal(("The request goes to the buyer.", "en", "La demande va à l'acheteur."),
            (summary.GetProperty("text").GetString(), summary.GetProperty("source_language").GetString(), summary.GetProperty("translations").GetProperty("fr").GetString()));
        var sheets = new List<JsonElement>();
        foreach (var label in new[] { "Enter the request", "Order" })
            sheets.Add((await facilitator.GetAsync($"/api/steps/{StepOf(detail, label).GetProperty("id").GetGuid()}/sheet")).Ok());
        var documents = sheets.SelectMany(sheet => sheet.GetProperty("outputs").EnumerateArray().Select(output => output.GetProperty("document"))).ToList();
        var pains = sheets.SelectMany(sheet => sheet.GetProperty("pain_points").EnumerateArray()).ToList();
        Assert.Equal([("Purchase request", "en")], documents.Select(document => (document.GetProperty("name").GetString(), document.GetProperty("source_language").GetString())));
        Assert.Equal([("Incomplete", "en")], pains.Select(pain => (pain.GetProperty("description").GetString(), pain.GetProperty("source_language").GetString())).Distinct());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_label_proposed_from_a_long_name_is_cut_before_a_character_never_inside_it(EngineKind kind)
    {
        // Audit of the copy 64, P2-06: a name of 199 characters (a letter, then 99 emojis of two halves each) cut to the
        // 150 of a label would end on the first half of an emoji; it ends before it.
        var host = await hosts.OfAsync(kind);
        var project = await ApiProject.CreateAsync(host);
        using var facilitator = project.Facilitator;
        var name = "a" + string.Concat(Enumerable.Repeat("\U0001F4E6", 99));
        var content = AchatsV2.Replace("\"name\": \"Achats\"", $"\"name\": \"{name}\"", StringComparison.Ordinal);
        var label = (await facilitator.PostAsync($"{Insert(project)}/preview", new { content })).Ok().GetProperty("label").GetString()!;
        Assert.Equal(name[..149], label);
        var inserted = (await facilitator.PostAsync(Insert(project), new { content, lane_id = project.FirstLaneId, column = 0, row = 0, with_sheets = false },
            (await facilitator.PostAsync($"{Insert(project)}/preview", new { content })).Ok().GetProperty("structure_version").GetInt32())).Ok();
        Assert.Equal(name[..149], Assert.Single(inserted.GetProperty("map").GetProperty("steps").EnumerateArray()).GetProperty("label").GetString());
    }

    /// <summary>The flows of a map as « source > target : label », by the labels of their steps, in order.</summary>
    private static string[] Flows(JsonElement map)
    {
        var labels = map.GetProperty("steps").EnumerateArray().ToDictionary(step => step.GetProperty("id").GetGuid(), step => step.GetProperty("label").GetString());
        return [.. map.GetProperty("flows").EnumerateArray().Select(flow =>
            $"{labels[flow.GetProperty("source_step_id").GetGuid()]} > {labels[flow.GetProperty("target_step_id").GetGuid()]} : {flow.GetProperty("label").GetString()}").Order()];
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Inserted_after_or_before_a_step_the_file_cuts_its_flow_in_two_as_inserting_a_step_does_and_one_undo_joins_it_again(EngineKind kind)
    {
        // Decision of the owner of the project of 17/11/2026: the practice of Visio, Bizagi, Signavio and bpmn.io.
        var host = await hosts.OfAsync(kind);
        var project = await ApiProject.CreateAsync(host);
        using var facilitator = project.Facilitator;
        var url = $"/api/maps/{project.RootMapId}/steps";
        var first = (await facilitator.PostAsync(url, new { lane_id = project.FirstLaneId, column = 0, label = "Saisir" })).Ok().GetProperty("id").GetGuid();
        var second = (await facilitator.PostAsync(url, new { lane_id = project.FirstLaneId, column = 1, label = "Envoyer" })).Ok().GetProperty("id").GetGuid();
        (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/flows", new { source_step_id = first, target_step_id = second, label = "complet" })).Ok();
        var other = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/lanes", new { name = "Achats" })).Ok().GetProperty("id").GetGuid();

        var preview = (await facilitator.PostAsync($"{Insert(project)}/preview", new { content = AchatsV2 })).Ok();
        var reply = (await facilitator.PostAsync(Insert(project), new { content = AchatsV2, lane_id = other, anchor_step_id = first, position = "after" },
            preview.GetProperty("structure_version").GetInt32())).Ok();
        var map = reply.GetProperty("map");
        // The label stays on the side of its origin; the new step in the lane chosen, one column after the step it follows.
        Assert.Equal(["Achats > Envoyer : ", "Saisir > Achats : complet"], Flows(map));
        var inserted = StepOf(map, "Achats");
        Assert.Equal((other, 1), (inserted.GetProperty("lane_id").GetGuid(), inserted.GetProperty("column").GetInt32()));
        Assert.Equal(2, StepOf(map, "Envoyer").GetProperty("column").GetInt32());

        preview = (await facilitator.PostAsync($"{Insert(project)}/preview", new { content = AchatsV2 })).Ok();
        reply = (await facilitator.PostAsync(Insert(project), new { content = AchatsV2, lane_id = project.FirstLaneId, anchor_step_id = first, position = "before", label = "Amont" },
            preview.GetProperty("structure_version").GetInt32())).Ok();
        Assert.Contains("Amont > Saisir : ", Flows(reply.GetProperty("map")));
        Assert.Equal(0, StepOf(reply.GetProperty("map"), "Amont").GetProperty("column").GetInt32());

        (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/undo")).Ok();
        (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/undo")).Ok();
        var back = await project.MapAsync();
        Assert.Equal(["Saisir > Envoyer : complet"], Flows(back));
        Assert.Equal(["Envoyer", "Saisir"], Labels(back));

        // A step of another map, or a position other than before and after, is refused before anything is written.
        var version = back.GetProperty("structure_version").GetInt32();
        (await facilitator.PostAsync(Insert(project), new { content = AchatsV2, lane_id = project.FirstLaneId, anchor_step_id = first, position = "parallel" }, version))
            .Refused(400, "VALEUR_INVALIDE");
        Assert.Equal(["Envoyer", "Saisir"], Labels(await project.MapAsync()));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Inserted_on_a_flow_the_file_cuts_that_flow_only_even_one_exit_of_a_decision_and_one_undo_joins_it_again(EngineKind kind)
    {
        // Decision of the owner of the project of 17/11/2026: a shape dropped on a connector in Visio, Bizagi or draw.io.
        var host = await hosts.OfAsync(kind);
        var project = await ApiProject.CreateAsync(host);
        using var facilitator = project.Facilitator;
        var check = await project.AddStepAsync(0, "Conforme ?", type: "decision");
        var send = await project.AddStepAsync(1, "Expédier");
        var redo = await project.AddStepAsync(1, "Reprendre", row: 1);
        var yes = await project.AddFlowAsync(check.Id, send.Id, "oui");
        await project.AddFlowAsync(check.Id, redo.Id, "non");
        var other = await project.AddLaneAsync("Achats");

        var preview = (await facilitator.PostAsync($"{Insert(project)}/preview", new { content = AchatsV2 })).Ok();
        var version = preview.GetProperty("structure_version").GetInt32();
        var reply = (await facilitator.PostAsync(Insert(project), new { content = AchatsV2, lane_id = other, flow_id = yes, anchor_step_id = redo.Id }, version)).Ok();
        var map = reply.GetProperty("map");
        // The exit chosen only is cut, its label on the side of its origin; the other exit stays; the step in the lane chosen.
        Assert.Equal(["Achats > Expédier : ", "Conforme ? > Achats : oui", "Conforme ? > Reprendre : non"], Flows(map));
        var inserted = StepOf(map, "Achats");
        Assert.Equal((other, 1), (inserted.GetProperty("lane_id").GetGuid(), inserted.GetProperty("column").GetInt32()));
        // Made at once in the lane chosen: the steps of the lane of the origin are not pushed for nothing (audit of the copy 64, P2-06).
        Assert.Equal((1, 1), (StepOf(map, "Expédier").GetProperty("column").GetInt32(), StepOf(map, "Reprendre").GetProperty("column").GetInt32()));
        Assert.Equal(reply.GetProperty("id").GetGuid(), inserted.GetProperty("id").GetGuid());
        Assert.NotEqual(JsonValueKind.Null, inserted.GetProperty("sub_map_id").ValueKind);

        (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/undo")).Ok();
        var back = await project.MapAsync();
        Assert.Equal(["Conforme ? > Expédier : oui", "Conforme ? > Reprendre : non"], Flows(back));
        Assert.Equal(["Conforme ?", "Expédier", "Reprendre"], Labels(back));

        // A flow unknown, or of another map, is refused before anything is written.
        version = back.GetProperty("structure_version").GetInt32();
        (await facilitator.PostAsync(Insert(project), new { content = AchatsV2, lane_id = project.FirstLaneId, flow_id = Guid.NewGuid() }, version))
            .Refused(404, "INTROUVABLE");
        var (detail, lowerLane) = await project.DetailAsync(send.Id);
        var a = await project.AddStepAsync(0, "A", lowerLane, detail);
        var b = await project.AddStepAsync(1, "B", lowerLane, detail);
        var elsewhere = await project.AddFlowAsync(a.Id, b.Id, mapId: detail);
        version = (await project.MapAsync()).GetProperty("structure_version").GetInt32();
        (await facilitator.PostAsync(Insert(project), new { content = AchatsV2, lane_id = project.FirstLaneId, flow_id = elsewhere }, version))
            .Refused(404, "INTROUVABLE");
        Assert.Equal(["Conforme ?", "Expédier", "Reprendre"], Labels(await project.MapAsync()));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Without_the_option_no_sheet_is_copied_and_a_label_typed_is_kept_without_the_translations_of_the_file(EngineKind kind)
    {
        var host = await hosts.OfAsync(kind);
        var project = await ApiProject.CreateAsync(host);
        using var facilitator = project.Facilitator;
        var reply = await InsertedAsync(project, facilitator, Achats, withSheets: false, label: "Commander");
        var inserted = StepOf(reply.GetProperty("map"), "Commander");
        Assert.False(inserted.GetProperty("translations").TryGetProperty("en", out _));
        var detail = await project.MapAsync(inserted.GetProperty("sub_map_id").GetGuid());
        Assert.All(detail.GetProperty("steps").EnumerateArray(), step => Assert.NotEqual("draft", step.GetProperty("sheet").GetProperty("status").GetString()));
        Assert.Equal("Commander", detail.GetProperty("name").GetString());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_copy_of_the_same_project_inserts_twice_beside_its_own_steps(EngineKind kind)
    {
        // The identifiers of a copy are the codes of the steps of the project: the insertion numbers its own, never takes
        // a step of the file for a step of the project.
        var host = await hosts.OfAsync(kind);
        var project = await ApiProject.CreateAsync(host);
        using var facilitator = project.Facilitator;
        (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/steps", new { lane_id = project.FirstLaneId, column = 0, label = "Saisir" })).Ok();
        var copy = (await facilitator.GetAsync($"/api/maps/{project.RootMapId}/pmap")).Body;
        await InsertedAsync(project, facilitator, copy, withSheets: true, label: "Premier");
        var reply = await InsertedAsync(project, facilitator, copy, withSheets: true, label: "Second");
        var root = reply.GetProperty("map");
        Assert.Equal(["Premier", "Saisir", "Second"], Labels(root));
        var codes = root.GetProperty("steps").EnumerateArray().Select(step => step.GetProperty("code").GetString()).ToList();
        Assert.Equal(codes.Count, codes.Distinct().Count());
        foreach (var label in new[] { "Premier", "Second" })
            Assert.Equal(["Saisir"], Labels(await project.MapAsync(StepOf(root, label).GetProperty("sub_map_id").GetGuid())));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_detail_of_a_step_is_replaced_by_a_newer_file_in_one_operation_undone_by_one_undo(EngineKind kind)
    {
        var host = await hosts.OfAsync(kind);
        var project = await ApiProject.CreateAsync(host);
        using var facilitator = project.Facilitator;
        var inserted = await InsertedAsync(project, facilitator, Achats, withSheets: true);
        var stepId = inserted.GetProperty("id").GetGuid();
        var oldDetail = inserted.GetProperty("ids")[0].GetGuid();

        var preview = (await facilitator.PostAsync($"{Insert(project)}/preview", new { content = AchatsV2, step_id = stepId })).Ok();
        Assert.Equal(1, preview.GetProperty("steps").GetInt32());
        var reply = (await facilitator.PostAsync($"/api/steps/{stepId}/replace-pmap", new { content = AchatsV2 }, preview.GetProperty("structure_version").GetInt32())).Ok();
        Assert.Equal("inserer_fichier", reply.GetProperty("history").GetProperty("undo")[0].GetProperty("kind").GetString());
        var newDetail = StepOf(reply.GetProperty("map"), "Achats").GetProperty("sub_map_id").GetGuid();
        Assert.NotEqual(oldDetail, newDetail);
        Assert.Equal(["Commander en ligne"], Labels(await project.MapAsync(newDetail)));
        (await facilitator.GetAsync($"/api/maps/{oldDetail}")).Refused(404, "INTROUVABLE");
        // The audit keeps both copies, as the opening of a file, identifiers and counts only.
        using (var admin = project.Admin)
        {
            var events = (await admin.GetAsync($"/api/admin/audit?project_id={project.Id}&kind={AuditKinds.FileInserted}")).Ok().EnumerateArray()
                .Select(audited => audited.GetProperty("details")).Select(raw => raw.ValueKind == JsonValueKind.String ? JsonDocument.Parse(raw.GetString()!).RootElement : raw)
                .Select(details => (details.GetProperty("mode").GetString(), details.GetProperty("step").GetGuid(), details.GetProperty("steps").GetInt32())).Order().ToList();
            Assert.Equal([("insert", stepId, 4), ("replace", stepId, 1)], events);
        }

        (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/undo")).Ok();
        Assert.Equal(oldDetail, StepOf(await project.MapAsync(), "Achats").GetProperty("sub_map_id").GetGuid());
        Assert.Equal(["Passer la commande", "Saisir la demande"], Labels(await project.MapAsync(oldDetail)));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_step_without_content_refuses_a_detail_and_a_file_too_deep_for_the_map_is_refused_before_anything_is_written(EngineKind kind)
    {
        var host = await hosts.OfAsync(kind);
        var project = await ApiProject.CreateAsync(host);
        using var facilitator = project.Facilitator;
        var end = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/steps", new { lane_id = project.FirstLaneId, column = 0, label = "Fin", type = "end" }))
            .Ok().GetProperty("id").GetGuid();
        var version = (await project.MapAsync()).GetProperty("structure_version").GetInt32();
        (await facilitator.PostAsync($"/api/steps/{end}/replace-pmap", new { content = AchatsV2 }, version)).Refused(400, "ETAPE_NON_DETAILLABLE");

        // Twelve levels: the root map leaves room for eleven.
        static string Nested(int levels, int level = 1) => level > levels
            ? $$"""{ "lanes": [{ "id": "l{{level}}", "name": "X" }], "steps": [{ "id": "s{{level}}", "lane": "l{{level}}", "label": "Fin" }] }"""
            : $$"""{ "lanes": [{ "id": "l{{level}}", "name": "X" }], "steps": [{ "id": "s{{level}}", "lane": "l{{level}}", "label": "N", "sub_map": {{Nested(levels, level + 1)}} }] }""";
        var deep = $$"""{ "format": "topogenia", "version": "1.1", "project": { "name": "Profond" }, "map": {{Nested(11)}} }""";
        var preview = (await facilitator.PostAsync($"{Insert(project)}/preview", new { content = deep })).Ok();
        Assert.Equal((12, 11), (preview.GetProperty("depth").GetInt32(), preview.GetProperty("room").GetInt32()));
        var fault = Assert.Single((await facilitator.PostAsync(Insert(project), new { content = deep, lane_id = project.FirstLaneId, column = 1 },
            preview.GetProperty("structure_version").GetInt32())).Refused(400, "FICHIER_INVALIDE").GetProperty("errors").EnumerateArray());
        Assert.Equal(("map", "PMAP_PROFONDEUR"), (fault.GetProperty("path").GetString(), fault.GetProperty("code").GetString()));
        Assert.Equal(["Fin"], Labels(await project.MapAsync()));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_file_refused_by_a_rule_of_the_domain_while_its_detail_is_built_writes_nothing_not_even_the_step(EngineKind kind)
    {
        // The headcount of a system lane passes the reading and is refused by the rule of the domain, once the step and
        // its detail are begun: the whole operation is refused.
        var host = await hosts.OfAsync(kind);
        var project = await ApiProject.CreateAsync(host);
        using var facilitator = project.Facilitator;
        const string Robot = """
            { "format": "topogenia", "version": "1.2", "project": { "name": "Robot" },
              "map": { "lanes": [{ "id": "c1", "name": "Demandeur" }, { "id": "c2", "name": "ERP", "type": "system", "headcount": 3 }],
                       "steps": [{ "id": "e1", "lane": "c1", "label": "Saisir", "column": 0 }, { "id": "e2", "lane": "c2", "label": "Enregistrer", "column": 1 }] } }
            """;
        var version = (await project.MapAsync()).GetProperty("structure_version").GetInt32();
        var refused = (await facilitator.PostAsync(Insert(project), new { content = Robot, lane_id = project.FirstLaneId, column = 0 }, version))
            .Refused(400, "FICHIER_INVALIDE");
        Assert.StartsWith("map.lanes[1]", Assert.Single(refused.GetProperty("errors").EnumerateArray()).GetProperty("path").GetString(), StringComparison.Ordinal);
        var map = await project.MapAsync();
        Assert.Empty(map.GetProperty("steps").EnumerateArray());
        Assert.Equal(version, map.GetProperty("structure_version").GetInt32());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_stale_preview_a_reader_and_a_file_in_a_language_that_is_not_configured_change_nothing(EngineKind kind)
    {
        var host = await hosts.OfAsync(kind);
        var project = await ApiProject.CreateAsync(host);
        using var facilitator = project.Facilitator;
        using var reader = project.Reader;
        var preview = (await facilitator.PostAsync($"{Insert(project)}/preview", new { content = Achats })).Ok();
        (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/lanes", new { name = "Logistique" })).Ok();
        (await facilitator.PostAsync(Insert(project), new { content = Achats, lane_id = project.FirstLaneId, column = 0 }, preview.GetProperty("structure_version").GetInt32()))
            .Refused(409, "CONFLIT_VERSION");

        (await reader.PostAsync($"{Insert(project)}/preview", new { content = Achats })).Refused(403, "ACCES_REFUSE");
        (await reader.PostAsync(Insert(project), new { content = Achats, lane_id = project.FirstLaneId, column = 0 }, 1)).Refused(403, "ACCES_REFUSE");

        var esperanto = AchatsV2.Replace("\"name\": \"Achats\" }", "\"name\": \"Achats\", \"language\": \"eo\" }", StringComparison.Ordinal);
        Assert.Equal("project.language", Assert.Single((await facilitator.PostAsync($"{Insert(project)}/preview", new { content = esperanto }))
            .Refused(400, "FICHIER_INVALIDE").GetProperty("errors").EnumerateArray()).GetProperty("path").GetString());
        Assert.Empty((await project.MapAsync()).GetProperty("steps").EnumerateArray());
    }
}
