// ContentEntryTests.cs
// sheets.json, documents.json, painpoints.json and translations.json of the package (spec 7.2), with their order.

using System.Text.Json.Nodes;
using Topogenia.Interchange.Packages;
using Topogenia.Interchange.Tests.Support;

namespace Topogenia.Interchange.Tests.Packages;

public sealed class ContentEntryTests
{
    private static JsonNode Entry(string path)
    {
        var sample = new SampleProject();
        var entries = PackageWriter.Entries(sample.Workspace(), sample.Logins, new PackageOptions(WithoutPeople: false));
        return JsonNode.Parse(entries.Single(entry => entry.Path == path).Content)!;
    }

    [Fact]
    public void The_sheets_of_the_exported_steps_are_by_step_code_the_remainder_of_an_open_frame_included()
    {
        var sheets = Entry("sheets.json")["sheets"]!.AsArray();

        Assert.Equal(["CONGES-E004", "CONGES-E010", "CONGES-E012"], sheets.Select(sheet => (string?)sheet!["step"]));
        Assert.Equal(["validated", "needs_review", "draft"], sheets.Select(sheet => (string?)sheet!["status"]));
        Assert.Equal("Préciser le délai de prévenance", (string?)sheets[1]!["clarificationRequest"]);
    }

    [Fact]
    public void A_sheet_carries_its_fields_its_procedure_and_its_validated_versions_with_their_frozen_json()
    {
        var sheet = Entry("sheets.json")["sheets"]![0]!;

        Assert.Equal("Manager direct", (string?)sheet["actor"]);
        Assert.Equal("Accorder ou refuser la demande", (string?)sheet["purpose"]);
        Assert.Equal("Messagerie", (string?)sheet["tools"]);
        Assert.Equal("weekly", (string?)sheet["frequency"]);
        Assert.Equal(15.5m, (decimal)sheet["durationMinutes"]!);
        Assert.Equal("""{"unit":"day","value":1}""", sheet["leadTime"]!.ToJsonString());
        Assert.Equal("Vérifier le planning de l'équipe avant de répondre.\nRépondre sous 48 h.", (string?)sheet["freeText"]);
        Assert.Equal("""[{"nature":"check","position":1,"text":"Ouvrir la demande","tool":"Messagerie"},{"position":2,"text":"Consulter le planning"}]""",
            sheet["procedure"]!.ToJsonString());
        var versions = sheet["versions"]!.AsArray();
        Assert.Equal([1, 2], versions.Select(version => (int)version!["number"]!));
        Assert.Equal("""{"fields":{},"procedure":[{"text":"Ouvrir"}],"filled_count":1}""", (string?)versions[0]!["content"]);
        Assert.Equal("""{"fields":{"purpose":{"text":"Accorder ou refuser"},"frequency":{"text":"weekly"},"duration_minutes":{"number":15},"lead_time":{"number":2,"text":"hour"}},"procedure":[{"text":"Ouvrir la demande","tool":"Messagerie","nature":"check"}],"filled_count":4}""",
            (string?)versions[1]!["content"]);
        Assert.Equal("EXEMPLE\\denis", (string?)versions[0]!["validatedBy"]);
        Assert.Equal("weekly", (string?)versions[1]!["frequency"]);
        Assert.Equal(15m, (decimal)versions[1]!["durationMinutes"]!);
        Assert.Equal("""{"unit":"hour","value":2}""", versions[1]!["leadTime"]!.ToJsonString());
        Assert.False(versions[0]!.AsObject().ContainsKey("leadTime"));
        Assert.Equal("2026-09-01T08:44:00.0000000Z", (string?)versions[1]!["validatedAt"]);
        Assert.False(versions[0]!.AsObject().ContainsKey("frequency"));
    }

    [Fact]
    public void The_documents_carry_their_direction_location_waiting_time_and_uses_with_their_ca_and_pain_point()
    {
        var documents = Entry("documents.json")["documents"]!.AsArray();

        Assert.Equal(["Formulaire de demande", "Planning d'équipe", "Relevé des soldes"], documents.Select(document => (string?)document!["name"]));
        var form = documents[0]!;
        Assert.Equal("CONGES-E002", (string?)form["step"]);
        Assert.Equal("output", (string?)form["direction"]);
        Assert.Equal("https://exemple.invalid/formulaire", (string?)form["location"]);
        Assert.Equal("url", (string?)form["locationType"]);
        // The waiting time of an output is carried by each of its uses (annex A 2.11): only the external input has one.
        Assert.False(form.AsObject().ContainsKey("waitHours"));
        Assert.True((bool)form["shareConfirmed"]!);
        Assert.Equal("input", (string?)documents[1]!["direction"]);
        Assert.Equal("path", (string?)documents[1]!["locationType"]);
        Assert.Equal("2", documents[1]!["waitHours"]!.ToJsonString());
        var uses = form["uses"]!.AsArray();
        Assert.Equal(["CONGES-E003", "CONGES-E004"], uses.Select(use => (string?)use!["step"]));
        Assert.Equal(["createdAt", "createdBy", "step"], uses[0]!.AsObject().Select(property => property.Key));
        Assert.Equal(4m, (decimal)uses[1]!["waitHours"]!);
        Assert.Equal(80, (int)uses[1]!["percentCompleteAccurate"]!);
        Assert.Equal("CONGES-I001", (string?)uses[1]!["painPoint"]);
        Assert.Equal("CONGES-E012", (string?)documents[2]!["step"]);
    }

    [Fact]
    public void The_pain_points_carry_their_step_hours_status_and_history_of_statuses_in_time_order()
    {
        var painPoints = Entry("painpoints.json")["painPoints"]!.AsArray();

        Assert.Equal(["CONGES-I001", "CONGES-I002", "CONGES-I003"], painPoints.Select(painPoint => (string?)painPoint!["code"]));
        var first = painPoints[0]!;
        Assert.Equal("CONGES-E004", (string?)first["step"]);
        Assert.Equal("3.5", first["lostHoursPerMonth"]!.ToJsonString());
        Assert.Equal("scored", (string?)first["status"]);
        Assert.Equal(
            """[{"changedAt":"2026-09-01T08:52:00.0000000Z","changedBy":"EXEMPLE\\chloe","statusAfter":"reported"},{"changedAt":"2026-09-01T08:53:00.0000000Z","changedBy":"EXEMPLE\\alice","comment":"Chiffré en atelier","statusAfter":"scored","statusBefore":"reported"}]""",
            first["history"]!.ToJsonString(new System.Text.Json.JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
        Assert.Equal("CONGES-E012", (string?)painPoints[1]!["step"]);
        Assert.False(painPoints[2]!.AsObject().ContainsKey("step"));
        Assert.Equal("under_review", (string?)painPoints[2]!["status"]);
    }

    [Fact]
    public void The_translations_of_the_exported_objects_are_all_there_manual_and_automatic_with_their_source()
    {
        var translations = Entry("translations.json")["translations"]!.AsArray();

        Assert.Equal(
            [
                "project||Leave request|manual", "map||Leave request|auto", "map|CONGES-E002|Enter the request|auto",
                "lane|CONGES-C01|Employee|manual", "step|CONGES-E002|Enter the request|auto", "step|CONGES-E012|Check the balances|manual",
                "flow|CONGES-E003>CONGES-E004|yes|manual",
            ],
            translations.Select(item => string.Join('|', (string?)item!["object"], Reference(item), (string?)item["text"], (string?)item["source"])));
        Assert.All(translations, item => Assert.Equal("en", (string?)item!["language"]));
        Assert.Equal(["label", "label", "label"], translations.Where(item => (string?)item!["object"] is "step" or "flow").Select(item => (string?)item!["field"]));
        Assert.Equal("EXEMPLE\\alice", (string?)translations[0]!["updatedBy"]);
        Assert.False(translations[1]!.AsObject().ContainsKey("updatedBy"));
        Assert.Equal("2026-09-01T09:00:00.0000000Z", (string?)translations[1]!["updatedAt"]);
    }

    private static string Reference(JsonNode item) =>
        (string?)item["parentStep"] ?? (string?)item["lane"] ?? (string?)item["step"]
        ?? (item["flow"] is { } flow ? (string?)flow["source"] + ">" + (string?)flow["target"] : "");
}
