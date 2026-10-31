// DocumentLocationNeverOpenedTests.cs
// S-5.4-13 at the run on both databases: the location of the documents is a named pipe that the test listens to, and no route that writes, reads, copies or exports a document opens it.

using System.IO.Pipes;
using System.Text.Json;
using Topogenia.Host.Tests.Api;
using Topogenia.Host.Tests.Dissolutions;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Host.Tests.Sheets;
using Topogenia.Host.Tests.Steps;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Security;

/// <summary>
/// Spec 5.4 (R486, S-5.4-13): the server never opens the address of a document, a local link or a network path; the
/// computer of the user opens it. The location of the documents of these tests is a named pipe that the test listens to:
/// an opening of that path by the server, whatever the API (a file, a stream, SQLite, a process, the share of Windows),
/// is a connection to the pipe. Every route that writes, reads, copies or exports a document (the table of the routes of
/// `docs/couverture.md`) is called with it, and the pipe must stay without a client; at the end the test opens the path
/// itself and the pipe must see it, so that a listener that sees nothing cannot pass for a proof. The second form names
/// the pipe through the share of the machine (<c>\\127.0.0.1\pipe\...</c>), read by the client SMB of Windows as the path
/// of a share of another machine would be, on the loopback, in a few milliseconds.
/// </summary>
public sealed class DocumentLocationNeverOpenedTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private const string LocalPipes = @"\\.\pipe\";

    private const string SharedPipes = @"\\127.0.0.1\pipe\";

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task No_route_opens_the_location_of_a_document_that_names_a_local_pipe(EngineKind kind) => await EveryRouteAsync(kind, LocalPipes);

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task No_route_opens_the_location_of_a_document_that_names_a_pipe_through_the_share_of_the_machine(EngineKind kind) =>
        await EveryRouteAsync(kind, SharedPipes);

    private async Task EveryRouteAsync(EngineKind kind, string pipes)
    {
        var host = await hosts.OfAsync(kind);
        using var watch = new PipeWatch(pipes);
        var project = await ApiProject.CreateAsync(host);
        var (producer, consumer, output) = await DocumentsAsync(project, watch);
        await UsesAndSheetAsync(project, watch, producer, consumer, output);
        await UndoAndHistoryAsync(project, watch, consumer);
        await PmapAsync(project, watch);
        await PackageAndTrashAsync(project, watch);
        await DissolutionAsync(await ApiProject.CreateAsync(host), watch);
        await watch.NeverOpenedButSeenAsync();
    }

    private static string Documents(Guid stepId, Guid? documentId = null) => $"/api/steps/{stepId}/documents{(documentId is { } id ? $"/{id}" : "")}";

    /// <summary>The documents of the outputs and of the inputs of a sheet.</summary>
    private static IEnumerable<JsonElement> DocumentsOf(JsonElement sheet) =>
        sheet.GetProperty("outputs").EnumerateArray().Concat(sheet.GetProperty("inputs").EnumerateArray()).Select(entry => entry.GetProperty("document"));

    private static JsonElement Named(JsonElement sheet, string name) => DocumentsOf(sheet).First(document => document.GetProperty("name").GetString() == name);

    /// <summary>Every document of every step of a map, read on the sheets, carries the location (a copy or an import kept it).</summary>
    private static async Task CarriesTheLocationAsync(ApiClient client, Guid mapId, string location)
    {
        var steps = (await client.GetAsync($"/api/maps/{mapId}")).Ok().GetProperty("map").GetProperty("steps").EnumerateArray()
            .Select(step => step.GetProperty("id").GetGuid()).ToList();
        var locations = new List<string?>();
        foreach (var step in steps)
            locations.AddRange(DocumentsOf((await client.GetAsync($"/api/steps/{step}/sheet")).Ok()).Select(document => document.GetProperty("location").GetString()));
        Assert.NotEmpty(locations);
        Assert.All(locations, found => Assert.Equal(location, found));
    }

    /// <summary>An output, an external input and a draft written with the location, the output changed, the draft deleted, then the sheet, the map and the "Other" window read.</summary>
    private static async Task<(Guid Producer, Guid Consumer, Guid Output)> DocumentsAsync(ApiProject project, PipeWatch watch)
    {
        var producer = await StepSheets.StepAsync(project, 0, "Commander");
        var consumer = await StepSheets.StepAsync(project, 1, "Livrer");
        using var facilitator = project.Facilitator;
        using var reader = project.Reader;
        var location = watch.Location;
        var created = (await watch.CallAsync("POST /api/steps/{id}/documents", () => facilitator.PostAsync(Documents(producer),
            new { direction = "output", name = "Bon de commande", location, share_confirmed = true }))).Ok();
        var output = Named(created, "Bon de commande");
        Assert.Equal((location, "path"), (output.GetProperty("location").GetString(), output.GetProperty("location_type").GetString()));
        var outputId = output.GetProperty("id").GetGuid();
        (await watch.CallAsync("POST /api/steps/{id}/documents", () => facilitator.PostAsync(Documents(consumer),
            new { direction = "input", name = "Facture", location, wait_hours = 4 }))).Ok();
        (await watch.CallAsync("POST /api/steps/{id}/documents", () => facilitator.PostAsync(Documents(producer),
            new { direction = "output", name = "Brouillon", location }))).Ok();
        var updated = (await watch.CallAsync("PUT /api/steps/{id}/documents/{documentId}", () => facilitator.PutAsync(Documents(producer, outputId),
            new { name = "Commande signée", location, share_confirmed = true }, output.GetProperty("version").GetInt32()))).Ok();
        Assert.Equal(location, Named(updated, "Commande signée").GetProperty("location").GetString());
        var draft = Named(updated, "Brouillon");
        (await watch.CallAsync("DELETE /api/steps/{id}/documents/{documentId}", () => facilitator.DeleteAsync(Documents(producer, draft.GetProperty("id").GetGuid()),
            draft.GetProperty("version").GetInt32()))).Ok();
        (await watch.CallAsync("GET /api/steps/{id}/sheet", () => reader.GetAsync($"/api/steps/{producer}/sheet"))).Ok();
        (await watch.CallAsync("GET /api/maps/{id}", () => reader.GetAsync($"/api/maps/{project.RootMapId}"))).Ok();
        (await watch.CallAsync("GET /api/steps/{id}/documents/available", () => reader.GetAsync($"/api/steps/{consumer}/documents/available"))).Ok();
        return (producer, consumer, outputId);
    }

    /// <summary>The use of the output ticked, unticked and ticked again by the consumer; the sheet of the producer saved and validated.</summary>
    private static async Task UsesAndSheetAsync(ApiProject project, PipeWatch watch, Guid producer, Guid consumer, Guid output)
    {
        using var facilitator = project.Facilitator;
        var uses = $"/api/steps/{consumer}/uses/{output}";
        var unused = await project.UseVersionAsync(consumer, output);
        (await watch.CallAsync("PUT /api/steps/{id}/uses/{documentId}", () => facilitator.PutAsync(uses, new { wait_hours = 2, pain_point = "Toujours en retard" }, unused))).Ok();
        var used = await project.UseVersionAsync(consumer, output);
        (await watch.CallAsync("DELETE /api/steps/{id}/uses/{documentId}", () => facilitator.DeleteAsync(uses, used))).Ok();
        var again = await project.UseVersionAsync(consumer, output);
        (await watch.CallAsync("PUT /api/steps/{id}/uses/{documentId}", () => facilitator.PutAsync(uses, new { wait_hours = 2 }, again))).Ok();
        var read = await StepSheets.ReadVersionAsync(facilitator, producer);
        var saved = (await watch.CallAsync("PUT /api/steps/{id}/sheet", () => facilitator.PutAsync($"/api/steps/{producer}/sheet",
            StepSheets.Purpose("Commander le matériel"), read))).Ok();
        (await watch.CallAsync("POST /api/steps/{id}/sheet/validate", () => facilitator.PostAsync($"/api/steps/{producer}/sheet/validate", null,
            StepSheets.VersionOf(saved)))).Ok();
    }

    /// <summary>The consumer deleted with its input and its use, the deletion undone, redone and undone again; the history and the audit read.</summary>
    private static async Task UndoAndHistoryAsync(ApiProject project, PipeWatch watch, Guid consumer)
    {
        using var facilitator = project.Facilitator;
        using var admin = project.Admin;
        var map = $"/api/maps/{project.RootMapId}";
        var version = await project.StepVersionAsync(consumer);
        (await watch.CallAsync("DELETE /api/steps/{id}", () => facilitator.DeleteAsync($"/api/steps/{consumer}", version))).Ok();
        (await watch.CallAsync("POST /api/maps/{id}/undo", () => facilitator.PostAsync($"{map}/undo"))).Ok();
        (await watch.CallAsync("POST /api/maps/{id}/redo", () => facilitator.PostAsync($"{map}/redo"))).Ok();
        (await watch.CallAsync("POST /api/maps/{id}/undo", () => facilitator.PostAsync($"{map}/undo"))).Ok();
        (await watch.CallAsync("GET /api/maps/{id}/history", () => facilitator.GetAsync($"{map}/history"))).Ok();
        (await watch.CallAsync("GET /api/admin/audit", () => admin.GetAsync($"/api/admin/audit?project_id={project.Id}"))).Ok();
        Assert.Equal(watch.Location, Named((await facilitator.GetAsync($"/api/steps/{consumer}/sheet")).Ok(), "Facture").GetProperty("location").GetString());
    }

    /// <summary>The copy of the project as a `.pmap` with its sheets, opened again as a project, and the duplication of the project.</summary>
    private static async Task PmapAsync(ApiProject project, PipeWatch watch)
    {
        using var admin = project.Admin;
        var copy = await watch.CallAsync("GET /api/projects/{id}/pmap", () => admin.GetAsync($"/api/projects/{project.Id}/pmap?with_sheets=true"));
        Assert.True(copy.Status == 200, copy.Body);
        Assert.Contains(JsonSerializer.Serialize(watch.Location), copy.Body, StringComparison.Ordinal);
        var opened = (await watch.CallAsync("POST /api/projects/import-pmap", () => admin.PostAsync("/api/projects/import-pmap",
            new { content = copy.Body, code = ApiProject.NewCode() }))).Ok(201);
        await CarriesTheLocationAsync(admin, opened.GetProperty("root_map_id").GetGuid(), watch.Location);
        (await watch.CallAsync("POST /api/projects/{id}/duplicate", () => admin.PostAsync($"/api/projects/{project.Id}/duplicate",
            new { code = ApiProject.NewCode() }))).Ok(201);
    }

    /// <summary>
    /// The package of the project exported, previewed and imported as a new project, previewed and imported in place of the
    /// project; the new project moved to the trash, listed and restored.
    /// </summary>
    private static async Task PackageAndTrashAsync(ApiProject project, PipeWatch watch)
    {
        using var admin = project.Admin;
        var exported = await watch.CallAsync("POST /api/projects/{id}/export", () => admin.PostAsync($"/api/projects/{project.Id}/export", new { without_people = false }));
        Assert.True(exported.Status == 200, exported.Body);
        var code = ApiProject.NewCode();
        (await watch.CallAsync("POST /api/projects/import/preview", () => admin.SendAsync(HttpMethod.Post, $"/api/projects/import/preview?with_members=false&code={code}",
            exported.Bytes, null))).Ok();
        var imported = (await watch.CallAsync("POST /api/projects/import", () => admin.SendAsync(HttpMethod.Post, $"/api/projects/import?with_members=false&code={code}",
            exported.Bytes, null))).Ok(201);
        var importedId = imported.GetProperty("id").GetGuid();
        await CarriesTheLocationAsync(admin, imported.GetProperty("root_map_id").GetGuid(), watch.Location);
        (await watch.CallAsync("POST /api/projects/{id}/import/preview", () => admin.SendAsync(HttpMethod.Post, $"/api/projects/{project.Id}/import/preview?with_members=true",
            exported.Bytes, null))).Ok();
        var version = await project.ProjectVersionAsync();
        (await watch.CallAsync("POST /api/projects/{id}/import", () => admin.SendAsync(HttpMethod.Post, $"/api/projects/{project.Id}/import?with_members=true",
            exported.Bytes, version))).Ok(201);
        var read = (await admin.GetAsync($"/api/projects/{importedId}")).Ok().GetProperty("version").GetInt32();
        (await watch.CallAsync("DELETE /api/projects/{id}", () => admin.DeleteAsync($"/api/projects/{importedId}?confirmation={code}", read))).Ok(204);
        (await watch.CallAsync("GET /api/trash", () => admin.GetAsync("/api/trash"))).Ok();
        (await watch.CallAsync("POST /api/trash/{id}/restore", () => admin.PostAsync($"/api/trash/{importedId}/restore"))).Ok(204);
        await CarriesTheLocationAsync(admin, imported.GetProperty("root_map_id").GetGuid(), watch.Location);
    }

    /// <summary>A document of a detailed step written with the location, the step dissolved and its remainder finished on the steps of its detail.</summary>
    private static async Task DissolutionAsync(ApiProject project, PipeWatch watch)
    {
        var row = await DetailedRow.CreateAsync(project);
        using var facilitator = project.Facilitator;
        (await watch.CallAsync("POST /api/steps/{id}/documents", () => facilitator.PostAsync(Documents(row.Macro),
            new { direction = "output", name = "Commande", location = watch.Location }))).Ok();
        var version = await row.MacroVersionAsync();
        var frame = (await watch.CallAsync("POST /api/steps/{id}/dissolve", () => facilitator.PostAsync($"/api/steps/{row.Macro}/dissolve", null, version))).Ok()
            .GetProperty("id").GetGuid();
        (await watch.CallAsync("POST /api/dissolutions/{id}/finish", () => facilitator.PostAsync($"/api/dissolutions/{frame}/finish"))).Ok();
        await CarriesTheLocationAsync(facilitator, project.RootMapId, watch.Location);
    }

    /// <summary>
    /// A named pipe of a unique name that waits, without blocking, for its first client, and its path in the form given.
    /// The server opens a path during the request that reads it: when the reply comes, the connection is made, and the wait
    /// is only for its completion to reach the test, within a bound.
    /// </summary>
    private sealed class PipeWatch : IDisposable
    {
        private static readonly TimeSpan Bound = TimeSpan.FromSeconds(1);

        private static readonly TimeSpan ControlBound = TimeSpan.FromSeconds(10);

        private readonly NamedPipeServerStream pipe;
        private readonly Task connection;

        public PipeWatch(string pipes)
        {
            var name = $"document-{Guid.NewGuid():N}";
            pipe = new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            connection = pipe.WaitForConnectionAsync(TestContext.Current.CancellationToken);
            Location = pipes + name;
        }

        public string Location { get; }

        private async Task<bool> OpenedWithinAsync(TimeSpan bound)
        {
            await Task.WhenAny(connection, Task.Delay(bound, TestContext.Current.CancellationToken));
            return connection.IsCompletedSuccessfully;
        }

        /// <summary>
        /// Sends a request of the route named and checks that the location was not opened, before the test reads the reply:
        /// a refusal waits for the bound, so that an opening that made the request fail is said as an opening.
        /// </summary>
        public async Task<ApiResponse> CallAsync(string route, Func<Task<ApiResponse>> call)
        {
            var reply = await call();
            var opened = reply.Status < 400 ? connection.IsCompletedSuccessfully : await OpenedWithinAsync(Bound);
            Assert.False(opened, $"{route} opened the location of the document {Location} (status {reply.Status})");
            return reply;
        }

        /// <summary>No opening after the last reply either; then the test opens the path itself, which the pipe must see.</summary>
        public async Task NeverOpenedButSeenAsync()
        {
            Assert.False(await OpenedWithinAsync(Bound), $"A route opened the location of the document {Location} after its reply");
            _ = File.Exists(Location);
            Assert.True(await OpenedWithinAsync(ControlBound), $"The pipe saw no client when the test opened {Location}: the watch proves nothing here");
        }

        public void Dispose()
        {
            pipe.Dispose();
            _ = connection.ContinueWith(static wait => wait.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
        }
    }
}
