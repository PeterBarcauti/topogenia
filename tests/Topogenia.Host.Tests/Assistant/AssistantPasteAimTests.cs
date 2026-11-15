// AssistantPasteAimTests.cs
// A reply pasted from the AI of the person is read against the prompt that was copied, its aim kept: the language of a translation (ADR 083), the maps of summaries (plan 3.1, lot R); without them the prompt read again is another one.

using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Assistant;

public sealed class AssistantPasteAimTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private const string Answer = """{"form":"answer","message":"Le processus traduit.","cited":[],"questions":[],"findings":[],"pmap":null}""";

    private static async Task CopyPasteAsync(ApiHost host)
    {
        using var admin = host.As(Logins.Admin);
        var version = (await admin.GetAsync("/api/admin/assistant")).Ok().GetProperty("version").GetInt32();
        (await admin.PutAsync("/api/admin/assistant", new { connector = "copy_paste", allowed_classifications = new[] { "internal" } }, version)).Ok();
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_pasted_reply_of_a_translation_is_read_with_its_language(EngineKind kind)
    {
        var host = await hosts.OfAsync(kind);
        await CopyPasteAsync(host);
        var project = await ApiProject.CreateAsync(host);
        using var facilitator = project.Facilitator;
        var url = $"/api/maps/{project.RootMapId}/assistant/replies";
        var read = (await facilitator.PostAsync(url, new { intent = "translate", request = "Traduire le processus", language = "en", reply = Answer })).Ok();
        Assert.Equal("answer", read.GetProperty("reply").GetProperty("form").GetString());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_pasted_reply_of_summaries_is_read_with_its_maps_even_when_they_are_up_to_date(EngineKind kind)
    {
        var host = await hosts.OfAsync(kind);
        await CopyPasteAsync(host);
        var project = await ApiProject.CreateAsync(host);
        using var facilitator = project.Facilitator;
        (await facilitator.PutAsync($"/api/maps/{project.RootMapId}/summaries", new { summaries = new[] { new { map_id = project.RootMapId, text = "Une carte résumée." } } })).Ok();
        var code = (await facilitator.GetAsync($"/api/maps/{project.RootMapId}")).Ok().GetProperty("map").GetProperty("project_code").GetString();
        var reply = $$"""{"form":"summaries","message":"Une carte.","cited":[],"questions":[],"findings":[],"pmap":null,"report":null,"summaries":[{"map":"{{code}}","text":"La carte refaite."}]}""";
        var url = $"/api/maps/{project.RootMapId}/assistant/replies";
        var read = (await facilitator.PostAsync(url, new { intent = "summaries", request = "Refais le résumé.", maps = new[] { project.RootMapId }, reply })).Ok();
        var summary = read.GetProperty("reply").GetProperty("summaries")[0];
        Assert.Equal(("La carte refaite.", "Une carte résumée.", 1),
            (summary.GetProperty("text").GetString(), summary.GetProperty("current").GetString(), summary.GetProperty("version").GetInt32()));
        // Asked for the maps without summary or stale, the same reply finds none: every map is up to date.
        (await facilitator.PostAsync(url, new { intent = "summaries", request = "Résume les cartes.", reply })).Refused(409, "RESUMES_A_JOUR");
        // A reply whose sentences name no map asked has nothing to review: an invalid reply, not a message alone (audit 3.1 V2, 4.6.3).
        var elsewhere = reply.Replace($"\"map\":\"{code}\"", "\"map\":\"XYZ-E999\"", StringComparison.Ordinal);
        var refused = (await facilitator.PostAsync(url, new { intent = "summaries", request = "Refais le résumé.", maps = new[] { project.RootMapId }, reply = elsewhere })).Ok();
        Assert.Equal("invalid_reply", refused.GetProperty("failure").GetString());
    }
}
