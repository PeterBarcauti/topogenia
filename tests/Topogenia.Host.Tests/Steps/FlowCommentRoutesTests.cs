// FlowCommentRoutesTests.cs
// The routes of the comment of presentation of a flow through the whole host on both databases (ADR 099, lot J10): written line by line with If-Match, read with its map, out of the history, kept with a flow in the trash and back with it (its creation undone, a step inserted on it), kept by a flow reversed whose original follows its new source, gone with its purge, left by a copy.

using System.Text.Json;
using Topogenia.Host.Tests.Api;
using Topogenia.Host.Tests.Exchange;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Steps;

public sealed class FlowCommentRoutesTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private async Task<ApiProject> ProjectAsync(EngineKind kind) => await ApiProject.CreateAsync(await hosts.OfAsync(kind));

    private static string CommentUrl(Guid flow) => $"/api/flows/{flow}/presentation-comment";

    /// <summary>The lines of the comments of the flows of a map as a presentation reads them: flow, language, text, source, version.</summary>
    private static async Task<List<(Guid Flow, string Language, string Text, string Source, int Version)>> SaidAsync(ApiClient client, Guid mapId) =>
        [.. (await client.GetAsync($"/api/maps/{mapId}/flow-presentation-comments")).Ok().EnumerateArray().Select(Line)];

    private static (Guid Flow, string Language, string Text, string Source, int Version) Line(JsonElement comment)
    {
        Assert.Equal(["flow_id", "language", "source", "text", "version"], comment.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        return (comment.GetProperty("flow_id").GetGuid(), comment.GetProperty("language").GetString()!, comment.GetProperty("text").GetString()!,
            comment.GetProperty("source").GetString()!, comment.GetProperty("version").GetInt32());
    }

    /// <summary>Three steps in a row and the two flows between them.</summary>
    private static async Task<(Guid[] Steps, Guid First, Guid Second)> ChainAsync(ApiProject project)
    {
        Guid[] steps = [(await project.AddStepAsync(0, "Commander")).Id, (await project.AddStepAsync(1, "Livrer")).Id, (await project.AddStepAsync(2, "Payer")).Id];
        return (steps, await project.AddFlowAsync(steps[0], steps[1]), await project.AddFlowAsync(steps[1], steps[2]));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_facilitator_writes_the_lines_of_the_comment_of_a_flow_and_every_reader_of_the_map_reads_them(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        using var reader = project.Reader;
        var (_, first, second) = await ChainAsync(project);

        var written = (await facilitator.PutAsync(CommentUrl(first), new { text = "  La commande part chez le fournisseur.  ", language = "fr" }, 0)).Ok();
        Assert.Equal(["comments"], written.EnumerateObject().Select(property => property.Name));
        Assert.Equal([(first, "fr", "La commande part chez le fournisseur.", "manual", 1)], written.GetProperty("comments").EnumerateArray().Select(Line));
        var translated = (await facilitator.PutAsync(CommentUrl(first), new { text = "The order goes to the supplier.", language = "en" }, 0)).Ok();
        Assert.Equal([("en", 1), ("fr", 1)], translated.GetProperty("comments").EnumerateArray().Select(Line).Select(line => (line.Language, line.Version)));
        (await facilitator.PutAsync(CommentUrl(second), new { text = "La facture suit la livraison.", language = "fr" }, 0)).Ok();
        (await facilitator.PutAsync(CommentUrl(first), new { text = "La commande part chez le fournisseur, par courriel.", language = "fr" }, 1)).Ok();

        var said = await SaidAsync(reader, project.RootMapId);
        Assert.Equal(new[] { (first, "en", "The order goes to the supplier.", "manual", 1), (first, "fr", "La commande part chez le fournisseur, par courriel.", "manual", 2),
            (second, "fr", "La facture suit la livraison.", "manual", 1) }.OrderBy(line => line.Item1).ThenBy(line => line.Item2, StringComparer.Ordinal), said);
        // The comments of the steps are another list: no step of the map has one.
        Assert.Empty((await reader.GetAsync($"/api/maps/{project.RootMapId}/presentation-comments")).Ok().EnumerateArray());
        // An empty original removes the comment of the flow, its translation too; the other flow keeps its own.
        Assert.Empty((await facilitator.PutAsync(CommentUrl(first), new { text = "", language = "fr" }, 2)).Ok().GetProperty("comments").EnumerateArray());
        Assert.Equal([(second, "fr")], (await SaidAsync(reader, project.RootMapId)).Select(line => (line.Flow, line.Language)));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task An_invalid_comment_an_unknown_flow_and_a_stale_version_are_refused_and_nothing_is_written(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        using var reader = project.Reader;
        var (steps, flow, _) = await ChainAsync(project);
        (await facilitator.PutAsync(CommentUrl(flow), new { text = "Une phrase.", language = "xx" }, 0)).Refused(400, "LANGUE_INVALIDE");
        (await facilitator.PutAsync(CommentUrl(flow), new { text = "Une phrase." }, 0)).Refused(400, "LANGUE_INVALIDE");
        (await facilitator.PutAsync(CommentUrl(flow), new { text = "<b>Une phrase.</b>", language = "fr" }, 0)).Refused(400, "TEXTE_HTML_INTERDIT");
        (await facilitator.PutAsync(CommentUrl(flow), new { text = new string('x', 501), language = "fr" }, 0)).Refused(400, "TEXTE_TROP_LONG");
        // The identifier of a step is no flow, and a reader writes no comment.
        (await facilitator.PutAsync(CommentUrl(steps[0]), new { text = "Une phrase.", language = "fr" }, 0)).Refused(404, "INTROUVABLE");
        (await facilitator.PutAsync(CommentUrl(Guid.NewGuid()), new { text = "Une phrase.", language = "fr" }, 0)).Refused(404, "INTROUVABLE");
        (await reader.PutAsync(CommentUrl(flow), new { text = "Une phrase.", language = "fr" }, 0)).Refused(403, "ACCES_REFUSE");
        Assert.Empty(await SaidAsync(reader, project.RootMapId));

        (await facilitator.PutAsync(CommentUrl(flow), new { text = "Version de Anne.", language = "fr" }, 0)).Ok();
        var conflict = (await facilitator.PutAsync(CommentUrl(flow), new { text = "Version lue avant.", language = "fr" }, 0)).Refused(409, "CONFLIT_VERSION");
        Assert.Equal("anne", conflict.GetProperty("by").GetString());
        Assert.Equal([(flow, "fr", "Version de Anne.", "manual", 1)], await SaidAsync(reader, project.RootMapId));
    }

    /// <summary>A comment is no operation of the map: Ctrl+Z does not undo it, and it takes no place in the history.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_comment_of_a_flow_is_out_of_the_history_of_the_map(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var (_, flow, _) = await ChainAsync(project);
        var history = $"/api/maps/{project.RootMapId}/history";
        var before = (await facilitator.GetAsync(history)).Ok().GetProperty("undo").GetArrayLength();

        (await facilitator.PutAsync(CommentUrl(flow), new { text = "Dit pendant le passage.", language = "fr" }, 0)).Ok();

        Assert.Equal(before, (await facilitator.GetAsync(history)).Ok().GetProperty("undo").GetArrayLength());
        // The undo takes back the last flow created, never the comment written since.
        (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/undo")).Ok();
        Assert.Equal([(flow, "Dit pendant le passage.")], (await SaidAsync(facilitator, project.RootMapId)).Select(line => (line.Flow, line.Text)));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_flow_deleted_alone_or_with_its_step_keeps_its_comment_out_of_reach_and_finds_it_again_when_it_comes_back(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var (steps, first, second) = await ChainAsync(project);
        (await facilitator.PutAsync(CommentUrl(first), new { text = "La commande part.", language = "fr" }, 0)).Ok();
        (await facilitator.PutAsync(CommentUrl(first), new { text = "The order leaves.", language = "en" }, 0)).Ok();
        (await facilitator.PutAsync(CommentUrl(second), new { text = "La facture suit.", language = "fr" }, 0)).Ok();
        var all = await SaidAsync(facilitator, project.RootMapId);
        Assert.Equal(3, all.Count);

        // The first flow deleted alone: its comment is neither read nor written, the undo brings both back.
        (await facilitator.DeleteAsync($"/api/flows/{first}", await project.FlowVersionAsync(first))).Ok();
        Assert.Equal([second], (await SaidAsync(facilitator, project.RootMapId)).Select(line => line.Flow));
        (await facilitator.PutAsync(CommentUrl(first), new { text = "Autre.", language = "fr" }, 1)).Refused(404, "INTROUVABLE");
        (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/undo")).Ok();
        Assert.Equal(all, await SaidAsync(facilitator, project.RootMapId));

        // The last step deleted takes the second flow with it, and its comment waits with it.
        (await facilitator.DeleteAsync($"/api/steps/{steps[2]}", await project.StepVersionAsync(steps[2]))).Ok();
        Assert.Equal([first, first], (await SaidAsync(facilitator, project.RootMapId)).Select(line => line.Flow));
        (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/undo")).Ok();
        Assert.Equal(all, await SaidAsync(facilitator, project.RootMapId));
        // Back, each line is written again over the version it had.
        var again = (await facilitator.PutAsync(CommentUrl(second), new { text = "La facture suit la livraison.", language = "fr" }, 1)).Ok();
        Assert.Equal(2, again.GetProperty("comments")[0].GetProperty("version").GetInt32());
    }

    /// <summary>A flow keeps its comment when it is reversed or relabelled: its key is the flow, and its text is the one of the person.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_flow_reversed_or_relabelled_keeps_its_comment(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var (_, flow, _) = await ChainAsync(project);
        (await facilitator.PutAsync(CommentUrl(flow), new { text = "La commande part.", language = "fr" }, 0)).Ok();

        (await facilitator.PatchAsync($"/api/flows/{flow}", new { label = "urgent" }, await project.FlowVersionAsync(flow))).Ok();
        (await facilitator.PostAsync($"/api/flows/{flow}/reverse", null, await project.FlowVersionAsync(flow))).Ok();

        Assert.Equal([(flow, "fr", "La commande part.", "manual", 1)], await SaidAsync(facilitator, project.RootMapId));
        Assert.Equal(2, (await facilitator.PutAsync(CommentUrl(flow), new { text = "La commande revient.", language = "fr" }, 1)).Ok()
            .GetProperty("comments")[0].GetProperty("version").GetInt32());
    }

    /// <summary>
    /// The creation of a commented flow undone then done again (Ctrl+Z, Ctrl+Y): nothing is refused, the comment waits out of
    /// reach meanwhile, and the flow that comes back finds it again (ADR 099, point 16).
    /// </summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_creation_of_a_commented_flow_is_undone_then_redone_and_the_flow_finds_its_comment_again(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        Guid[] steps = [(await project.AddStepAsync(0, "Commander")).Id, (await project.AddStepAsync(1, "Livrer")).Id];
        var flow = await project.AddFlowAsync(steps[0], steps[1]);
        (await facilitator.PutAsync(CommentUrl(flow), new { text = "La commande part.", language = "fr" }, 0)).Ok();
        (await facilitator.PutAsync(CommentUrl(flow), new { text = "The order leaves.", language = "en" }, 0)).Ok();
        var said = await SaidAsync(facilitator, project.RootMapId);
        Assert.Equal([(flow, "en"), (flow, "fr")], said.Select(line => (line.Flow, line.Language)));

        // The undo takes back the creation of the flow, the last operation of the map: its comment is neither read nor written.
        (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/undo")).Ok();
        Assert.Empty(StructureCalls.FlowPairs(await project.MapAsync()));
        Assert.Empty(await SaidAsync(facilitator, project.RootMapId));
        (await facilitator.PutAsync(CommentUrl(flow), new { text = "Autre.", language = "fr" }, 1)).Refused(404, "INTROUVABLE");

        (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/redo")).Ok();
        Assert.Equal([(steps[0], steps[1])], StructureCalls.FlowPairs(await project.MapAsync()));
        Assert.Equal(said, await SaidAsync(facilitator, project.RootMapId));
    }

    /// <summary>
    /// A commented flow between two steps written in two languages, reversed: it keeps its lines, and its original becomes
    /// the line in the language of origin of its new source step (ADR 099, point 16). The original is told from a
    /// translation by what an empty text removes: a translation alone, the whole comment with the original (point 13).
    /// </summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_commented_flow_reversed_between_steps_of_two_languages_keeps_its_lines_and_its_original_follows_its_new_source(EngineKind kind)
    {
        using var admin = (await hosts.OfAsync(kind)).As(Logins.Admin);
        // No route writes a step in another language than the one of its project: such steps come from a package.
        var root = (await admin.SendAsync(HttpMethod.Post, $"/api/projects/import?code={ApiProject.NewCode()}&with_members=false", TestPackages.TwoLanguages("PKG"), null))
            .Ok(201).GetProperty("root_map_id").GetGuid();
        async Task<JsonElement> FlowAsync() => (await admin.GetAsync($"/api/maps/{root}")).Ok().GetProperty("map").GetProperty("flows").EnumerateArray().Single();
        var flow = (await FlowAsync()).GetProperty("id").GetGuid();
        var source = (await FlowAsync()).GetProperty("source_step_id").GetGuid();
        (await admin.PutAsync(CommentUrl(flow), new { text = "La commande part.", language = "fr" }, 0)).Ok();
        (await admin.PutAsync(CommentUrl(flow), new { text = "The order leaves.", language = "en" }, 0)).Ok();
        // From the French step to the English one, the English line is a translation: emptied, it goes alone.
        var alone = (await admin.PutAsync(CommentUrl(flow), new { text = "", language = "en" }, 1)).Ok();
        Assert.Equal([(flow, "fr", "La commande part.", "manual", 1)], alone.GetProperty("comments").EnumerateArray().Select(Line));
        (await admin.PutAsync(CommentUrl(flow), new { text = "The order leaves.", language = "en" }, 0)).Ok();
        var said = await SaidAsync(admin, root);

        (await admin.PostAsync($"/api/flows/{flow}/reverse", null, (await FlowAsync()).GetProperty("version").GetInt32())).Ok();

        Assert.Equal(source, (await FlowAsync()).GetProperty("target_step_id").GetGuid());
        Assert.Equal([(flow, "en", "The order leaves.", "manual", 1), (flow, "fr", "La commande part.", "manual", 1)], await SaidAsync(admin, root));
        Assert.Equal(said, await SaidAsync(admin, root));
        // The flow now leaves the English step: the English line is its original, and emptied it takes the whole comment with it.
        Assert.Empty((await admin.PutAsync(CommentUrl(flow), new { text = "", language = "en" }, 1)).Ok().GetProperty("comments").EnumerateArray());
        Assert.Empty(await SaidAsync(admin, root));
    }

    /// <summary>
    /// A step inserted on a commented flow: the flow goes to the trash with its comment, the two flows that take its place
    /// have none, and the undo gives the flow back with its comment (ADR 099, point 16).
    /// </summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_step_inserted_on_a_commented_flow_sends_it_to_the_trash_with_its_comment_and_the_undo_gives_both_back(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var (first, last) = ((await project.AddStepAsync(0, "Commander")).Id, (await project.AddStepAsync(1, "Payer")).Id);
        var flow = await project.AddFlowAsync(first, last);
        (await facilitator.PutAsync(CommentUrl(flow), new { text = "La commande part au paiement.", language = "fr" }, 0)).Ok();
        (await facilitator.PutAsync(CommentUrl(flow), new { text = "The order goes to payment.", language = "en" }, 0)).Ok();
        var said = await SaidAsync(facilitator, project.RootMapId);
        Assert.Equal(2, said.Count);

        var inserted = (await facilitator.PostAsync($"/api/steps/{first}/insert", new { position = "after", label = "Contrôler" })).Ok().GetProperty("id").GetGuid();

        // Two new flows took the place of the commented one: none of them has a comment, and its own is out of reach.
        var map = await project.MapAsync();
        Assert.Equal([(first, inserted), (inserted, last)], StructureCalls.FlowPairs(map).OrderBy(pair => pair.Source == first ? 0 : 1));
        Assert.DoesNotContain(map.GetProperty("flows").EnumerateArray(), item => item.GetProperty("id").GetGuid() == flow);
        Assert.Empty(await SaidAsync(facilitator, project.RootMapId));
        (await facilitator.PutAsync(CommentUrl(flow), new { text = "Autre.", language = "fr" }, 1)).Refused(404, "INTROUVABLE");

        (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/undo")).Ok();
        Assert.Equal([(first, last)], StructureCalls.FlowPairs(await project.MapAsync()));
        Assert.Equal(said, await SaidAsync(facilitator, project.RootMapId));
    }

    /// <summary>A project in the trash is purged with the comments of its flows: they go before the flows, or the purge would be refused by the database.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_project_is_purged_with_the_comments_of_its_flows_those_of_a_flow_in_the_trash_too(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        using var admin = project.Admin;
        var (steps, first, second) = await ChainAsync(project);
        (await facilitator.PutAsync(CommentUrl(first), new { text = "La commande part.", language = "fr" }, 0)).Ok();
        (await facilitator.PutAsync(CommentUrl(first), new { text = "The order leaves.", language = "en" }, 0)).Ok();
        (await facilitator.PutAsync(CommentUrl(second), new { text = "La facture suit.", language = "fr" }, 0)).Ok();
        (await facilitator.PutAsync($"/api/steps/{steps[0]}/presentation-comment", new { text = "Ici on commande.", language = "fr" }, 0)).Ok();
        (await facilitator.DeleteAsync($"/api/flows/{second}", await project.FlowVersionAsync(second))).Ok();
        Assert.Equal("", (await facilitator.DeleteAsync($"/api/projects/{project.Id}?confirmation={project.Code}", await project.ProjectVersionAsync())).Body);

        var purged = await admin.DeleteAsync($"/api/trash/{project.Id}");

        Assert.Equal((204, ""), (purged.Status, purged.Body));
        (await admin.GetAsync($"/api/maps/{project.RootMapId}/flow-presentation-comments")).Refused(404, "INTROUVABLE");
    }

    /// <summary>« Dupliquer » goes through a `.pmap` copy, which carries no comment of presentation (ADR 099, point 7): the copy has none on its flows.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_duplicated_project_has_no_comment_on_its_flows(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        using var admin = project.Admin;
        var (_, flow, _) = await ChainAsync(project);
        (await facilitator.PutAsync(CommentUrl(flow), new { text = "La commande part.", language = "fr" }, 0)).Ok();
        var copy = (await facilitator.GetAsync($"/api/projects/{project.Id}/pmap")).Body;
        Assert.DoesNotContain("La commande part.", copy, StringComparison.Ordinal);

        var duplicated = (await admin.PostAsync($"/api/projects/{project.Id}/duplicate", new { code = ApiProject.NewCode() })).Ok(201);

        var root = (await admin.GetAsync($"/api/projects/{duplicated.GetProperty("id").GetGuid()}")).Ok().GetProperty("root_map_id").GetGuid();
        Assert.Equal(2, (await admin.GetAsync($"/api/maps/{root}")).Ok().GetProperty("map").GetProperty("flows").GetArrayLength());
        Assert.Empty(await SaidAsync(admin, root));
        Assert.Single(await SaidAsync(facilitator, project.RootMapId));
    }
}
