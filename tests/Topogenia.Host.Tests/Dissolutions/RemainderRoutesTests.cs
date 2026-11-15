// RemainderRoutesTests.cs
// The remainder of a frame through the whole host on both databases: assign, remove, finish and their refusals (annex A 1.5-03 to 13, A 2.9).

using System.Text.Json;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Persistence.Tests.Infrastructure;
using static Topogenia.Host.Tests.Selection.MapDrawing;

namespace Topogenia.Host.Tests.Dissolutions;

public sealed class RemainderRoutesTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    /// <summary>A dissolved row whose remainder holds two outputs, « Bon de commande » then « Facture »; the frame is returned.</summary>
    private async Task<(DetailedRow Row, Guid Frame)> DissolvedAsync(EngineKind kind)
    {
        var row = await DetailedRow.CreateAsync(await ApiProject.CreateAsync(await hosts.OfAsync(kind)), "Bon de commande", "Facture");
        using var facilitator = row.Project.Facilitator;
        return (row, (await facilitator.PostAsync($"/api/steps/{row.Macro}/dissolve", null, await row.MacroVersionAsync())).Ok().GetProperty("id").GetGuid());
    }

    private static IReadOnlyList<string?> DocumentNames(JsonElement frame) =>
        frame.GetProperty("documents").EnumerateArray().Select(document => document.GetProperty("name").GetString()).ToList();

    private static IReadOnlyList<Guid> UsageIds(JsonElement frame) =>
        frame.GetProperty("usages").EnumerateArray().Select(usage => usage.GetProperty("document_id").GetGuid()).ToList();

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task An_assignment_answers_the_frame_left_with_the_map_brings_back_from_its_holder_and_is_undone_by_the_map(EngineKind kind)
    {
        var (row, frame) = await DissolvedAsync(kind);
        using var facilitator = row.Project.Facilitator;
        var assigned = (await facilitator.PostAsync($"/api/dissolutions/{frame}/assign", new { kind = "document", object_id = row.Documents[0], step_id = row.First })).Ok();
        Assert.Equal(frame, assigned.GetProperty("id").GetGuid());
        Assert.Equal(["Facture"], DocumentNames(assigned.GetProperty("frame")));
        Assert.Equal(["Facture"], DocumentNames(assigned.GetProperty("map").GetProperty("dissolution")));
        Assert.Equal(["Bon de commande"], Step(assigned.GetProperty("map"), row.First).GetProperty("markers").GetProperty("documents").EnumerateArray()
            .Select(marker => marker.GetProperty("name").GetString()));

        var back = (await facilitator.PostAsync($"/api/dissolutions/{frame}/assign",
            new { kind = "document", object_id = row.Documents[0], step_id = row.Macro, from_step_id = row.First })).Ok();
        Assert.Equal(["Bon de commande", "Facture"], DocumentNames(back.GetProperty("frame")));
        var undone = (await facilitator.PostAsync($"/api/maps/{row.Project.RootMapId}/undo")).Ok();
        Assert.Equal((back.GetProperty("operation_id").GetGuid(), "affecter"), (undone.GetProperty("id").GetGuid(), undone.GetProperty("code").GetString()));
        Assert.Equal(["Facture"], DocumentNames(undone.GetProperty("map").GetProperty("dissolution")));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task An_assignment_refuses_an_unknown_kind_a_missing_object_or_step_and_an_unknown_frame(EngineKind kind)
    {
        var (row, frame) = await DissolvedAsync(kind);
        using var facilitator = row.Project.Facilitator;
        var url = $"/api/dissolutions/{frame}/assign";
        string? Field(ApiResponse response, string code) => response.Refused(400, code).GetProperty("field").GetString();

        Assert.Equal("kind", Field(await facilitator.PostAsync(url, new { kind = "memo", object_id = row.Documents[0], step_id = row.First }), "VALEUR_INVALIDE"));
        Assert.Equal("object_id", Field(await facilitator.PostAsync(url, new { kind = "document", step_id = row.First }), "CHAMP_OBLIGATOIRE"));
        Assert.Equal("step_id", Field(await facilitator.PostAsync(url, new { kind = "document", object_id = row.Documents[0] }), "CHAMP_OBLIGATOIRE"));
        Assert.Equal("from_step_id", Field(await facilitator.PostAsync(url, new { kind = "document", object_id = row.Documents[0], step_id = row.Macro }), "CHAMP_OBLIGATOIRE"));
        (await facilitator.PostAsync($"/api/dissolutions/{Guid.NewGuid()}/assign", new { kind = "document", object_id = row.Documents[0], step_id = row.First }))
            .Refused(404, "INTROUVABLE");
        var read = (await facilitator.GetAsync($"/api/maps/{row.Project.RootMapId}")).Ok().GetProperty("map");
        Assert.Equal(["Bon de commande", "Facture"], DocumentNames(read.GetProperty("dissolution")));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_use_brought_back_to_the_remainder_without_the_step_that_holds_it_is_refused_on_from_step_id(EngineKind kind)
    {
        var row = await DetailedRow.CreateAsync(await ApiProject.CreateAsync(await hosts.OfAsync(kind)), "Bon de commande");
        using var facilitator = row.Project.Facilitator;
        // Before produces « Devis » and Macro uses it: once Macro is dissolved, the use is in the remainder.
        var quote = (await facilitator.PostAsync($"/api/steps/{row.Before}/documents", new { direction = "output", name = "Devis" })).Ok()
            .GetProperty("outputs")[0].GetProperty("document").GetProperty("id").GetGuid();
        (await facilitator.PutAsync($"/api/steps/{row.Macro}/uses/{quote}", new { wait_hours = 1 }, 0)).Ok();
        var frame = (await facilitator.PostAsync($"/api/steps/{row.Macro}/dissolve", null, await row.MacroVersionAsync())).Ok().GetProperty("id").GetGuid();
        var url = $"/api/dissolutions/{frame}/assign";
        Assert.Equal([quote], UsageIds((await row.Project.MapAsync()).GetProperty("dissolution")));
        Assert.Empty(UsageIds((await facilitator.PostAsync(url, new { kind = "usage", object_id = quote, step_id = row.First })).Ok().GetProperty("frame")));

        // Back to the dissolved step without the step that holds the use (1.x ServiceStructure.Remontee.cs:396).
        Assert.Equal("from_step_id", (await facilitator.PostAsync(url, new { kind = "usage", object_id = quote, step_id = row.Macro }))
            .Refused(400, "CHAMP_OBLIGATOIRE").GetProperty("field").GetString());
        var remainder = (await row.Project.MapAsync()).GetProperty("dissolution");
        Assert.Empty(UsageIds(remainder));
        Assert.Equal(["Bon de commande"], DocumentNames(remainder));
        Assert.True((await facilitator.GetAsync($"/api/steps/{row.First}/sheet")).Ok().GetProperty("inputs").EnumerateArray()
            .Single(input => input.GetProperty("document").GetProperty("id").GetGuid() == quote).GetProperty("used").GetBoolean());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Removing_from_the_remainder_is_for_good_logs_nothing_and_an_element_already_removed_is_not_found(EngineKind kind)
    {
        var (row, frame) = await DissolvedAsync(kind);
        using var facilitator = row.Project.Facilitator;
        var url = $"/api/dissolutions/{frame}/remove";
        var removed = (await facilitator.PostAsync(url, new { kind = "document", object_id = row.Documents[0] })).Ok();
        Assert.Equal((frame, JsonValueKind.Null), (removed.GetProperty("id").GetGuid(), removed.GetProperty("operation_id").ValueKind));
        Assert.Equal(["Facture"], DocumentNames(removed.GetProperty("frame")));

        (await facilitator.PostAsync(url, new { kind = "document", object_id = row.Documents[0] })).Refused(404, "INTROUVABLE");
        Assert.Equal("object_id", (await facilitator.PostAsync(url, new { kind = "document" })).Refused(400, "CHAMP_OBLIGATOIRE").GetProperty("field").GetString());
        Assert.Equal("kind", (await facilitator.PostAsync(url, new { kind = "memo", object_id = row.Documents[1] })).Refused(400, "VALEUR_INVALIDE").GetProperty("field").GetString());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Finishing_answers_the_arrangements_closes_the_frame_and_a_second_finish_answers_none(EngineKind kind)
    {
        var (row, frame) = await DissolvedAsync(kind);
        using var facilitator = row.Project.Facilitator;
        var finished = (await facilitator.PostAsync($"/api/dissolutions/{frame}/finish")).Ok();
        var arrangements = finished.GetProperty("arrangements").EnumerateArray().ToList();
        Assert.Equal([("document", row.Documents[0], "Bon de commande"), ("document", row.Documents[1], "Facture")],
            arrangements.Select(item => (item.GetProperty("kind").GetString(), item.GetProperty("object_id").GetGuid(), item.GetProperty("name").GetString())));
        Assert.Equal(JsonValueKind.Null, finished.GetProperty("map").GetProperty("dissolution").ValueKind);
        Assert.Equal(frame, finished.GetProperty("id").GetGuid());
        var operation = finished.GetProperty("operation_id").GetGuid();

        var again = (await facilitator.PostAsync($"/api/dissolutions/{frame}/finish")).Ok();
        Assert.Equal((0, JsonValueKind.Null), (again.GetProperty("arrangements").GetArrayLength(), again.GetProperty("operation_id").ValueKind));
        var undone = (await facilitator.PostAsync($"/api/maps/{row.Project.RootMapId}/undo")).Ok();
        Assert.Equal((operation, "terminer_remontee"), (undone.GetProperty("id").GetGuid(), undone.GetProperty("code").GetString()));
        Assert.Equal(frame, undone.GetProperty("map").GetProperty("dissolution").GetProperty("id").GetGuid());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_closed_frame_takes_no_assignment_and_no_removal(EngineKind kind)
    {
        var (row, frame) = await DissolvedAsync(kind);
        using var facilitator = row.Project.Facilitator;
        var stored = (await facilitator.PostAsync($"/api/dissolutions/{frame}/finish")).Ok().GetProperty("arrangements")[0];
        var (document, holder) = (stored.GetProperty("object_id").GetGuid(), stored.GetProperty("step_id").GetGuid());
        (await facilitator.PostAsync($"/api/dissolutions/{frame}/assign", new { kind = "document", object_id = document, step_id = row.First == holder ? row.Second : row.First }))
            .Refused(409, "REMONTEE_TERMINEE");
        (await facilitator.PostAsync($"/api/dissolutions/{frame}/remove", new { kind = "document", object_id = document })).Refused(409, "REMONTEE_TERMINEE");
        Assert.Contains(document, (await row.Project.MapAsync()).GetProperty("steps").EnumerateArray()
            .Where(step => step.GetProperty("id").GetGuid() == holder)
            .SelectMany(step => step.GetProperty("markers").GetProperty("documents").EnumerateArray()).Select(marker => marker.GetProperty("document_id").GetGuid()));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_frame_routes_hide_the_frame_from_a_stranger_and_refuse_a_contributor(EngineKind kind)
    {
        var (row, frame) = await DissolvedAsync(kind);
        var requests = new (string Url, object? Body)[]
        {
            ($"/api/dissolutions/{frame}/assign", new { kind = "document", object_id = row.Documents[0], step_id = row.First }),
            ($"/api/dissolutions/{frame}/remove", new { kind = "document", object_id = row.Documents[0] }),
            ($"/api/dissolutions/{frame}/finish", null),
        };
        using var stranger = row.Project.Stranger;
        using var contributor = row.Project.Contributor;
        foreach (var (url, body) in requests)
        {
            (await stranger.PostAsync(url, body)).Refused(404, "INTROUVABLE");
            (await contributor.PostAsync(url, body)).Refused(403, "ACCES_REFUSE");
        }
        Assert.Equal(["Bon de commande", "Facture"], DocumentNames((await row.Project.MapAsync()).GetProperty("dissolution")));
    }
}
