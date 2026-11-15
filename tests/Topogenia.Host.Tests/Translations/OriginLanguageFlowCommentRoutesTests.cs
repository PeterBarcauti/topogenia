// OriginLanguageFlowCommentRoutesTests.cs
// The comments of presentation of the flows at the change of the language of origin of a project, through the whole host on both databases (ADR 100, ADR 099 lot J10): one without its line in the language asked is a text that lacks; translated, the change passes and its line in the new language is its original.

using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Host.Tests.Steps;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Translations;

public sealed class OriginLanguageFlowCommentRoutesTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private static string CommentUrl(Guid flow) => $"/api/flows/{flow}/presentation-comment";

    private static async Task<ApiResponse> ChangeAsync(ApiClient client, ApiProject project, string language) =>
        await client.PutAsync($"/api/projects/{project.Id}/origin-language", new { language },
            (await client.GetAsync($"/api/projects/{project.Id}")).Ok().GetProperty("version").GetInt32());

    private static async Task<List<(Guid Flow, string Language)>> LinesAsync(ApiClient client, ApiProject project) =>
        [.. (await client.GetAsync($"/api/maps/{project.RootMapId}/flow-presentation-comments")).Ok().EnumerateArray()
            .Select(line => (line.GetProperty("flow_id").GetGuid(), line.GetProperty("language").GetString()!))
            .OrderBy(line => line.Item2, StringComparer.Ordinal)];

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_comment_of_a_flow_without_its_line_in_the_language_is_a_text_that_lacks_and_translated_the_change_passes(EngineKind kind)
    {
        var project = await ApiProject.CreateAsync(await hosts.OfAsync(kind));
        using var facilitator = project.Facilitator;
        var (first, second) = (await project.AddStepAsync(0, "Demander"), await project.AddStepAsync(1, "Valider"));
        var flow = await project.AddFlowAsync(first.Id, second.Id, "oui");
        await ProjectTranslations.TranslateAllAsync(facilitator, project.Id, "en");
        (await facilitator.PutAsync(CommentUrl(flow), new { text = "La demande part chez le responsable.", language = "fr" }, 0)).Ok();

        var refusal = (await ChangeAsync(facilitator, project, "en")).Refused(409, "TRADUCTION_INCOMPLETE");

        // Every text of the closed core is translated, the label of the flow too: its comment alone lacks, named by its flow.
        Assert.Equal(("en", 1), (refusal.GetProperty("language").GetString(), refusal.GetProperty("count").GetInt32()));
        var missing = Assert.Single(refusal.GetProperty("missing").EnumerateArray());
        Assert.Equal(("flow_presentation_comment", flow, $"{first.Code} > {second.Code}"),
            (missing.GetProperty("type").GetString(), missing.GetProperty("id").GetGuid(), missing.GetProperty("code").GetString()));
        Assert.Equal("fr", (await facilitator.GetAsync($"/api/projects/{project.Id}")).Ok().GetProperty("source_language").GetString());

        (await facilitator.PutAsync(CommentUrl(flow), new { text = "The request goes to the manager.", language = "en" }, 0)).Ok();
        Assert.Equal("en", (await ChangeAsync(facilitator, project, "en")).Ok().GetProperty("source_language").GetString());

        // Both lines stay. The project is in English now: the English line is the original, and emptied it takes the French one with it.
        Assert.Equal([(flow, "en"), (flow, "fr")], await LinesAsync(facilitator, project));
        (await facilitator.PutAsync(CommentUrl(flow), new { text = "", language = "en" }, 1)).Ok();
        Assert.Empty(await LinesAsync(facilitator, project));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_text_a_comment_of_a_step_and_a_comment_of_a_flow_that_lack_are_refused_together_in_that_order(EngineKind kind)
    {
        var project = await ApiProject.CreateAsync(await hosts.OfAsync(kind));
        using var facilitator = project.Facilitator;
        var (first, second) = (await project.AddStepAsync(0, "Demander"), await project.AddStepAsync(1, "Valider"));
        var flow = await project.AddFlowAsync(first.Id, second.Id);
        (await facilitator.PutAsync(CommentUrl(flow), new { text = "La demande part chez le responsable.", language = "fr" }, 0)).Ok();
        (await facilitator.PutAsync($"/api/steps/{second.Id}/presentation-comment", new { text = "Le responsable valide.", language = "fr" }, 0)).Ok();

        var refusal = (await ChangeAsync(facilitator, project, "en")).Refused(409, "TRADUCTION_INCOMPLETE");

        var missing = refusal.GetProperty("missing").EnumerateArray().Select(text => (text.GetProperty("type").GetString()!, text.GetProperty("id").GetGuid())).ToList();
        Assert.Equal(refusal.GetProperty("count").GetInt32(), missing.Count);
        Assert.Contains(("step", second.Id), missing);
        Assert.Equal([("presentation_comment", second.Id), ("flow_presentation_comment", flow)], missing[^2..]);
    }
}
