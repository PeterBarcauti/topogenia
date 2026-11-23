// DesktopWritesRoutesTests.cs
// The declaration by a page of the desktop mode of what it still has to send to the host (F-204, ADR 115) and of a form changed and not validated (R11): its bounds (64 characters, 32 tabs), the session it needs, nothing in server mode.

using Microsoft.Extensions.DependencyInjection;
using Topogenia.Host.Desktop;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Desktop;

/// <summary>
/// `POST /api/desktop/writes` `{ tab, waiting }`: each tab of the desktop mode says whether a typing or a write of a
/// document still waits to reach the host, so that « Quit » by the icon near the clock waits for it
/// (<see cref="DesktopWrites"/>). A run of its own for each test that counts the tabs (the state is the one of its
/// host); SQL Server runs the server mode (`ApiHost`).
/// </summary>
public sealed class DesktopWritesRoutesTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    public const string Route = "/api/desktop/writes";

    /// <summary>A client of the run with the session cookie of its launch token.</summary>
    public static async Task<ApiClient> SignedInAsync(DesktopRun run)
    {
        ArgumentNullException.ThrowIfNull(run);
        var client = run.Client();
        var cookie = await run.SignInAsync(client);
        client.DefaultRequestHeaders.Add("Cookie", cookie);
        return new ApiClient(client);
    }

    /// <summary>What a tab declares, as the page sends it.</summary>
    public static Task<ApiResponse> DeclareAsync(ApiClient client, string tab, bool waiting)
    {
        ArgumentNullException.ThrowIfNull(client);
        return client.PostAsync(Route, new { tab, waiting });
    }

    /// <summary>What a tab declares with a form changed and not validated (R11), as the page sends it.</summary>
    public static Task<ApiResponse> DeclareAsync(ApiClient client, string tab, bool waiting, bool unsaved)
    {
        ArgumentNullException.ThrowIfNull(client);
        return client.PostAsync(Route, new { tab, waiting, unsaved });
    }

    /// <summary>
    /// A form not validated is counted apart from the writes waiting (R11): `unsaved` absent reads false; a tab that says
    /// both counts once within the 32 tabs; the same bounds refuse a 33rd tab and a name too long.
    /// </summary>
    [Fact]
    public async Task A_form_not_validated_is_counted_apart_and_a_tab_counts_once_within_the_same_bounds()
    {
        await using var run = await DesktopRun.StartAsync();
        using var client = await SignedInAsync(run);
        var writes = run.Services.GetRequiredService<DesktopWrites>();

        (await DeclareAsync(client, "tab-1", false, true)).Ok(204);
        Assert.Equal((0, 1), (writes.Waiting, writes.Unsaved));
        (await DeclareAsync(client, "tab-1", true, true)).Ok(204);
        Assert.Equal((1, 1), (writes.Waiting, writes.Unsaved));
        (await DeclareAsync(client, "tab-1", true)).Ok(204);
        Assert.Equal((1, 0), (writes.Waiting, writes.Unsaved));
        (await DeclareAsync(client, "tab-1", false, true)).Ok(204);
        Assert.Equal((0, 1), (writes.Waiting, writes.Unsaved));
        (await DeclareAsync(client, "tab-1", false)).Ok(204);
        Assert.Equal((0, 0), (writes.Waiting, writes.Unsaved));
        Assert.Equal(("tab", DesktopWrites.MaxTabLength), Bound((await DeclareAsync(client, new string('a', DesktopWrites.MaxTabLength + 1), false, true)).Refused(400, "TEXTE_TROP_LONG")));

        for (var tab = 1; tab <= DesktopWrites.MaxTabs / 2; tab++)
            (await DeclareAsync(client, $"form-{tab}", false, true)).Ok(204);
        for (var tab = 1; tab <= DesktopWrites.MaxTabs / 2; tab++)
            (await DeclareAsync(client, $"write-{tab}", true)).Ok(204);
        Assert.Equal((DesktopWrites.MaxTabs / 2, DesktopWrites.MaxTabs / 2), (writes.Waiting, writes.Unsaved));
        Assert.Equal(("tab", DesktopWrites.MaxTabs), Bound((await DeclareAsync(client, "thirty-third", false, true)).Refused(400, "VALEUR_INVALIDE")));
        Assert.Equal(("tab", DesktopWrites.MaxTabs), Bound((await DeclareAsync(client, "thirty-third", true)).Refused(400, "VALEUR_INVALIDE")));
        // A tab counted already says both: it keeps its one place.
        (await DeclareAsync(client, "form-1", true, true)).Ok(204);
        (await DeclareAsync(client, "write-1", true, true)).Ok(204);
        Assert.Equal((DesktopWrites.MaxTabs / 2 + 1, DesktopWrites.MaxTabs / 2 + 1), (writes.Waiting, writes.Unsaved));
        // A tab that has nothing left, form included, leaves its place.
        (await DeclareAsync(client, "form-1", false)).Ok(204);
        (await DeclareAsync(client, "thirty-third", false, true)).Ok(204);
        Assert.Equal((DesktopWrites.MaxTabs / 2, DesktopWrites.MaxTabs / 2 + 1), (writes.Waiting, writes.Unsaved));
    }

    private static (string?, int) Bound(System.Text.Json.JsonElement refusal) =>
        (refusal.GetProperty("field").GetString(), refusal.GetProperty("max").GetInt32());

    [Fact]
    public async Task A_tab_name_too_long_or_a_thirty_third_tab_is_refused_400()
    {
        await using var run = await DesktopRun.StartAsync();
        using var client = await SignedInAsync(run);
        var writes = run.Services.GetRequiredService<DesktopWrites>();

        var tooLong = (await DeclareAsync(client, new string('a', DesktopWrites.MaxTabLength + 1), true)).Refused(400, "TEXTE_TROP_LONG");
        Assert.Equal(("tab", DesktopWrites.MaxTabLength), (tooLong.GetProperty("field").GetString(), tooLong.GetProperty("max").GetInt32()));
        Assert.Equal("tab", (await DeclareAsync(client, "onglet 1", true)).Refused(400, "VALEUR_INVALIDE").GetProperty("field").GetString());
        Assert.Equal("tab", (await client.PostAsync(Route, new { waiting = true })).Refused(400, "CHAMP_OBLIGATOIRE").GetProperty("field").GetString());
        Assert.Equal("waiting", (await client.PostAsync(Route, new { tab = "a" })).Refused(400, "CHAMP_OBLIGATOIRE").GetProperty("field").GetString());
        Assert.Equal(0, writes.Waiting);

        (await DeclareAsync(client, new string('a', DesktopWrites.MaxTabLength), true)).Ok(204);
        for (var tab = 2; tab <= DesktopWrites.MaxTabs; tab++)
            (await DeclareAsync(client, $"{Guid.NewGuid()}", true)).Ok(204);
        Assert.Equal(DesktopWrites.MaxTabs, writes.Waiting);

        var thirtyThird = (await DeclareAsync(client, "thirty-third", true)).Refused(400, "VALEUR_INVALIDE");
        Assert.Equal(("tab", DesktopWrites.MaxTabs), (thirtyThird.GetProperty("field").GetString(), thirtyThird.GetProperty("max").GetInt32()));
        // A tab already counted says it again; one that has sent everything leaves its place.
        (await DeclareAsync(client, new string('a', DesktopWrites.MaxTabLength), true)).Ok(204);
        (await DeclareAsync(client, "thirty-third", false)).Ok(204);
        Assert.Equal(DesktopWrites.MaxTabs, writes.Waiting);
        (await DeclareAsync(client, new string('a', DesktopWrites.MaxTabLength), false)).Ok(204);
        (await DeclareAsync(client, "thirty-third", true)).Ok(204);
        Assert.Equal(DesktopWrites.MaxTabs, writes.Waiting);
    }

    [Fact]
    public async Task The_declaration_needs_the_session_and_answers_404_in_server_mode()
    {
        await using var run = await DesktopRun.StartAsync();
        using (var anonymous = new ApiClient(run.Client()))
            Assert.Equal(401, (await DeclareAsync(anonymous, "a", true)).Status);
        var writes = run.Services.GetRequiredService<DesktopWrites>();
        Assert.Equal(0, writes.Waiting);
        using (var client = await SignedInAsync(run))
        {
            (await DeclareAsync(client, "a", true)).Ok(204);
            Assert.Equal(1, writes.Waiting);
            (await DeclareAsync(client, "a", false)).Ok(204);
            Assert.Equal(0, writes.Waiting);
        }

        var server = await hosts.OfAsync(EngineKind.SqlServer);
        using var admin = server.As(Logins.Admin);
        (await DeclareAsync(admin, "a", true)).Refused(404, "INTROUVABLE");
        Assert.Null(server.Services.GetService<DesktopWrites>());
    }
}
