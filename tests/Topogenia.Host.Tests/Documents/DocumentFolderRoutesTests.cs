// DocumentFolderRoutesTests.cs
// « Open the folder » of a document through the whole host (ADR 104): in desktop mode the folder that holds the document is given to the shell, here a recorder; its right, its refusals, nothing written; not found in server mode.

using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Topogenia.Application.Ports;
using Topogenia.Application.Queries;
using Topogenia.Domain.Errors;
using Topogenia.Host.Api.Documents;
using Topogenia.Host.Composition;
using Topogenia.Host.Desktop;
using Topogenia.Host.Tests.Api;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Host.Tests.Sheets;
using Topogenia.Host.Tests.Steps;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Documents;

/// <summary>
/// SQLite runs the desktop mode and SQL Server the server mode (`ApiHost`). The shell of the desktop host of the tests
/// records what it is asked (<see cref="ApiHost.Folders"/>): no Explorer is opened. The disk is real, a temporary folder
/// made by each test and removed at its end; the look that waits or is refused is replaced in <c>DocumentFolderLookTests</c>.
/// </summary>
public sealed class DocumentFolderRoutesTests(ApiHosts hosts) : IClassFixture<ApiHosts>, IDisposable
{
    private readonly DirectoryInfo root = Directory.CreateTempSubdirectory($"{TestDatabase.FolderPrefix}{Environment.ProcessId}-folders-");

    public static string Route(Guid stepId, Guid documentId) => $"/api/steps/{stepId}/documents/{documentId}/folder";

    private async Task<ApiHost> DesktopAsync() => await hosts.OfAsync(EngineKind.Sqlite);

    /// <summary>A folder of the test with one file in it.</summary>
    private (string Folder, string File) Museum(string name = "Musee")
    {
        var folder = Directory.CreateDirectory(Path.Combine(root.FullName, name)).FullName;
        var file = Path.Combine(folder, "inventaire.xlsx");
        File.WriteAllText(file, "inventaire");
        return (folder, file);
    }

    /// <summary>A step of a new project with one output at the location given; the project, the step and the document.</summary>
    public static async Task<(ApiProject Project, Guid Step, Guid Document)> DocumentAsync(ApiHost host, string? location, string creator = Logins.Admin)
    {
        var project = await ApiProject.CreateAsync(host, creator);
        var step = await StepSheets.StepAsync(project, 0);
        return (project, step, await OutputAsync(project, step, location));
    }

    private static async Task<Guid> OutputAsync(ApiProject project, Guid step, string? location)
    {
        using var facilitator = project.Facilitator;
        var sheet = (await facilitator.PostAsync($"/api/steps/{step}/documents", new { direction = "output", name = "Inventaire", location })).Ok();
        return sheet.GetProperty("outputs").EnumerateArray().Last().GetProperty("document").GetProperty("id").GetGuid();
    }

    /// <summary>The folders the shell of the host was asked to show under the folder of this test.</summary>
    private List<string> Shown(ApiHost host) => [.. host.Folders.Shown.Where(folder => folder.StartsWith(root.FullName, StringComparison.Ordinal))];

    [Fact]
    public async Task In_desktop_mode_a_reader_is_shown_the_folder_that_holds_the_document_and_nothing_is_written()
    {
        var host = await DesktopAsync();
        var (folder, file) = Museum();
        var (project, step, document) = await DocumentAsync(host, file);
        using var reader = project.Reader;
        using var admin = project.Admin;
        var version = await project.DocumentVersionAsync(step, document);
        var audit = (await admin.GetAsync($"/api/admin/audit?project_id={project.Id}")).Ok().GetArrayLength();

        var reply = await reader.PostAsync(Route(step, document));

        Assert.Equal((204, ""), (reply.Status, reply.Body));
        Assert.Equal([folder + @"\"], Shown(host));
        Assert.Equal(version, await project.DocumentVersionAsync(step, document));
        Assert.Equal(audit, (await admin.GetAsync($"/api/admin/audit?project_id={project.Id}")).Ok().GetArrayLength());
        var history = (await reader.GetAsync($"/api/maps/{project.RootMapId}/history")).Ok();
        Assert.Equal((0, 0), (history.GetProperty("undo").GetArrayLength(), history.GetProperty("redo").GetArrayLength()));
    }

    [Fact]
    public async Task The_path_of_a_folder_shows_it_and_the_path_of_a_file_shows_its_parent_the_file_never_opened()
    {
        var host = await DesktopAsync();
        var (folder, file) = Museum();
        var (project, step, ofFile) = await DocumentAsync(host, file);
        var ofFolder = await OutputAsync(project, step, folder);
        var ofMissing = await OutputAsync(project, step, Path.Combine(folder, "renomme.xlsx"));
        using var reader = project.Reader;

        // The test holds the file open and shared with no one: an opening of it by the host would fail.
        using (new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            (await reader.PostAsync(Route(step, ofFile))).Ok(204);
        (await reader.PostAsync(Route(step, ofFolder))).Ok(204);
        (await reader.PostAsync(Route(step, ofMissing))).Ok(204);

        Assert.Equal([folder + @"\", folder + @"\", folder + @"\"], Shown(host));
        Assert.Equal("inventaire", File.ReadAllText(file));
    }

    [Fact]
    public async Task Every_member_and_the_administrator_may_ask_a_stranger_finds_nothing_and_an_anonymous_caller_is_not_authenticated()
    {
        var host = await DesktopAsync();
        var (folder, file) = Museum();
        var (project, step, document) = await DocumentAsync(host, file, Logins.Owner);
        using var anonymous = host.As(null);
        using var stranger = project.Stranger;

        Assert.Equal(401, (await anonymous.PostAsync(Route(step, document))).Status);
        (await stranger.PostAsync(Route(step, document))).Refused(404, "INTROUVABLE");
        Assert.Empty(Shown(host));
        foreach (var login in new[] { Logins.Reader, Logins.Contributor, Logins.Facilitator, Logins.Admin })
        {
            using var member = host.As(login);
            (await member.PostAsync(Route(step, document))).Ok(204);
        }
        Assert.Equal(Enumerable.Repeat(folder + @"\", 4), Shown(host));
    }

    /// <summary>The checks of every write come first (`RequestGuard`): the anti-CSRF header, the origin, the site of the request.</summary>
    [Fact]
    public async Task The_route_is_a_write_for_the_guard_of_the_requests_and_a_request_it_refuses_shows_nothing()
    {
        var host = await DesktopAsync();
        var (project, step, document) = await DocumentAsync(host, Museum().File);
        using var reader = project.Reader;
        var server = (Microsoft.AspNetCore.TestHost.TestServer)host.Services.GetRequiredService<IServer>();
        using var raw = server.CreateClient();
        raw.BaseAddress = new Uri($"http://127.0.0.1:{ApiHost.DesktopPort}");
        raw.DefaultRequestHeaders.Add(TestAuthentication.Header, Logins.Reader);

        using var withoutHeader = await raw.PostAsync(new Uri(Route(step, document), UriKind.Relative), null, TestContext.Current.CancellationToken);
        Assert.Equal(400, (int)withoutHeader.StatusCode);
        Assert.Contains("EN_TETE_MANQUANT", await withoutHeader.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        (await reader.SendAsync(HttpMethod.Post, Route(step, document), null, null, ("Origin", "http://127.0.0.1:1"))).Refused(403, "ACCES_REFUSE");
        (await reader.SendAsync(HttpMethod.Post, Route(step, document), null, null, ("Sec-Fetch-Site", "cross-site"))).Refused(403, "ACCES_REFUSE");

        Assert.Empty(Shown(host));
    }

    [Fact]
    public async Task A_document_of_another_step_of_another_project_of_a_deleted_step_or_of_a_project_in_the_trash_is_not_found()
    {
        var host = await DesktopAsync();
        var (project, step, document) = await DocumentAsync(host, Museum().File);
        var other = await StepSheets.StepAsync(project, 1, "Livrer");
        var (elsewhere, foreignStep, foreignDocument) = await DocumentAsync(host, Museum("Association").File);
        using var facilitator = project.Facilitator;

        (await facilitator.PostAsync(Route(other, document))).Refused(404, "INTROUVABLE");
        (await facilitator.PostAsync(Route(step, foreignDocument))).Refused(404, "INTROUVABLE");
        (await facilitator.PostAsync(Route(foreignStep, document))).Refused(404, "INTROUVABLE");
        (await facilitator.PostAsync(Route(step, Guid.CreateVersion7()))).Refused(404, "INTROUVABLE");
        (await facilitator.PostAsync(Route(Guid.CreateVersion7(), document))).Refused(404, "INTROUVABLE");

        (await facilitator.DeleteAsync($"/api/steps/{step}", await project.StepVersionAsync(step))).Ok();
        (await facilitator.PostAsync(Route(step, document))).Refused(404, "INTROUVABLE");
        (await facilitator.DeleteAsync($"/api/projects/{elsewhere.Id}?confirmation={elsewhere.Code}", await elsewhere.ProjectVersionAsync())).Ok(204);
        (await facilitator.PostAsync(Route(foreignStep, foreignDocument))).Refused(404, "INTROUVABLE");

        Assert.Empty(Shown(host));
    }

    [Fact]
    public async Task A_document_without_a_location_or_with_a_web_address_has_no_folder_to_show()
    {
        var host = await DesktopAsync();
        var (project, step, bare) = await DocumentAsync(host, null);
        var web = await OutputAsync(project, step, "https://exemple.invalid/dossier/inventaire.xlsx");
        using var reader = project.Reader;
        var before = host.Folders.Shown.Count;

        (await reader.PostAsync(Route(step, bare))).Refused(400, "DOCUMENT_SANS_CHEMIN");
        (await reader.PostAsync(Route(step, web))).Refused(400, "DOCUMENT_SANS_CHEMIN");

        Assert.Equal(before, host.Folders.Shown.Count);
    }

    [Fact]
    public async Task A_path_whose_folder_and_parent_are_missing_is_a_folder_not_found()
    {
        var host = await DesktopAsync();
        var (project, step, document) = await DocumentAsync(host, Path.Combine(root.FullName, "Absent", "Archives", "inventaire.xlsx"));
        using var reader = project.Reader;

        var details = (await reader.PostAsync(Route(step, document))).Refused(404, "DOSSIER_INTROUVABLE");

        Assert.Empty(details.EnumerateObject());
        Assert.Empty(Shown(host));
    }

    [Fact]
    public async Task A_path_of_260_characters_is_too_long_and_one_of_259_is_looked_at()
    {
        var host = await DesktopAsync();
        var start = Path.Combine(root.FullName, "Absent") + @"\";
        var (project, step, tooLong) = await DocumentAsync(host, start + new string('a', 260 - start.Length));
        var longest = await OutputAsync(project, step, start + new string('a', 259 - start.Length));
        using var reader = project.Reader;

        var refused = await reader.PostAsync(Route(step, tooLong));

        Assert.Equal(259, refused.Refused(400, "CHEMIN_TROP_LONG").GetProperty("max").GetInt32());
        Assert.Contains("259", refused.Json.GetProperty("message").GetString(), StringComparison.Ordinal);
        (await reader.PostAsync(Route(step, longest))).Refused(404, "DOSSIER_INTROUVABLE");
        Assert.Empty(Shown(host));
    }

    /// <summary>Each path is a valid location of a document; none is the folder Windows would open under that text.</summary>
    [Theory]
    [InlineData(@"\\?\C:\Dossiers\inventaire.xlsx")]
    [InlineData(@"\\?\UNC\serveur\partage\inventaire.xlsx")]
    [InlineData(@"\\.\pipe\canal")]
    [InlineData(@"\\127.0.0.1\pipe\canal")]
    [InlineData(@"C:\Dossiers\..\Windows\notepad.exe")]
    [InlineData(@"C:\Dossiers\Musee.\inventaire.xlsx")]
    [InlineData(@"C:\Dossiers\inventaire.")]
    [InlineData(@"C:\Dossiers\inventaire.xlsx:flux")]
    [InlineData(@"\\serveur")]
    public async Task A_path_windows_would_read_as_another_object_is_refused_and_nothing_is_shown(string location)
    {
        var host = await DesktopAsync();
        var (project, step, document) = await DocumentAsync(host, location);
        using var reader = project.Reader;
        var before = host.Folders.Shown.Count;

        (await reader.PostAsync(Route(step, document))).Refused(400, "CHEMIN_NON_PRIS_EN_CHARGE");

        Assert.Equal(before, host.Folders.Shown.Count);
    }

    [Fact]
    public async Task Mixed_separators_are_those_of_windows_for_the_shell_and_a_drive_alone_is_shown_itself()
    {
        var host = await DesktopAsync();
        var (folder, file) = Museum();
        var (project, step, mixed) = await DocumentAsync(host, file.Replace('\\', '/'));
        var drive = Path.GetPathRoot(root.FullName)!;
        var ofDrive = await OutputAsync(project, step, drive);
        using var reader = project.Reader;

        (await reader.PostAsync(Route(step, mixed))).Ok(204);
        (await reader.PostAsync(Route(step, ofDrive))).Ok(204);

        Assert.Equal([folder + @"\"], Shown(host));
        Assert.Equal(drive, host.Folders.Shown[^1]);
        Assert.All(host.Folders.Shown, shown => Assert.EndsWith(@"\", shown, StringComparison.Ordinal));
    }

    /// <summary>The server mode registers neither the opener nor its shell nor its look: its route can do nothing but refuse.</summary>
    [Fact]
    public async Task In_server_mode_the_route_is_not_found_for_every_member_and_the_host_holds_nothing_that_opens_a_folder()
    {
        var host = await hosts.OfAsync(EngineKind.SqlServer);
        var (project, step, document) = await DocumentAsync(host, Museum().Folder);
        using var reader = project.Reader;
        using var admin = project.Admin;

        (await reader.PostAsync(Route(step, document))).Refused(404, "INTROUVABLE");
        (await admin.PostAsync(Route(step, document))).Refused(404, "INTROUVABLE");

        Assert.Null(host.Services.GetService<IFolderOpener>());
        Assert.Null(host.Services.GetService<IFolderShell>());
        Assert.Null(host.Services.GetService<IFolderDisk>());
        Assert.Empty(host.Folders.Shown);
        Assert.IsType<CheckedFolderOpener>((await DesktopAsync()).Services.GetRequiredService<IFolderOpener>());
    }

    /// <summary>The user of a request that the route must not ask: the template of the reads asks him before anything else.</summary>
    private sealed class UnaskedUser : ICurrentUser
    {
        public int Asked { get; private set; }

        public CurrentUser? User
        {
            get
            {
                Asked++;
                return null;
            }
        }
    }

    /// <summary>
    /// The mode is decided before anything is read: outside the desktop mode, or without an opener, the handler answers
    /// INTROUVABLE without asking who calls, so without reading the document; with both, the read begins (control).
    /// </summary>
    [Theory]
    [InlineData(HostMode.Server, true, "INTROUVABLE", 0)]
    [InlineData(HostMode.Desktop, false, "INTROUVABLE", 0)]
    [InlineData(HostMode.Desktop, true, "NON_AUTHENTIFIE", 1)]
    public async Task The_mode_is_decided_before_the_document_is_read(HostMode mode, bool withOpener, string code, int asked)
    {
        var user = new UnaskedUser();
        var shell = new FolderRecorder();
        var opener = new CheckedFolderOpener(new FixedLook(FolderLook.Folder), shell, FolderOpeningOptions.Default);
        var handler = new DocumentFolderApi(new QueryRunner(null!, null!, null!, null!, user, null!), Options.Create(new ProductOptions { Mode = mode }),
            withOpener ? opener : null);

        var refusal = await Assert.ThrowsAsync<DomainException>(() =>
            handler.ShowAsync(Guid.CreateVersion7(), Guid.CreateVersion7(), TestContext.Current.CancellationToken));

        Assert.Equal((code, asked), (refusal.Code, user.Asked));
        Assert.Empty(shell.Shown);
    }

    public void Dispose() => root.Delete(recursive: true);
}
