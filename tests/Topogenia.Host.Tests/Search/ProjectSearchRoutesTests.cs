// ProjectSearchRoutesTests.cs
// The search in the whole project through the whole host on both databases (ADR 110): what each role finds, the tools as the copy of the map for the view shows them, nothing written, the refusals, the word kept out of the log, the limit of the body.

using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Topogenia.Host.Tests.Api;
using Topogenia.Host.Tests.Dissolutions;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Host.Tests.Sheets;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Search;

/// <summary>
/// Each test draws its own project: a detailed row (<see cref="DetailedRow"/>: Before, the macro step detailed into First
/// and Second, After), a document of the macro step that holds the word, the contributor made actor of the first lane of
/// the root map (he fills Before and After, not First), and sheets whose tools hold the word: Before validated with one
/// tool, then drafted again with another tool and a tool in its procedure; After and First in draft only, never
/// validated, each with a tool of its own.
/// </summary>
public sealed class ProjectSearchRoutesTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    /// <summary>The limit of the body of the search, written here as the decision gives it (4 KB), not read from the code under test.</summary>
    private const int FourKilobytes = 4 * 1024;

    private static string Url(Guid projectId) => $"/api/projects/{projectId}/search";

    private static string SheetUrl(Guid stepId) => $"/api/steps/{stepId}/sheet";

    private static object Sheet(string tools, string? procedureTool = null) => new
    {
        fields = new { tools = new { value = tools } },
        procedure = procedureTool is null ? [] : new object[] { new { text = "Saisir", tool = procedureTool, nature = "re_enter" } },
    };

    private static async Task<JsonElement> SaveAsync(ApiClient client, Guid stepId, object sheet) =>
        (await client.PutAsync(SheetUrl(stepId), sheet, await StepSheets.ReadVersionAsync(client, stepId))).Ok();

    private async Task<DetailedRow> SceneAsync(EngineKind kind)
    {
        var project = await ApiProject.CreateAsync(await hosts.OfAsync(kind));
        var row = await DetailedRow.CreateAsync(project, "Tableur de suivi");
        await StepSheets.MakeContributorActorAsync(project);
        using var facilitator = project.Facilitator;
        var before = await SaveAsync(facilitator, row.Before, Sheet("Tableur validé"));
        (await facilitator.PostAsync($"{SheetUrl(row.Before)}/validate", null, StepSheets.VersionOf(before))).Ok();
        await SaveAsync(facilitator, row.Before, Sheet("Tableur brouillon", "Tableur du déroulé"));
        await SaveAsync(facilitator, row.After, Sheet("Tableur caché"));
        await SaveAsync(facilitator, row.First, Sheet("Tableur interne"));
        return row;
    }

    private static async Task<JsonElement> SearchAsync(ApiClient client, Guid projectId, string text, Guid? mapId = null) =>
        (await client.PostAsync(Url(projectId), new { text, language = "en", map_id = mapId })).Ok();

    private static IEnumerable<JsonElement> HitsOf(JsonElement map) => map.GetProperty("hits").EnumerateArray();

    /// <summary>The results of one kind on one map: the name of their step and the text found.</summary>
    private static IReadOnlyList<(string? Step, string? Text)> Found(JsonElement view, Guid mapId, string kind) =>
        [.. view.GetProperty("maps").EnumerateArray().Where(map => map.GetProperty("map_id").GetGuid() == mapId).SelectMany(HitsOf)
            .Where(hit => hit.GetProperty("kind").GetString() == kind)
            .Select(hit => (hit.GetProperty("step").GetProperty("text").GetString(), hit.GetProperty("text").GetString()))];

    /// <summary>The tools of the steps of a map (its own steps, not those of its sub-maps) in a copy of the map, which hold the word.</summary>
    private static IReadOnlyList<(string? Step, string? Text)> ToolsOfCopy(string copy, string word)
    {
        using var document = JsonDocument.Parse(copy);
        var tools = new List<(string?, string?)>();
        foreach (var step in document.RootElement.GetProperty("map").GetProperty("steps").EnumerateArray())
        {
            if (!step.TryGetProperty("sheet", out var sheet))
                continue;
            var texts = new List<string?>();
            if (sheet.TryGetProperty("tools", out var field))
                texts.Add(field.GetString());
            if (sheet.TryGetProperty("procedure", out var procedure))
                texts.AddRange(procedure.EnumerateArray().Select(task => task.TryGetProperty("tool", out var tool) ? tool.GetString() : null));
            tools.AddRange(texts.Where(text => text is not null && text.Contains(word, StringComparison.Ordinal)).Distinct(StringComparer.Ordinal)
                .Select(text => (step.GetProperty("label").GetString(), text)));
        }
        return tools;
    }

    private static IReadOnlyList<(string?, string?)> Sorted(IEnumerable<(string? Step, string? Text)> found) =>
        [.. found.OrderBy(item => item.Step, StringComparer.Ordinal).ThenBy(item => item.Text, StringComparer.Ordinal)];

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Each_role_finds_what_it_reads_the_reader_no_draft_the_contributor_his_drafts_the_facilitator_all(EngineKind kind)
    {
        var row = await SceneAsync(kind);
        var project = row.Project;
        (string?, string?)[] contributorTools = [("Commander", "Tableur brouillon"), ("Commander", "Tableur du déroulé"), ("Livrer", "Tableur caché")];
        var expected = new (ApiClient Client, (string?, string?)[] Root, (string?, string?)[] Detail)[]
        {
            (project.Reader, [("Commander", "Tableur validé")], []),
            (project.Contributor, contributorTools, []),
            (project.Facilitator, contributorTools, [("Valider", "Tableur interne")]),
            (project.Admin, contributorTools, [("Valider", "Tableur interne")]),
        };
        foreach (var (client, root, detail) in expected)
        {
            using var caller = client;
            var view = await SearchAsync(caller, project.Id, "tableur", row.SubMap);
            Assert.Equal(root, Found(view, project.RootMapId, "tool"));
            Assert.Equal(detail, Found(view, row.SubMap, "tool"));
            // A document is seen by every member, whatever his role.
            Assert.Equal([("Achats", "Tableur de suivi")], Found(view, project.RootMapId, "document"));
            // The map asked comes first when it holds a result; a map without result is not given.
            var maps = view.GetProperty("maps").EnumerateArray().Select(map => (map.GetProperty("map_id").GetGuid(), map.GetProperty("depth").GetInt32())).ToList();
            Assert.Equal(detail.Length == 0 ? [(project.RootMapId, 1)] : [(row.SubMap, 2), (project.RootMapId, 1)], maps);
            Assert.Equal(root.Length + detail.Length + 1, view.GetProperty("total").GetInt32());
        }

        using var reader = project.Reader;
        // Two words found in one text, in any order (R9): the place is the first mark, the word « Tableur »; each word
        // found is marked, in the order of the text (R9b).
        var hit = HitsOf((await SearchAsync(reader, project.Id, "VALIDE TABLEUR")).GetProperty("maps")[0]).First();
        Assert.Equal(["kind", "step_id", "lane_id", "text", "language", "start", "length", "marks", "step_code", "step", "status"],
            hit.EnumerateObject().Select(property => property.Name));
        Assert.Equal([(0, 7), (8, 6)], hit.GetProperty("marks").EnumerateArray()
            .Select(mark => (mark.GetProperty("start").GetInt32(), mark.GetProperty("length").GetInt32())));
        Assert.Equal(("tool", row.Before, "Tableur validé", "fr", 0, 7), (hit.GetProperty("kind").GetString(), hit.GetProperty("step_id").GetGuid(),
            hit.GetProperty("text").GetString(), hit.GetProperty("language").GetString(), hit.GetProperty("start").GetInt32(), hit.GetProperty("length").GetInt32()));
        Assert.Equal(("Commander", "fr"), (hit.GetProperty("step").GetProperty("text").GetString(), hit.GetProperty("step").GetProperty("source_language").GetString()));
        Assert.StartsWith(project.Code, hit.GetProperty("step_code").GetString(), StringComparison.Ordinal);
    }

    /// <summary>
    /// The rule of the sheets is called, never rewritten (ADR 110 point 2): for each role, on the root map and on the
    /// detailed map, the tools found are those that the copy of the map for the view in BPMN 2.0 shows.
    /// </summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_tools_found_are_those_the_copy_of_the_map_for_the_view_shows_for_each_role(EngineKind kind)
    {
        var row = await SceneAsync(kind);
        var project = row.Project;
        var counts = new List<int>();
        foreach (var client in new[] { project.Reader, project.Contributor, project.Facilitator, project.Admin })
        {
            using var caller = client;
            var view = await SearchAsync(caller, project.Id, "Tableur");
            foreach (var mapId in new[] { project.RootMapId, row.SubMap })
            {
                var copy = await caller.GetAsync($"/api/maps/{mapId}/pmap?purpose=view");
                Assert.Equal(200, copy.Status);
                var shown = ToolsOfCopy(copy.Body, "Tableur");
                Assert.Equal(Sorted(shown), Sorted(Found(view, mapId, "tool")));
                counts.Add(shown.Count);
            }
        }
        // The equivalence is not empty: the roles do not see the same tools.
        Assert.Equal([1, 0, 3, 0, 3, 1, 3, 1], counts);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_search_writes_no_event_of_audit_and_nothing_in_the_project(EngineKind kind)
    {
        var row = await SceneAsync(kind);
        var project = row.Project;
        using var admin = project.Admin;
        using var facilitator = project.Facilitator;
        using var reader = project.Reader;
        async Task<string> StateAsync() => string.Join("\n",
            (await admin.GetAsync($"/api/admin/audit?project_id={project.Id}&limit=1000")).Body,
            (await facilitator.GetAsync($"/api/projects/{project.Id}")).Body,
            (await facilitator.GetAsync($"/api/maps/{project.RootMapId}")).Body,
            (await facilitator.GetAsync($"/api/maps/{row.SubMap}")).Body,
            (await facilitator.GetAsync($"/api/maps/{project.RootMapId}/history")).Body,
            (await reader.GetAsync($"/api/maps/{project.RootMapId}/history")).Body,
            (await facilitator.GetAsync(SheetUrl(row.Before))).Body);
        var before = await StateAsync();

        await SearchAsync(facilitator, project.Id, "tableur", row.SubMap);
        await SearchAsync(reader, project.Id, "achats");
        await SearchAsync(admin, project.Id, "Valider", project.RootMapId);

        Assert.Equal(before, await StateAsync());
    }

    /// <summary>Non-member 404, anonymous 401 before his body is read, unknown project and project in the trash 404, for every member.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_stranger_and_a_project_in_the_trash_get_not_found(EngineKind kind)
    {
        var project = await ApiProject.CreateAsync(await hosts.OfAsync(kind));
        using var stranger = project.Stranger;
        using var anonymous = project.Host.As(null);
        using var reader = project.Reader;
        using var facilitator = project.Facilitator;
        using var admin = project.Admin;
        var body = new { text = "Acteur" };
        Assert.Equal(1, (await SearchAsync(reader, project.Id, "Acteur")).GetProperty("total").GetInt32());

        (await stranger.PostAsync(Url(project.Id), body)).Refused(404, "INTROUVABLE");
        Assert.Equal(401, (await anonymous.PostAsync(Url(project.Id), body)).Status);
        // The anonymous is refused before his body is read: a body that is not JSON is not even looked at (400 otherwise).
        Assert.Equal(401, (await anonymous.PostAsync(Url(project.Id), "{ pas du JSON")).Status);
        (await reader.PostAsync(Url(Guid.NewGuid()), body)).Refused(404, "INTROUVABLE");

        (await facilitator.DeleteAsync($"/api/projects/{project.Id}?confirmation={project.Code}", await project.ProjectVersionAsync())).Ok(204);
        foreach (var member in new[] { reader, facilitator, admin })
            (await member.PostAsync(Url(project.Id), body)).Refused(404, "INTROUVABLE");
    }

    /// <summary>
    /// The word is a business content: it travels in the body of a POST, never in the address (the logs of IIS write the
    /// query string of every address); the host reads no query string for it, and its log writes one line of the route,
    /// without the word, even at the most detailed level of its own categories.
    /// </summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_word_never_leaves_the_body_the_log_holds_the_route_alone(EngineKind kind)
    {
        var project = await ApiProject.CreateAsync(await hosts.OfAsync(kind));
        const string secret = "Zorglubienne";
        using var capture = new LogCapture();
        await project.Host.WithVariantAsync(capture.Register, async clientOf =>
        {
            using var reader = clientOf(Logins.Reader);
            Assert.Equal(0, (await SearchAsync(reader, project.Id, secret)).GetProperty("total").GetInt32());
            var addressed = await reader.PostAsync($"{Url(project.Id)}?text=Acteur", new { language = "fr" });
            Assert.Equal("text", addressed.Refused(400, "VALEUR_INVALIDE").GetProperty("field").GetString());
            // No route answers a GET of the search: a word in an address is never read.
            Assert.Equal(404, (await reader.GetAsync($"{Url(project.Id)}?text=Acteur")).Status);
        });

        var lines = capture.Lines;
        Assert.Contains(lines, line => line.StartsWith("POST /api/projects/{id:guid}/search 200 in ", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.StartsWith("POST /api/projects/{id:guid}/search 400 in ", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, line => line.Contains(secret, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(lines, line => line.Contains(project.Id.ToString(), StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// The body of a search is 4 KB at most (ADR 110 point 4): a body of exactly 4 KB is read, one byte more is refused
    /// 413 before it is read, with its length announced or sent in chunks; within it, the text and the language keep
    /// their own bounds.
    /// </summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_body_too_large_is_refused_before_it_is_read(EngineKind kind)
    {
        var project = await ApiProject.CreateAsync(await hosts.OfAsync(kind));
        foreach (var announced in new[] { true, false })
        {
            using var reader = project.Host.Unbuffered(Logins.Reader);
            var atTheLimit = await SendAsync(reader, project.Id, Padded(FourKilobytes), announced);
            Assert.Equal(1, atTheLimit.Ok().GetProperty("total").GetInt32());
            var over = await SendAsync(reader, project.Id, Padded(FourKilobytes + 1), announced);
            var details = over.Refused(413, "REQUETE_TROP_GROSSE");
            Assert.Equal((4, "KB"), (details.GetProperty("max").GetInt32(), details.GetProperty("unit").GetString()));
            Assert.DoesNotContain("0 M", over.Json.GetProperty("message").GetString(), StringComparison.Ordinal);
        }

        using var client = project.Reader;
        Assert.Equal("text", (await client.PostAsync(Url(project.Id), new { text = new string('a', 101) })).Refused(400, "VALEUR_INVALIDE").GetProperty("field").GetString());
        Assert.Equal("text", (await client.PostAsync(Url(project.Id), new { text = "   " })).Refused(400, "VALEUR_INVALIDE").GetProperty("field").GetString());
        Assert.Equal("language", (await client.PostAsync(Url(project.Id), new { text = "Acteur", language = "EN" })).Refused(400, "VALEUR_INVALIDE")
            .GetProperty("field").GetString());
        Assert.Equal(0, (await client.PostAsync(Url(project.Id), new { text = new string('a', 100) })).Ok().GetProperty("total").GetInt32());
    }

    /// <summary>`{"text":"Acteur"}` padded with spaces before its closing brace to exactly <paramref name="size"/> bytes.</summary>
    private static byte[] Padded(int size)
    {
        const string head = "{\"text\":\"Acteur\"";
        return Encoding.ASCII.GetBytes(head + new string(' ', size - head.Length - 1) + "}");
    }

    private static async Task<ApiResponse> SendAsync(ApiClient client, Guid projectId, byte[] body, bool announced)
    {
        using HttpContent content = announced ? new ByteArrayContent(body) : new StreamContent(new ForwardOnly(body));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        Assert.Equal(announced ? (long?)body.Length : null, content.Headers.ContentLength);
        return await client.SendContentAsync(HttpMethod.Post, Url(projectId), content, null);
    }

    /// <summary>
    /// A log provider of a variant of the host that keeps every line written, with the filters of the product (its
    /// framework at Error) except for the categories of the product, kept from the most detailed level.
    /// </summary>
    internal sealed class LogCapture : ILoggerProvider
    {
        private readonly ConcurrentQueue<string> lines = new();

        public IReadOnlyList<string> Lines => [.. lines];

        public void Register(IServiceCollection services)
        {
            services.AddSingleton<ILoggerProvider>(this);
            services.Configure<LoggerFilterOptions>(options =>
                options.Rules.Add(new LoggerFilterRule(typeof(LogCapture).FullName, "Topogenia", LogLevel.Trace, null)));
        }

        public ILogger CreateLogger(string categoryName) => new Writer(lines);

        public void Dispose()
        {
        }

        private sealed class Writer(ConcurrentQueue<string> lines) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                lines.Enqueue(formatter(state, exception) + (exception is null ? "" : $" {exception}"));
        }
    }

    /// <summary>A stream read once, forward only, whose length is unknown: the client sends it in chunks, without length.</summary>
    private sealed class ForwardOnly(byte[] bytes) : Stream
    {
        private readonly MemoryStream inner = new(bytes, writable: false);

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                inner.Dispose();
            base.Dispose(disposing);
        }
    }
}
