// AssistantPanelTests.cs
// The routes of the panel of the assistant through the whole host on both databases: its state, the exchange written as a stream of JSON lines, the refusals before the stream, the context sent, the proposal corrected once, the prompt copied, the reply pasted, the home page, the audit without any text (spec 3.0 sections 3 to 8).

using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Topogenia.Application.Assistant;
using Topogenia.Application.Ports;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Assistant;

public sealed class AssistantPanelTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private const string Key = "sk-ant-test-panel-0123456789";

    private const string Answer = """{"form":"answer","message":"La carte compte une étape.","cited":[],"questions":[],"findings":[],"pmap":null}""";

    private const string ValidPmap = """
        {"format":"topogenia","version":"1.1","project":{"name":"Demande d'achat"},"map":{"lanes":[{"id":"dem","name":"Demandeur"}],
         "steps":[{"id":"s1","lane":"dem","label":"Saisir la demande","column":0},{"id":"s2","lane":"dem","label":"Envoyer la demande","column":1}],
         "flows":[{"source":"s1","target":"s2"}]},"uncertainties":["Le délai n'est pas connu."]}
        """;

    private const string InvalidPmap = """
        {"format":"topogenia","version":"1.1","project":{"name":"Demande d'achat"},"map":{"lanes":[{"id":"dem","name":"Demandeur"}],
         "steps":[{"id":"s1","lane":"inconnu","label":"Saisir la demande"}],"flows":[]}}
        """;

    private static string Proposal(string pmap) => JsonSerializer.Serialize(new { form = "proposal", message = "Deux étapes.", cited = Array.Empty<string>(),
        questions = Array.Empty<object>(), findings = Array.Empty<object>(), pmap });

    private static TestConnector Anthropic(ApiHost host) =>
        host.Services.GetServices<IAssistantConnector>().OfType<TestConnector>().Single(item => item.Kind == AssistantConnector.Anthropic);

    /// <summary>The settings of the test: the connector given, the key kept for the API of Anthropic, the classifications allowed.</summary>
    private static async Task SetAsync(ApiHost host, string connector, string[]? allowed = null)
    {
        using var admin = host.As(Logins.Admin);
        var version = (await admin.GetAsync("/api/admin/assistant")).Ok().GetProperty("version").GetInt32();
        (await admin.PutAsync("/api/admin/assistant", new
        {
            connector, secret = connector == "anthropic" ? Key : null, model = connector == "bedrock" ? "global.anthropic.claude-sonnet-5-5" : null,
            region = connector == "bedrock" ? "eu-west-3" : null, allowed_classifications = allowed ?? ["internal"],
        }, version)).Ok();
    }

    /// <summary>The lines of a stream of an exchange.</summary>
    private static List<JsonElement> Lines(ApiResponse response)
    {
        Assert.Equal(200, response.Status);
        return [.. response.Body.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => JsonDocument.Parse(line).RootElement.Clone())];
    }

    private static object Ask(string request, string intent = "free", string[]? selection = null, object[]? history = null) =>
        new { intent, request, selection = selection ?? [], history = history ?? [] };

    private static async Task<(ApiProject Project, string StepCode)> ProjectWithStepAsync(ApiHost host)
    {
        var project = await ApiProject.CreateAsync(host);
        using var facilitator = project.Facilitator;
        var step = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/steps", new { lane_id = project.FirstLaneId, column = 0, label = "Commander" })).Ok();
        var code = step.GetProperty("map").GetProperty("steps")[0].GetProperty("code").GetString()!;
        return (project, code);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_state_says_the_mode_where_the_data_go_and_what_each_member_may_ask(EngineKind kind)
    {
        var host = await hosts.OfAsync(kind);
        var project = await ApiProject.CreateAsync(host);
        await SetAsync(host, "copy_paste");
        using var reader = project.Reader;
        using var facilitator = project.Facilitator;
        using var stranger = project.Stranger;
        var copying = (await reader.GetAsync($"/api/maps/{project.RootMapId}/assistant")).Ok();
        Assert.Equal(("copy_paste", false, true, false, "project"), (copying.GetProperty("mode").GetString(), copying.GetProperty("can_propose").GetBoolean(),
            copying.GetProperty("empty_map").GetBoolean(), copying.GetProperty("shows_cost").GetBoolean(), copying.GetProperty("destination").GetProperty("scope").GetString()));
        await SetAsync(host, "anthropic");
        var model = (await facilitator.GetAsync($"/api/maps/{project.RootMapId}/assistant")).Ok();
        Assert.Equal(("model", true, true, "anthropic", "claude-opus-5-5", "internal"), (model.GetProperty("mode").GetString(), model.GetProperty("can_propose").GetBoolean(),
            model.GetProperty("allowed").GetBoolean(), model.GetProperty("destination").GetProperty("connector").GetString(),
            model.GetProperty("destination").GetProperty("model").GetString(), model.GetProperty("destination").GetProperty("classification").GetString()));
        (await stranger.GetAsync($"/api/maps/{project.RootMapId}/assistant")).Refused(404, "INTROUVABLE");
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task An_exchange_streams_its_start_its_progress_and_its_reply_and_is_audited_without_its_texts(EngineKind kind)
    {
        var host = await hosts.OfAsync(kind);
        var (project, code) = await ProjectWithStepAsync(host);
        await SetAsync(host, "anthropic");
        var connector = Anthropic(host);
        connector.Next($$"""{"form":"answer","message":"La première étape est {{code}}.","cited":["{{code}}"],"questions":[],"findings":[],"pmap":null}""");
        using var facilitator = project.Facilitator;
        var lines = Lines(await facilitator.PostAsync($"/api/maps/{project.RootMapId}/assistant/exchanges", Ask("Quelle est la première étape ?", "ask", [code, "XXXX-E999"])));
        Assert.Equal("started", lines[0].GetProperty("type").GetString());
        Assert.Equal(("anthropic", "project"), (lines[0].GetProperty("destination").GetProperty("connector").GetString(), lines[0].GetProperty("destination").GetProperty("scope").GetString()));
        Assert.Contains(lines, line => line.GetProperty("type").GetString() == "progress" && line.GetProperty("phase").GetString() == "thinking");
        var reply = lines[^1];
        Assert.Equal(("reply", "answer"), (reply.GetProperty("type").GetString(), reply.GetProperty("reply").GetProperty("form").GetString()));
        var named = Assert.Single(reply.GetProperty("reply").GetProperty("objects").EnumerateArray());
        Assert.Equal((code, "step", project.RootMapId), (named.GetProperty("code").GetString(), named.GetProperty("kind").GetString(), named.GetProperty("map_id").GetGuid()));
        // The facilitator is no administrator: no cost is shown.
        Assert.Equal(JsonValueKind.Null, reply.GetProperty("cost").ValueKind);

        var prompt = connector.Prompts[^1];
        Assert.Equal((AssistantIntent.Ask, AssistantScope.Project, AssistantSchemaFormat.Package), (prompt.Intent, prompt.Scope, prompt.SchemaFormat));
        Assert.Equal([code], prompt.Selection);
        Assert.Contains(AssistantForm.Proposal, prompt.Forms);
        Assert.Contains("=== maps.json ===", prompt.Schema!, StringComparison.Ordinal);
        Assert.DoesNotContain("=== members.json ===", prompt.Schema!, StringComparison.Ordinal);
        Assert.Contains(code, prompt.OpenMap!, StringComparison.Ordinal);
        // The languages a translation takes (ADR 081): the one of origin of the project and those configured.
        Assert.Equal("fr", prompt.Languages!.Origin);
        Assert.Contains("en", prompt.Languages.Configured);
        Assert.Equal(Key, connector.Connections[^1].Secret);

        using var admin = host.As(Logins.Admin);
        var audit = (await admin.GetAsync("/api/admin/audit?kind=assistant.exchanged")).Ok().EnumerateArray().First();
        var details = audit.GetProperty("details").GetString()!;
        Assert.Contains("\"form\":\"answer\"", details, StringComparison.Ordinal);
        Assert.Contains("\"cache_write_tokens\":80000", details, StringComparison.Ordinal);
        Assert.DoesNotContain("première", details, StringComparison.Ordinal);
        Assert.DoesNotContain(Key, details, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_reader_asks_with_the_pmap_of_the_project_and_cannot_have_a_proposal(EngineKind kind)
    {
        var host = await hosts.OfAsync(kind);
        var (project, _) = await ProjectWithStepAsync(host);
        await SetAsync(host, "anthropic");
        var connector = Anthropic(host);
        using var reader = project.Reader;
        connector.Next(Answer);
        var answered = Lines(await reader.PostAsync($"/api/maps/{project.RootMapId}/assistant/exchanges", Ask("Que fait-on ?")));
        Assert.Equal("reply", answered[^1].GetProperty("type").GetString());
        var prompt = connector.Prompts[^1];
        Assert.Equal(AssistantSchemaFormat.Pmap, prompt.SchemaFormat);
        Assert.DoesNotContain(AssistantForm.Proposal, prompt.Forms);
        connector.Next(Proposal(ValidPmap));
        var refused = Lines(await reader.PostAsync($"/api/maps/{project.RootMapId}/assistant/exchanges", Ask("Crée une étape.")));
        Assert.Equal(("failure", "form_refused"), (refused[^1].GetProperty("type").GetString(), refused[^1].GetProperty("failure").GetString()));
        (await reader.PostAsync($"/api/maps/{project.RootMapId}/assistant/exchanges", Ask("Change la carte.", "modify"))).Refused(403, "ACCES_REFUSE");
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_refusals_come_before_the_stream_as_ordinary_errors_and_nothing_is_sent(EngineKind kind)
    {
        var host = await hosts.OfAsync(kind);
        var project = await ApiProject.CreateAsync(host);
        using var facilitator = project.Facilitator;
        using var stranger = project.Stranger;
        var url = $"/api/maps/{project.RootMapId}/assistant/exchanges";
        var connector = Anthropic(host);
        await SetAsync(host, "copy_paste");
        var before = connector.Prompts.Count;
        (await facilitator.PostAsync(url, Ask("Bonjour"))).Refused(400, AssistantErrors.Unavailable);
        await SetAsync(host, "anthropic", allowed: []);
        (await facilitator.PostAsync(url, Ask("Bonjour"))).Refused(403, AssistantErrors.ClassificationRefused);
        await SetAsync(host, "anthropic");
        (await facilitator.PostAsync(url, Ask("   "))).Refused(400, "CHAMP_OBLIGATOIRE");
        (await facilitator.PostAsync(url, Ask(new string('a', AssistantBounds.RequestMax + 1)))).Refused(400, "TEXTE_TROP_LONG");
        (await facilitator.PostAsync(url, Ask("Bonjour", "unknown"))).Refused(400, "VALEUR_INVALIDE");
        (await stranger.PostAsync(url, Ask("Bonjour"))).Refused(404, "INTROUVABLE");
        Assert.Equal(before, connector.Prompts.Count);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_proposal_refused_by_the_format_is_sent_back_once_with_its_faults(EngineKind kind)
    {
        var host = await hosts.OfAsync(kind);
        var project = await ApiProject.CreateAsync(host);
        await SetAsync(host, "anthropic");
        var connector = Anthropic(host);
        using var facilitator = project.Facilitator;
        connector.Next(Proposal(InvalidPmap));
        connector.Next(Proposal(ValidPmap));
        var corrected = Lines(await facilitator.PostAsync($"/api/maps/{project.RootMapId}/assistant/exchanges", Ask("Décris la demande d'achat.", "create")));
        Assert.Contains(corrected, line => line.GetProperty("type").GetString() == "progress" && line.GetProperty("phase").GetString() == "correcting");
        var proposal = corrected[^1].GetProperty("reply").GetProperty("proposal");
        Assert.Equal((1, 1, 2, 1, 0), (proposal.GetProperty("maps").GetInt32(), proposal.GetProperty("lanes").GetInt32(), proposal.GetProperty("steps").GetInt32(),
            proposal.GetProperty("flows").GetInt32(), proposal.GetProperty("faults").GetArrayLength()));
        Assert.Equal("Le délai n'est pas connu.", proposal.GetProperty("uncertainties")[0].GetString());
        Assert.StartsWith("The application refused", connector.Prompts[^1].Request, StringComparison.Ordinal);
        Assert.Contains("map.steps[0].lane", connector.Prompts[^1].Request, StringComparison.Ordinal);

        connector.Next(Proposal(InvalidPmap));
        connector.Next(Proposal(InvalidPmap));
        var still = Lines(await facilitator.PostAsync($"/api/maps/{project.RootMapId}/assistant/exchanges", Ask("Décris la demande d'achat.", "create")));
        var fault = still[^1].GetProperty("reply").GetProperty("proposal").GetProperty("faults")[0];
        Assert.Equal("map.steps[0].lane", fault.GetProperty("path").GetString());
        Assert.False(string.IsNullOrEmpty(fault.GetProperty("message").GetString()));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_copy_and_paste_copies_the_whole_prompt_and_reads_a_reply_pasted(EngineKind kind)
    {
        var host = await hosts.OfAsync(kind);
        var (project, code) = await ProjectWithStepAsync(host);
        await SetAsync(host, "copy_paste");
        using var facilitator = project.Facilitator;
        var prompt = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/assistant/prompt", Ask("Résume la carte.", "modify"))).Ok().GetProperty("prompt").GetString()!;
        foreach (var part in new[] { "## docs/pmap/PMAP_GUIDE_IA.md", "# The schema of the project", code, "Résume la carte.", "# The schema of your reply" })
            Assert.Contains(part, prompt, StringComparison.Ordinal);
        var url = $"/api/maps/{project.RootMapId}/assistant/replies";
        var fenced = (await facilitator.PostAsync(url, new { intent = "modify", request = "Résume la carte.", reply = $"Voici :\n```json\n{Answer}\n```" })).Ok();
        Assert.Equal("answer", fenced.GetProperty("reply").GetProperty("form").GetString());
        var bare = (await facilitator.PostAsync(url, new { intent = "create", request = "Décris.", reply = ValidPmap })).Ok();
        Assert.Equal(("proposal", 2), (bare.GetProperty("reply").GetProperty("form").GetString(), bare.GetProperty("reply").GetProperty("proposal").GetProperty("steps").GetInt32()));
        var garbage = (await facilitator.PostAsync(url, new { intent = "ask", request = "Quoi ?", reply = "Je ne sais pas." })).Ok();
        Assert.Equal("invalid_reply", garbage.GetProperty("failure").GetString());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_home_page_describes_a_project_for_an_administrator_only(EngineKind kind)
    {
        var host = await hosts.OfAsync(kind);
        await SetAsync(host, "anthropic");
        var connector = Anthropic(host);
        using var admin = host.As(Logins.Admin);
        using var facilitator = host.As(Logins.Facilitator);
        connector.Next(Proposal(ValidPmap));
        var lines = Lines(await admin.PostAsync("/api/assistant/exchanges", Ask("Décris la demande d'achat.")));
        var reply = lines[^1];
        Assert.Equal(("proposal", "new_project"), (reply.GetProperty("reply").GetProperty("form").GetString(), lines[0].GetProperty("destination").GetProperty("scope").GetString()));
        // The administrator sees what the exchange cost.
        Assert.Equal(80000, reply.GetProperty("cost").GetProperty("cache_write_tokens").GetInt32());
        var prompt = connector.Prompts[^1];
        Assert.Equal((AssistantScope.NewProject, (string?)null, (string?)null), (prompt.Scope, prompt.Schema, prompt.OpenMap));
        (await facilitator.PostAsync("/api/assistant/exchanges", Ask("Décris."))).Refused(403, "ACCES_REFUSE");
    }

    [Fact]
    public async Task A_prompt_past_its_bound_is_refused_before_anything_is_sent()
    {
        var host = await hosts.OfAsync(EngineKind.Sqlite);
        var project = await ApiProject.CreateAsync(host);
        await SetAsync(host, "anthropic");
        var connector = Anthropic(host);
        var before = connector.Prompts.Count;
        // Twenty-five former turns of 100 000 characters: 2 500 000 characters, past the bound of a prompt.
        var history = Enumerable.Repeat((object)new { request = "Q", reply = new string('r', 100_000) }, 25).ToArray();
        using var facilitator = project.Facilitator;
        (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/assistant/exchanges", Ask("Bonjour", history: history)))
            .Refused(413, AssistantErrors.ContentTooLarge);
        Assert.Equal(before, connector.Prompts.Count);
    }

    [Fact]
    public async Task A_long_exchange_tells_it_works_at_least_every_two_seconds()
    {
        var host = await hosts.OfAsync(EngineKind.Sqlite);
        var project = await ApiProject.CreateAsync(host);
        await SetAsync(host, "anthropic");
        var connector = Anthropic(host);
        connector.Delay = TimeSpan.FromSeconds(4.5);
        try
        {
            connector.Next(Answer);
            using var facilitator = project.Facilitator;
            var lines = Lines(await facilitator.PostAsync($"/api/maps/{project.RootMapId}/assistant/exchanges", Ask("Bonjour")));
            var beats = lines.Where(line => line.GetProperty("type").GetString() == "progress").Select(line => line.GetProperty("elapsed_milliseconds").GetInt32()).ToList();
            Assert.Contains(beats, elapsed => elapsed >= 2000);
            Assert.Contains(beats, elapsed => elapsed >= 4000);
            Assert.Equal("reply", lines[^1].GetProperty("type").GetString());
        }
        finally
        {
            connector.Delay = TimeSpan.Zero;
        }
    }
}
