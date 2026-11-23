// DocumentLocationNeverOpenedTests.cs
// S-5.4-13 at the run on both databases: the location of the documents is a named pipe that the test listens to, and no route that writes, reads, copies or exports a document opens it; « Open the folder », the one route that may look at a location, never looks at a pipe; and with a location that its port admits, in desktop mode, that route alone reaches the look and the shell; the routes played, as the host recorded the entry point that each request reached, are compared with the table of the routes of the host, so that none is left out.

using System.IO.Pipes;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Topogenia.Host.Desktop;
using Topogenia.Host.Tests.Api;
using Topogenia.Host.Tests.Dissolutions;
using Topogenia.Host.Tests.Documents;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Host.Tests.Sheets;
using Topogenia.Host.Tests.Steps;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Security;

/// <summary>
/// Spec 5.4 (R486, S-5.4-13): the server never opens the address of a document, a local link or a network path; the
/// computer of the user opens it. One exception, named (ADR 104): in desktop mode, on a click of the person, the program
/// asks Windows to show the folder that holds the document; it never opens a document. That route alone
/// (`POST /api/steps/{id}/documents/{documentId}/folder`) may look at a location, and `DocumentFolderRoutesTests` proves
/// what it does; here it is called like every other route, and must leave the pipe alone: not found in server mode,
/// where nothing is read, and in desktop mode a pipe is refused on its text, never looked at.
/// The location of the documents of these tests is a named pipe that the test listens to:
/// an opening of that path by the server, whatever the API (a file, a stream, SQLite, a process, the share of Windows),
/// is a connection to the pipe. Every route that writes, reads, copies or exports a document (the table of the routes of
/// `docs/couverture.md`) is called with it, and the pipe must stay without a client; at the end the test opens the path
/// itself and the pipe must see it, so that a listener that sees nothing cannot pass for a proof. The second form names
/// the pipe through the share of the machine (<c>\\127.0.0.1\pipe\...</c>), read by the client SMB of Windows as the path
/// of a share of another machine would be, on the loopback, in a few milliseconds. The third form names the provider of
/// the shares of Windows before the server (<c>\\;LanmanRedirector\127.0.0.1\pipe\...</c>): the server of the path is
/// then the provider, the pipes hide behind the name of a share, and the first rules of the folder let the look open the
/// pipe; a server that is not written as the name of a machine is now refused on its text.
/// A pipe proves an opening, not a call of the port: the port refuses a pipe on its text, so that a second route to the
/// port would leave the pipe alone. The last test therefore runs the same routes in desktop mode on a location that the
/// port admits, a folder of the test, with a look and a shell that record: after every route that writes, reads, copies
/// or exports a document, neither was asked anything; after the route of the folder, the look was asked that path and
/// the shell that folder, and nothing else.
/// A tour written by hand misses the route added tomorrow, and a call added to a route it does not play. The test of
/// the folder and the one of the local pipe therefore play, besides, every route of the table of the rights
/// (`RightsMatrixTests`, one valid request per route) on a project whose document carries the location, and compare
/// what was played with the table of the routes of the host (`RouteTableTests`): each route is played, or named in the
/// exact list of those that are not (<see cref="OutOfTheTour"/>).
/// A name written by the test proves nothing of the request it sends: an entry whose address is the one of another
/// route would count a route as played that no request reached. What is played is therefore what the host recorded
/// (<see cref="RouteRecorder"/>: the method of each request and the pattern of the entry point that its routing
/// chose), and each request must reach the route it is sent as.
/// </summary>
public sealed class DocumentLocationNeverOpenedTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private const string LocalPipes = @"\\.\pipe\";

    private const string SharedPipes = @"\\127.0.0.1\pipe\";

    private const string RedirectedPipes = @"\\;LanmanRedirector\127.0.0.1\pipe\";

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task No_route_opens_the_location_of_a_document_that_names_a_local_pipe(EngineKind kind) => await EveryRouteAsync(kind, LocalPipes, wholeTable: true);

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task No_route_opens_the_location_of_a_document_that_names_a_pipe_through_the_share_of_the_machine(EngineKind kind) =>
        await EveryRouteAsync(kind, SharedPipes);

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task No_route_opens_the_location_of_a_document_that_names_a_pipe_behind_a_provider_of_windows(EngineKind kind) =>
        await EveryRouteAsync(kind, RedirectedPipes);

    /// <summary>
    /// A host of its own in desktop mode, whose look records in the place of the one of Windows (its shell is the recorder
    /// of every host of the tests): the location of the documents is a folder the test makes and removes. The folder is
    /// never looked at for real by the host: the recorder answers what the test finds there.
    /// </summary>
    [Fact]
    public async Task In_desktop_mode_the_route_of_the_folder_alone_looks_at_a_location_that_its_port_admits_and_asks_the_shell()
    {
        var folder = Directory.CreateTempSubdirectory($"{TestDatabase.FolderPrefix}{Environment.ProcessId}-location-");
        var look = new LookRecorder();
        await using var host = await ApiHost.StartAsync(EngineKind.Sqlite, services =>
        {
            services.RemoveAll<IFolderDisk>();
            services.AddSingleton<IFolderDisk>(look);
        });
        try
        {
            var watch = new FolderWatch(folder.FullName, look, host.Folders, host.Routes);
            await EveryRouteAsync(host, watch);
            await WholeTableAsync(host, watch);
            var (project, step, document) = await DocumentFolderRoutesTests.DocumentAsync(host, watch.Location);
            watch.Untouched("A route of the host other than the one of the folder");
            using var reader = project.Reader;

            (await watch.ReachingAsync(FolderRoute, () => reader.PostAsync(DocumentFolderRoutesTests.Route(step, document)))).Ok(204);

            Assert.Equal([watch.Location], look.Paths);
            Assert.Equal([watch.Location + @"\"], host.Folders.Shown);
            TheToursPlayedTheTable(watch);
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    /// <summary>
    /// The tour trusts no name (third review, defect 4): a request sent as one route to the address of another, which
    /// answers as well, fails the tour and is not counted as played; sent as the route it reaches, it is counted under
    /// the name that the host gives it.
    /// </summary>
    [Fact]
    public async Task A_request_sent_to_the_address_of_another_route_fails_the_tour_and_is_not_counted_as_played()
    {
        var host = await hosts.OfAsync(EngineKind.Sqlite);
        var project = await ApiProject.CreateAsync(host);
        using var watch = new PipeWatch(host.Routes, LocalPipes);
        using var reader = project.Reader;
        var comments = $"/api/maps/{project.RootMapId}/presentation-comments";
        const string named = "GET /api/maps/{id}/flow-presentation-comments";
        const string reached = "GET /api/maps/{id}/presentation-comments";

        var failure = await Assert.ThrowsAnyAsync<Xunit.Sdk.XunitException>(() => watch.CallAsync(named, () => reader.GetAsync(comments)));

        Assert.Contains($"The request sent as {named} reached {reached}", failure.Message, StringComparison.Ordinal);
        Assert.Empty(watch.Played);
        (await watch.CallAsync(reached, () => reader.GetAsync(comments))).Ok();
        Assert.Equal([reached], watch.Played);
        Assert.Contains(named, RouteTableTests.ExpectedRoutes);
    }

    private async Task EveryRouteAsync(EngineKind kind, string pipes, bool wholeTable = false)
    {
        var host = await hosts.OfAsync(kind);
        using var watch = new PipeWatch(host.Routes, pipes);
        await EveryRouteAsync(host, watch);
        if (wholeTable)
        {
            await WholeTableAsync(host, watch);
            TheToursPlayedTheTable(watch);
        }
        await watch.NeverOpenedButSeenAsync();
    }

    private const string FolderRoute = "POST /api/steps/{id}/documents/{documentId}/folder";

    /// <summary>The routes of the host that no tour plays, exactly, each with its reason.</summary>
    private static readonly string[] OutOfTheTour =
    [
        // The application told to stop: it would stop the host of these tests, and it reads no project (`DesktopServerTests`
        // plays it on a run of its own).
        "POST /api/desktop/quit",
    ];

    /// <summary>
    /// Every route of the table of the rights, played once as the administrator on a project of its own whose document
    /// carries the location; then the routes that table leaves out and that read the base, make a project or answer by the
    /// mode: the health, the sample project, made once in desktop mode and not found in server mode, and the writes that a
    /// page declares, held in memory in desktop mode and not found in server mode.
    /// </summary>
    private static async Task WholeTableAsync(ApiHost host, LocationWatch watch)
    {
        await RightsMatrixTests.PlayEveryRouteAsync(host, watch.Location, watch.CallAsync);
        using var admin = host.As(Logins.Admin);
        (await watch.CallAsync("GET /api/health", () => admin.GetAsync("/api/health"))).Ok();
        var sample = await watch.CallAsync("POST /api/desktop/sample-project", () => admin.PostAsync("/api/desktop/sample-project"));
        Assert.True(sample.Status is 201 or 404 or 409, $"The sample project: {sample.Status} {sample.Body}");
        // What a page still has to send (ADR 115): held in memory in desktop mode, not found in server mode, nothing read.
        var writes = await watch.CallAsync("POST /api/desktop/writes", () => admin.PostAsync("/api/desktop/writes", new { tab = "tour", waiting = false }));
        Assert.True(writes.Status is 204 or 404, $"The writes of a page: {writes.Status} {writes.Body}");
    }

    /// <summary>
    /// What the tours played is the table of the routes of the host, but the routes named out of the tour: a route added
    /// to the host fails here until a tour plays it or the list names it with its reason.
    /// </summary>
    private static void TheToursPlayedTheTable(LocationWatch watch)
    {
        Assert.Empty(watch.Played.Intersect(OutOfTheTour, StringComparer.Ordinal));
        Assert.Equal(RouteTableTests.ExpectedRoutes.Order(StringComparer.Ordinal), watch.Played.Concat(OutOfTheTour).Order(StringComparer.Ordinal));
    }

    /// <summary>Every route that writes, reads, copies or exports a document, called with the location of the watch.</summary>
    private static async Task EveryRouteAsync(ApiHost host, LocationWatch watch)
    {
        var project = await ApiProject.CreateAsync(host);
        var (producer, consumer, output) = await DocumentsAsync(project, watch);
        await UsesAndSheetAsync(project, watch, producer, consumer, output);
        await UndoAndHistoryAsync(project, watch, consumer);
        await PmapAsync(project, watch);
        await PackageAndTrashAsync(project, watch);
        await DissolutionAsync(await ApiProject.CreateAsync(host), watch);
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
    private static async Task<(Guid Producer, Guid Consumer, Guid Output)> DocumentsAsync(ApiProject project, LocationWatch watch)
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
        await watch.FolderAsync(project, producer, outputId);
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
    private static async Task UsesAndSheetAsync(ApiProject project, LocationWatch watch, Guid producer, Guid consumer, Guid output)
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
    private static async Task UndoAndHistoryAsync(ApiProject project, LocationWatch watch, Guid consumer)
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
    private static async Task PmapAsync(ApiProject project, LocationWatch watch)
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
    private static async Task PackageAndTrashAsync(ApiProject project, LocationWatch watch)
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
    private static async Task DissolutionAsync(ApiProject project, LocationWatch watch)
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
    /// The location given to the documents of a tour of the routes, what tells whether a route reached it, and what the
    /// host says each request reached (<see cref="RouteRecorder"/>).
    /// </summary>
    private abstract class LocationWatch(RouteRecorder routes)
    {
        private readonly HashSet<string> played = new(StringComparer.Ordinal);

        public abstract string Location { get; }

        /// <summary>
        /// The routes played, each once, as the host recorded them: the method of the request and the pattern of the entry
        /// point that its routing chose, never the name that the test wrote.
        /// </summary>
        public IReadOnlyCollection<string> Played => played;

        /// <summary>
        /// Sends a request of the route named and checks, before the test reads the reply, that the location was left alone
        /// and that the host reached the route named.
        /// </summary>
        public Task<ApiResponse> CallAsync(string route, Func<Task<ApiResponse>> call) => ReachingAsync(route, () => WatchedAsync(route, call));

        /// <summary>
        /// Sends a request and keeps as played the route that the host reached for it, which must be the one named: a
        /// request sent to the address of another route fails here, and is not counted. The call may send first what the
        /// route needs (a version read, a stack filled): its last request is the one of the route.
        /// </summary>
        public async Task<ApiResponse> ReachingAsync(string route, Func<Task<ApiResponse>> call)
        {
            var before = routes.Count;
            var reply = await call();
            var reached = routes.Since(before);
            Assert.True(reached.Count > 0 && reached[^1] == route, $"The request sent as {route} reached {(reached.Count > 0 ? reached[^1] : "no route of the host")}");
            played.Add(reached[^1]);
            return reply;
        }

        protected abstract Task<ApiResponse> WatchedAsync(string route, Func<Task<ApiResponse>> call);

        /// <summary>« Open the folder » asked in the middle of the tour, where the watch has something to say of it.</summary>
        public virtual Task FolderAsync(ApiProject project, Guid step, Guid document) => Task.CompletedTask;
    }

    /// <summary>A look that records each path it is asked, and answers what the test itself finds there: a folder, a file, nothing.</summary>
    private sealed class LookRecorder : IFolderDisk
    {
        private readonly List<string> paths = [];

        /// <summary>The paths looked at, in their order, as they were given.</summary>
        public IReadOnlyList<string> Paths
        {
            get
            {
                lock (paths)
                    return [.. paths];
            }
        }

        public FolderLook Look(string path)
        {
            lock (paths)
                paths.Add(path);
            return Directory.Exists(path) ? FolderLook.Folder : File.Exists(path) ? FolderLook.NotFolder : FolderLook.Missing;
        }
    }

    /// <summary>
    /// A folder that the port of « Open the folder » admits, and the look and the shell of the host, which record: a route
    /// that reached the port, by any way, has asked one of them something when its reply comes (the opener answers once
    /// its look and its shell have).
    /// </summary>
    private sealed class FolderWatch(string location, LookRecorder look, FolderRecorder shell, RouteRecorder routes) : LocationWatch(routes)
    {
        public override string Location => location;

        protected override async Task<ApiResponse> WatchedAsync(string route, Func<Task<ApiResponse>> call)
        {
            var reply = await call();
            Untouched(route);
            return reply;
        }

        /// <summary>Neither the look nor the shell was asked anything since the start.</summary>
        public void Untouched(string route)
        {
            Assert.True(look.Paths.Count == 0, $"{route} looked at {string.Join(", ", look.Paths)}");
            Assert.True(shell.Shown.Count == 0, $"{route} asked the shell to show {string.Join(", ", shell.Shown)}");
        }
    }

    /// <summary>
    /// A named pipe of a unique name that waits, without blocking, for its first client, and its path in the form given.
    /// The server opens a path during the request that reads it: when the reply comes, the connection is made, and the wait
    /// is only for its completion to reach the test, within a bound.
    /// </summary>
    private sealed class PipeWatch : LocationWatch, IDisposable
    {
        private static readonly TimeSpan Bound = TimeSpan.FromSeconds(1);

        private static readonly TimeSpan ControlBound = TimeSpan.FromSeconds(10);

        private readonly NamedPipeServerStream pipe;
        private readonly Task connection;

        public PipeWatch(RouteRecorder routes, string pipes)
            : base(routes)
        {
            var name = $"document-{Guid.NewGuid():N}";
            pipe = new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            connection = pipe.WaitForConnectionAsync(TestContext.Current.CancellationToken);
            Location = pipes + name;
        }

        public override string Location { get; }

        /// <summary>
        /// « Open the folder » asked by a reader: INTROUVABLE in server mode, the mode decided before the document is read;
        /// in desktop mode every form of the pipe is refused on its text (a device, a share of pipes, a server that is no
        /// name of a machine). No folder is shown.
        /// </summary>
        public override async Task FolderAsync(ApiProject project, Guid step, Guid document)
        {
            using var reader = project.Reader;
            var reply = await CallAsync(FolderRoute, () => reader.PostAsync($"{Documents(step, document)}/folder"));
            if (project.Host.Kind == EngineKind.SqlServer)
                reply.Refused(404, "INTROUVABLE");
            else
                reply.Refused(400, "CHEMIN_NON_PRIS_EN_CHARGE");
            Assert.Empty(project.Host.Folders.Shown);
        }

        private async Task<bool> OpenedWithinAsync(TimeSpan bound)
        {
            await Task.WhenAny(connection, Task.Delay(bound, TestContext.Current.CancellationToken));
            return connection.IsCompletedSuccessfully;
        }

        /// <summary>
        /// Sends a request of the route named and checks that the location was not opened, before the test reads the reply:
        /// a refusal waits for the bound, so that an opening that made the request fail is said as an opening.
        /// </summary>
        protected override async Task<ApiResponse> WatchedAsync(string route, Func<Task<ApiResponse>> call)
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
