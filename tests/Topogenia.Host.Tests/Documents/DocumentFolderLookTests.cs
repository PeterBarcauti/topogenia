// DocumentFolderLookTests.cs
// « Open the folder » of a document when Windows refuses, fails or does not answer (ADR 104), through a desktop host whose look or shell is replaced: access denied, unreachable, the bound of the look, a second click, a late answer.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Topogenia.Host.Desktop;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Persistence.Tests.Infrastructure;
using static Topogenia.Host.Tests.Documents.DocumentFolderRoutesTests;

namespace Topogenia.Host.Tests.Documents;

/// <summary>
/// Each test runs a variant of the desktop host of the class (`ApiHost.WithVariantAsync`) on the same base. No test waits
/// for a real delay: the look that does not answer is held by the test, and the bound it exceeds is of some milliseconds.
/// </summary>
public sealed class DocumentFolderLookTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private const string Folder = @"\\serveur\partage\dossier";

    private static Action<IServiceCollection> Look(IFolderDisk look, TimeSpan? bound = null) => services =>
    {
        services.RemoveAll<IFolderDisk>();
        services.AddSingleton(look);
        if (bound is { } timeout)
        {
            services.RemoveAll<FolderOpeningOptions>();
            services.AddSingleton(new FolderOpeningOptions(timeout));
        }
    };

    [Theory]
    [InlineData(FolderLook.Denied, 403, "DOSSIER_ACCES_REFUSE")]
    [InlineData(FolderLook.Unreachable, 409, "DOSSIER_INJOIGNABLE")]
    [InlineData(FolderLook.Missing, 404, "DOSSIER_INTROUVABLE")]
    public async Task What_the_look_finds_is_the_refusal_and_nothing_is_shown(FolderLook look, int status, string code)
    {
        var host = await hosts.OfAsync(EngineKind.Sqlite);
        var (_, step, document) = await DocumentAsync(host, Folder + @"\inventaire.xlsx");
        var before = host.Folders.Shown.Count;

        await host.WithVariantAsync(Look(new FixedLook(look)), async clientOf =>
        {
            using var reader = clientOf(Logins.Reader);
            Assert.Empty((await reader.PostAsync(Route(step, document))).Refused(status, code).EnumerateObject());
        });

        Assert.Equal(before, host.Folders.Shown.Count);
    }

    /// <summary>The folder was there at the look and the shell fails all the same: its failure is the refusal.</summary>
    [Theory]
    [InlineData(FolderOutcome.NotFound, 404, "DOSSIER_INTROUVABLE")]
    [InlineData(FolderOutcome.Denied, 403, "DOSSIER_ACCES_REFUSE")]
    [InlineData(FolderOutcome.Unreachable, 409, "DOSSIER_INJOIGNABLE")]
    public async Task What_the_shell_answers_is_the_refusal(FolderOutcome answer, int status, string code)
    {
        var host = await hosts.OfAsync(EngineKind.Sqlite);
        var (_, step, document) = await DocumentAsync(host, Folder);
        var shell = new FolderRecorder { Reply = answer };

        await host.WithVariantAsync(services =>
        {
            Look(new FixedLook(FolderLook.Folder))(services);
            services.RemoveAll<IFolderShell>();
            services.AddSingleton<IFolderShell>(shell);
        }, async clientOf =>
        {
            using var reader = clientOf(Logins.Reader);
            (await reader.PostAsync(Route(step, document))).Refused(status, code);
        });

        Assert.Equal([Folder + @"\"], shell.Shown);
    }

    [Fact]
    public async Task A_server_that_does_not_answer_in_time_is_unreachable_a_second_click_is_refused_at_once_and_the_late_answer_shows_nothing()
    {
        var host = await hosts.OfAsync(EngineKind.Sqlite);
        var (_, step, document) = await DocumentAsync(host, Folder);
        var before = host.Folders.Shown.Count;
        using var server = new HeldLook();

        await host.WithVariantAsync(Look(server, TimeSpan.FromMilliseconds(30)), async clientOf =>
        {
            using var reader = clientOf(Logins.Reader);
            (await reader.PostAsync(Route(step, document))).Refused(409, "DOSSIER_INJOIGNABLE");
            // The look still waits for the server: the next click is refused at once and starts no second look.
            (await reader.PostAsync(Route(step, document))).Refused(409, "DOSSIER_OUVERTURE_EN_COURS");
            Assert.Equal(1, server.Looks);
            Assert.Equal(before, host.Folders.Shown.Count);

            server.Release();
            await FreeAsync(reader, Route(step, document));
        });

        // The answer that came after the bound showed nothing: the only folder shown is the one of the click made afterwards.
        Assert.Equal([Folder + @"\"], host.Folders.Shown.Skip(before));
    }

    [Fact]
    public async Task While_a_look_waits_within_its_bound_a_second_click_is_refused_at_once_and_the_first_is_shown_when_its_answer_comes()
    {
        var host = await hosts.OfAsync(EngineKind.Sqlite);
        var (_, step, document) = await DocumentAsync(host, Folder);
        var before = host.Folders.Shown.Count;
        using var server = new HeldLook();

        await host.WithVariantAsync(Look(server, TimeSpan.FromMinutes(5)), async clientOf =>
        {
            using var reader = clientOf(Logins.Reader);
            var first = reader.PostAsync(Route(step, document));
            await server.WaitingAsync();

            (await reader.PostAsync(Route(step, document))).Refused(409, "DOSSIER_OUVERTURE_EN_COURS");
            Assert.Equal(before, host.Folders.Shown.Count);

            server.Release();
            (await first).Ok(204);
        });

        Assert.Equal([Folder + @"\"], host.Folders.Shown.Skip(before));
    }

    /// <summary>Asks again until the look let go by the test has ended: the click is then taken, and shown.</summary>
    private static async Task FreeAsync(ApiClient client, string route)
    {
        using var bound = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        bound.CancelAfter(TimeSpan.FromMinutes(1));
        while ((await client.PostAsync(route)).Status != 204)
            await Task.Delay(1, bound.Token);
    }
}
