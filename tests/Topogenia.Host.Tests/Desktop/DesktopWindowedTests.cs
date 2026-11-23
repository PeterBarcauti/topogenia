// DesktopWindowedTests.cs
// The desktop mode without a console (F-69, ADR 090): the start keeps its sentences for its window, a refusal is shown in a box, the icon near the clock opens and quits the application, after the writes that the pages declare waiting (F-204, ADR 115), asking at once for a form not validated (R11).

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Topogenia.Domain.Values;
using Topogenia.Host.Composition;
using Topogenia.Host.Desktop;
using Topogenia.Host.Startup;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Desktop;

public sealed class DesktopWindowedTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static readonly LanguageSettings Languages = new LanguageOptions().ToSettings();

    /// <summary>The window of a test: what it was given to show, to ask and to confirm, and the answers it gives.</summary>
    private sealed class RecordedWindow(string? answer = null, bool confirm = false) : IStartupWindow
    {
        public List<(IReadOnlyList<string> Lines, bool Problem)> Shown { get; } = [];

        public List<IReadOnlyList<string>> Asked { get; } = [];

        public List<(IReadOnlyList<string> Lines, TimeSpan After)> Confirmed { get; } = [];

        /// <summary>When the run that this window serves was asked to quit, so that a box says how long it was waited for.</summary>
        public long QuitAsked { get; set; }

        public bool Confirm(IReadOnlyList<string> lines)
        {
            lock (Confirmed)
                Confirmed.Add((lines, Stopwatch.GetElapsedTime(QuitAsked)));
            return confirm;
        }

        public int ConfirmCount()
        {
            lock (Confirmed)
                return Confirmed.Count;
        }

        public void Show(IReadOnlyList<string> lines, bool problem)
        {
            lock (Shown)
                Shown.Add((lines, problem));
        }

        public string? Ask(IReadOnlyList<string> lines)
        {
            Asked.Add(lines);
            return answer;
        }

        public IReadOnlyList<string> AllShown()
        {
            lock (Shown)
                return [.. Shown.SelectMany(shown => shown.Lines)];
        }
    }

    private sealed class RecordedBrowser : IBrowserLauncher
    {
        public List<string> Addresses { get; } = [];

        public void Open(string address)
        {
            lock (Addresses)
                Addresses.Add(address);
        }
    }

    /// <summary>
    /// Without a console, the sentences are kept in their order until taken, a question opens the window with them and
    /// the question, a person is there to answer; with a console, nothing changes (written at once, nothing kept).
    /// </summary>
    [Fact]
    public void Without_a_console_the_sentences_are_kept_and_a_question_takes_them_to_the_window()
    {
        var window = new RecordedWindow("2");
        var console = StartupConsole.Without(Languages, window);
        console.Write("SAUVEGARDES_DOSSIER", new Dictionary<string, object?> { ["folder"] = "D:/sauvegardes" });
        console.WriteText("Deuxième phrase");

        var answer = console.Ask("RESTAURATION_QUESTION");

        Assert.True(console.Windowed);
        Assert.True(console.Interactive);
        Assert.Equal("2", answer);
        var asked = Assert.Single(window.Asked);
        Assert.Equal([console.Messages.Format("SAUVEGARDES_DOSSIER", console.Language, new Dictionary<string, object?> { ["folder"] = "D:/sauvegardes" }),
            "Deuxième phrase", console.Messages.Format("RESTAURATION_QUESTION", console.Language, null)], asked);
        Assert.Empty(console.TakeKept());
        console.Write("RESTAURATION_FAITE");
        Assert.Equal([console.Messages.Format("RESTAURATION_FAITE", console.Language, null)], console.TakeKept());
        Assert.Empty(console.TakeKept());

        var output = new StringWriter();
        var withConsole = new StartupConsole(Languages, output, TextReader.Null, interactive: false);
        withConsole.WriteText("Écrite");
        Assert.False(withConsole.Windowed);
        Assert.Equal("Écrite" + Environment.NewLine, output.ToString());
        Assert.Empty(withConsole.TakeKept());
    }

    /// <summary>The menu of the icon names the product in the language of the start, in each language of the interface.</summary>
    [Fact]
    public void Each_language_names_the_choices_of_the_icon()
    {
        var messages = new StartupConsole(Languages, TextWriter.Null, TextReader.Null, interactive: false).Messages;
        string[] languages = ["cs", "de", "el", "en", "es", "fr", "hu", "it", "ja", "nl", "pl", "pt", "ro", "ru", "sv", "tr", "uk", "zh"];
        foreach (var language in languages)
        {
            foreach (var code in new[] { "ICONE_OUVRIR", "ICONE_QUITTER" })
            {
                Assert.Contains("{product}", messages.Of(language)[code], StringComparison.Ordinal);
                var text = messages.Format(code, language, new Dictionary<string, object?> { ["product"] = ConsoleWindow.Title });
                Assert.Contains(ConsoleWindow.Title, text, StringComparison.Ordinal);
                Assert.DoesNotContain("{", text, StringComparison.Ordinal);
            }
            // The question of the box when a typing still waits (ADR 115), asked as a question in each language.
            var question = messages.Format(DesktopTrayService.WaitingQuestion, language, null).TrimEnd();
            Assert.False(string.IsNullOrWhiteSpace(question), language);
            Assert.DoesNotContain("{", question, StringComparison.Ordinal);
            Assert.True(question.EndsWith('?') || question.EndsWith('\uFF1F') || question.EndsWith(';'), $"{language}: {question}");
            // The question of the box when a form is not validated (R11), another sentence, a question too.
            var unsaved = messages.Format(DesktopTrayService.UnsavedQuestion, language, null).TrimEnd();
            Assert.DoesNotContain("{", unsaved, StringComparison.Ordinal);
            Assert.NotEqual(question, unsaved);
            Assert.True(unsaved.EndsWith('?') || unsaved.EndsWith('\uFF1F') || unsaved.EndsWith(';'), $"{language}: {unsaved}");
        }
        Assert.Equal("Un formulaire ouvert n'est pas enregistré : sa saisie sera perdue. Quitter quand même ?", messages.Format(DesktopTrayService.UnsavedQuestion, "fr", null));
        Assert.Equal("An open form is not saved: what is typed in it will be lost. Quit anyway?", messages.Format(DesktopTrayService.UnsavedQuestion, "en", null));
        Assert.Equal("Une saisie de fiche n'est pas encore enregistrée. Quitter quand même ?", messages.Format(DesktopTrayService.WaitingQuestion, "fr", null));
        Assert.Equal("A sheet entry is not saved yet. Quit anyway?", messages.Format(DesktopTrayService.WaitingQuestion, "en", null));
        Assert.Equal("Ouvrir Topogenia", messages.Format("ICONE_OUVRIR", "fr", new Dictionary<string, object?> { ["product"] = "Topogenia" }));
        Assert.Equal("Quit Topogenia", messages.Format("ICONE_QUITTER", "en", new Dictionary<string, object?> { ["product"] = "Topogenia" }));
    }

    /// <summary>
    /// A run without a console writes neither the address nor how to close a window: its icon says how to quit. The icon
    /// opens the browser with a new launch token, as a second start does, and quits the application, as the interface does.
    /// </summary>
    [Fact]
    public async Task Without_a_console_the_icon_opens_and_quits_the_application()
    {
        var window = new RecordedWindow();
        var browser = new RecordedBrowser();
        await using var run = await DesktopRun.StartAsync(browser: browser, window: window);
        var tray = run.Services.GetRequiredService<DesktopTrayService>();
        var console = run.Services.GetRequiredService<StartupConsole>();
        var first = run.LaunchToken;

        if (OperatingSystem.IsWindows())
            Assert.True(SpinWait.SpinUntil(() => tray.Shown, TimeSpan.FromSeconds(10)), "The icon is in the notification area.");
        var ready = console.Messages.Format("PRET", console.Language, new Dictionary<string, object?> { ["address"] = run.Origin.ToString() });
        Assert.DoesNotContain(window.AllShown().Concat(console.TakeKept()), line =>
            line == console.Messages.Format("QUITTER", console.Language, null) || line.StartsWith(ready.Split(':')[0], StringComparison.Ordinal));
        Assert.Equal(console.Messages.Format("ICONE_OUVRIR", console.Language, new Dictionary<string, object?> { ["product"] = ConsoleWindow.Title }),
            tray.TextOf(TrayCommand.Open));

        tray.Choose(TrayCommand.Open);

        var opened = Assert.Single(browser.Addresses);
        Assert.StartsWith(run.Origin.ToString().TrimEnd('/') + DesktopSession.LaunchPath, opened, StringComparison.Ordinal);
        Assert.NotEqual(first, run.LaunchToken);
        Assert.EndsWith(run.LaunchToken!, opened, StringComparison.Ordinal);

        var lifetime = run.Services.GetRequiredService<IHostApplicationLifetime>();
        tray.Choose(TrayCommand.Quit);

        Assert.True(lifetime.ApplicationStopping.IsCancellationRequested);
        await run.StopAsync();
        Assert.False(tray.Shown);
    }

    /// <summary>No page declares a write waiting (one said so, then that it was sent): the icon stops at once, nothing asked.</summary>
    [Fact]
    public async Task The_icon_quits_at_once_when_no_page_waits()
    {
        var window = new RecordedWindow();
        await using var run = await DesktopRun.StartAsync(window: window);
        using var client = await DesktopWritesRoutesTests.SignedInAsync(run);
        (await DesktopWritesRoutesTests.DeclareAsync(client, "tab-1", true)).Ok(204);
        (await DesktopWritesRoutesTests.DeclareAsync(client, "tab-1", false)).Ok(204);
        var stopping = run.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping;

        run.Services.GetRequiredService<DesktopTrayService>().Choose(TrayCommand.Quit);

        Assert.True(stopping.IsCancellationRequested);
        Assert.Equal(0, window.ConfirmCount());
    }

    /// <summary>
    /// A page declared a typing (its sheet not yet saved): « Quit » by the icon does not stop the application while it
    /// waits, and stops it once the page says that it was sent; nothing is asked. The menu of the icon is never held: the
    /// choice returns at once.
    /// </summary>
    [Fact]
    public async Task The_icon_waits_for_the_page_that_declared_a_typing_and_stops_once_it_is_sent()
    {
        var window = new RecordedWindow();
        await using var run = await DesktopRun.StartAsync(window: window);
        using var client = await DesktopWritesRoutesTests.SignedInAsync(run);
        (await DesktopWritesRoutesTests.DeclareAsync(client, "tab-1", true)).Ok(204);
        var stopping = run.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping;
        var chosen = Stopwatch.GetTimestamp();

        run.Services.GetRequiredService<DesktopTrayService>().Choose(TrayCommand.Quit);

        Assert.True(Stopwatch.GetElapsedTime(chosen) < TimeSpan.FromMilliseconds(200), "The thread of the icon is not held by the wait.");
        await Task.Delay(500, Token);
        Assert.False(stopping.IsCancellationRequested, "The application stopped with a typing still waiting.");
        (await client.GetAsync("/api/me")).Ok();
        (await DesktopWritesRoutesTests.DeclareAsync(client, "tab-1", false)).Ok(204);
        Assert.True(SpinWait.SpinUntil(() => stopping.IsCancellationRequested, TimeSpan.FromSeconds(2)), "The application stops once the typing is sent.");
        Assert.Equal(0, window.ConfirmCount());
    }

    /// <summary>
    /// A typing that cannot be sent (a bound of duration upside down, a refusal): after five seconds, a box of Windows asks
    /// the person, once however many times « Quit » is chosen meanwhile; No keeps the application running.
    /// </summary>
    [Fact]
    public async Task A_typing_still_waiting_after_five_seconds_asks_the_person_and_no_keeps_the_application_running()
    {
        var window = new RecordedWindow(confirm: false);
        await using var run = await DesktopRun.StartAsync(window: window);
        using var client = await DesktopWritesRoutesTests.SignedInAsync(run);
        (await DesktopWritesRoutesTests.DeclareAsync(client, "tab-1", true)).Ok(204);
        var stopping = run.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping;
        var tray = run.Services.GetRequiredService<DesktopTrayService>();
        var console = run.Services.GetRequiredService<StartupConsole>();
        window.QuitAsked = Stopwatch.GetTimestamp();

        tray.Choose(TrayCommand.Quit);
        tray.Choose(TrayCommand.Quit);

        Assert.True(SpinWait.SpinUntil(() => window.ConfirmCount() > 0, TimeSpan.FromSeconds(10)), "The box is shown.");
        await Task.Delay(500, Token);
        var (lines, after) = Assert.Single(window.Confirmed);
        Assert.Equal([console.Messages.Format(DesktopTrayService.WaitingQuestion, console.Language, null)], lines);
        Assert.True(after >= DesktopTrayService.WritesBound - TimeSpan.FromMilliseconds(100), $"The box came after {after}.");
        Assert.False(stopping.IsCancellationRequested, "No keeps the application running.");
        (await client.GetAsync("/api/me")).Ok();
    }

    /// <summary>The same box: Yes stops the application, the typing left behind.</summary>
    [Fact]
    public async Task A_typing_still_waiting_after_five_seconds_asks_the_person_and_yes_stops_it()
    {
        var window = new RecordedWindow(confirm: true);
        await using var run = await DesktopRun.StartAsync(window: window);
        using var client = await DesktopWritesRoutesTests.SignedInAsync(run);
        (await DesktopWritesRoutesTests.DeclareAsync(client, "tab-1", true)).Ok(204);
        var stopping = run.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping;
        window.QuitAsked = Stopwatch.GetTimestamp();

        run.Services.GetRequiredService<DesktopTrayService>().Choose(TrayCommand.Quit);

        Assert.True(SpinWait.SpinUntil(() => stopping.IsCancellationRequested, TimeSpan.FromSeconds(10)), "Yes stops the application.");
        var (_, after) = Assert.Single(window.Confirmed);
        Assert.True(after >= DesktopTrayService.WritesBound - TimeSpan.FromMilliseconds(100), $"The box came after {after}.");
    }

    /// <summary>
    /// A page declared a form changed and not validated (R11): it never leaves by itself, so the box asks at once, not
    /// after five seconds, its own question; the menu of the icon is not held; No keeps the application running.
    /// </summary>
    [Fact]
    public async Task A_form_not_validated_asks_at_once_and_no_keeps_the_application_running()
    {
        var window = new RecordedWindow(confirm: false);
        await using var run = await DesktopRun.StartAsync(window: window);
        using var client = await DesktopWritesRoutesTests.SignedInAsync(run);
        (await DesktopWritesRoutesTests.DeclareAsync(client, "tab-1", false, true)).Ok(204);
        var stopping = run.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping;
        var console = run.Services.GetRequiredService<StartupConsole>();
        window.QuitAsked = Stopwatch.GetTimestamp();

        run.Services.GetRequiredService<DesktopTrayService>().Choose(TrayCommand.Quit);

        Assert.True(Stopwatch.GetElapsedTime(window.QuitAsked) < TimeSpan.FromMilliseconds(200), "The thread of the icon is not held by the box.");
        Assert.True(SpinWait.SpinUntil(() => window.ConfirmCount() > 0, TimeSpan.FromSeconds(10)), "The box is shown.");
        var (lines, after) = Assert.Single(window.Confirmed);
        Assert.Equal([console.Messages.Format(DesktopTrayService.UnsavedQuestion, console.Language, null)], lines);
        Assert.True(after < TimeSpan.FromSeconds(1), $"The box came after {after}, not at once.");
        await Task.Delay(500, Token);
        Assert.False(stopping.IsCancellationRequested, "No keeps the application running.");
        (await client.GetAsync("/api/me")).Ok();
    }

    /// <summary>
    /// A form not validated in a tab, a write still waiting in another: the box asks at once; Yes lets the write that
    /// leaves by itself reach the host before the stop, without a second question.
    /// </summary>
    [Fact]
    public async Task A_form_not_validated_asks_at_once_and_yes_stops_once_the_writes_waiting_are_sent()
    {
        var window = new RecordedWindow(confirm: true);
        await using var run = await DesktopRun.StartAsync(window: window);
        using var client = await DesktopWritesRoutesTests.SignedInAsync(run);
        (await DesktopWritesRoutesTests.DeclareAsync(client, "tab-1", false, true)).Ok(204);
        (await DesktopWritesRoutesTests.DeclareAsync(client, "tab-2", true)).Ok(204);
        var stopping = run.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping;
        window.QuitAsked = Stopwatch.GetTimestamp();

        run.Services.GetRequiredService<DesktopTrayService>().Choose(TrayCommand.Quit);

        Assert.True(SpinWait.SpinUntil(() => window.ConfirmCount() > 0, TimeSpan.FromSeconds(10)), "The box is shown.");
        Assert.True(Assert.Single(window.Confirmed).After < TimeSpan.FromSeconds(1), "The box comes at once.");
        await Task.Delay(500, Token);
        Assert.False(stopping.IsCancellationRequested, "The write of the second tab is still on its way.");
        (await DesktopWritesRoutesTests.DeclareAsync(client, "tab-2", false)).Ok(204);
        Assert.True(SpinWait.SpinUntil(() => stopping.IsCancellationRequested, TimeSpan.FromSeconds(2)), "Yes, the write sent: the application stops.");
        Assert.Equal(1, window.ConfirmCount());
    }

    /// <summary>Yes to a form not validated, nothing else waiting: the application stops at once after the answer.</summary>
    [Fact]
    public async Task A_form_not_validated_alone_and_yes_stops_at_once()
    {
        var window = new RecordedWindow(confirm: true);
        await using var run = await DesktopRun.StartAsync(window: window);
        using var client = await DesktopWritesRoutesTests.SignedInAsync(run);
        (await DesktopWritesRoutesTests.DeclareAsync(client, "tab-1", false, true)).Ok(204);
        var stopping = run.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping;
        window.QuitAsked = Stopwatch.GetTimestamp();

        run.Services.GetRequiredService<DesktopTrayService>().Choose(TrayCommand.Quit);

        Assert.True(SpinWait.SpinUntil(() => stopping.IsCancellationRequested, TimeSpan.FromSeconds(10)), "Yes stops the application.");
        Assert.True(Stopwatch.GetElapsedTime(window.QuitAsked) < TimeSpan.FromSeconds(2), "Stopped at once after the answer.");
        Assert.Equal(1, window.ConfirmCount());
    }

    /// <summary>
    /// The writes waited for are sent, but the page now holds a form not validated: the icon does not stop, it asks the
    /// question of the form at once rather than after five seconds.
    /// </summary>
    [Fact]
    public async Task A_form_left_not_validated_once_the_writes_are_sent_is_asked_for()
    {
        var window = new RecordedWindow(confirm: false);
        await using var run = await DesktopRun.StartAsync(window: window);
        using var client = await DesktopWritesRoutesTests.SignedInAsync(run);
        (await DesktopWritesRoutesTests.DeclareAsync(client, "tab-1", true)).Ok(204);
        var stopping = run.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping;
        var console = run.Services.GetRequiredService<StartupConsole>();
        window.QuitAsked = Stopwatch.GetTimestamp();

        run.Services.GetRequiredService<DesktopTrayService>().Choose(TrayCommand.Quit);
        (await DesktopWritesRoutesTests.DeclareAsync(client, "tab-1", false, true)).Ok(204);

        Assert.True(SpinWait.SpinUntil(() => window.ConfirmCount() > 0, TimeSpan.FromSeconds(10)), "The box is shown.");
        var (lines, after) = Assert.Single(window.Confirmed);
        Assert.Equal([console.Messages.Format(DesktopTrayService.UnsavedQuestion, console.Language, null)], lines);
        Assert.True(after < TimeSpan.FromSeconds(2), $"The box came after {after}.");
        await Task.Delay(300, Token);
        Assert.False(stopping.IsCancellationRequested, "No keeps the application running.");
    }

    /// <summary>Two tabs each declared a typing: the icon waits for both, the first sent is not enough.</summary>
    [Fact]
    public async Task Two_tabs_are_waited_for_each()
    {
        var window = new RecordedWindow();
        await using var run = await DesktopRun.StartAsync(window: window);
        using var client = await DesktopWritesRoutesTests.SignedInAsync(run);
        (await DesktopWritesRoutesTests.DeclareAsync(client, "tab-1", true)).Ok(204);
        (await DesktopWritesRoutesTests.DeclareAsync(client, "tab-2", true)).Ok(204);
        var stopping = run.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping;

        run.Services.GetRequiredService<DesktopTrayService>().Choose(TrayCommand.Quit);

        (await DesktopWritesRoutesTests.DeclareAsync(client, "tab-1", false)).Ok(204);
        await Task.Delay(500, Token);
        Assert.False(stopping.IsCancellationRequested, "The second tab still waits.");
        (await DesktopWritesRoutesTests.DeclareAsync(client, "tab-2", false)).Ok(204);
        Assert.True(SpinWait.SpinUntil(() => stopping.IsCancellationRequested, TimeSpan.FromSeconds(2)), "Both sent, the application stops.");
        Assert.Equal(0, window.ConfirmCount());
    }

    /// <summary>
    /// The box of Windows that asks whether to quit anyway: Yes and No, No the button by default (Enter keeps the
    /// application running), a warning, in the foreground and above the other windows; only Yes quits, a box that could
    /// not be shown (0) does not.
    /// </summary>
    [Fact]
    public void The_box_of_the_icon_asks_yes_or_no_with_no_by_default()
    {
        const uint buttons = 0xF;
        const uint defaultButton = 0xF00;
        const uint icon = 0xF0;
        Assert.Equal(0x4u, ConsoleWindow.ConfirmStyle & buttons);
        Assert.Equal(0x100u, ConsoleWindow.ConfirmStyle & defaultButton);
        Assert.Equal(0x30u, ConsoleWindow.ConfirmStyle & icon);
        Assert.Equal(0x50000u, ConsoleWindow.ConfirmStyle & 0x50000u);
        Assert.True(ConsoleWindow.IsYes(6));
        Assert.False(ConsoleWindow.IsYes(7));
        Assert.False(ConsoleWindow.IsYes(0));
    }

    /// <summary>With a console, the run shows no icon: the console says how to quit.</summary>
    [Fact]
    public async Task With_a_console_there_is_no_icon()
    {
        await using var run = await DesktopRun.StartAsync();

        Assert.False(run.Services.GetRequiredService<StartupConsole>().Windowed);
        Assert.False(run.Services.GetRequiredService<DesktopTrayService>().Shown);
    }

    /// <summary>
    /// Without a console, a start refused (the folder of the data cannot be created) is shown in a box of Windows, as a
    /// problem, in the language of the user and without a path; nothing goes to the output; the exit code is 1.
    /// </summary>
    [Fact]
    public async Task Without_a_console_a_refused_start_is_shown_in_a_box()
    {
        var folder = Directory.CreateTempSubdirectory($"{TestDatabase.FolderPrefix}{Environment.ProcessId}-");
        try
        {
            await File.WriteAllTextAsync(Path.Combine(folder.FullName, "data"), "", Token);
            var window = new RecordedWindow();
            var output = new StringWriter();

            var code = await Program.RunAsync([], output, Configured(Bare(folder)), Token, window);

            Assert.Equal(Program.Problem, code);
            Assert.Equal(string.Empty, output.ToString());
            var (lines, problem) = Assert.Single(window.Shown);
            Assert.True(problem);
            Assert.Contains(lines, line => line.Contains("Le compte n'a pas les droits nécessaires", StringComparison.Ordinal));
            Assert.DoesNotContain(lines, line => line.Contains(folder.FullName, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            await RemoveAsync(folder);
        }
    }

    /// <summary>Without a console, a failure the start does not foresee (its port taken) shows ERREUR_INTERNE in a box.</summary>
    [Fact]
    public async Task Without_a_console_an_unforeseen_failure_is_shown_in_a_box()
    {
        var folder = Directory.CreateTempSubdirectory($"{TestDatabase.FolderPrefix}{Environment.ProcessId}-");
        using var taken = new TcpListener(IPAddress.Loopback, 0);
        taken.Start();
        try
        {
            var settings = Bare(folder);
            settings[$"{ProductOptions.Section}:Desktop:Port"] = ((IPEndPoint)taken.LocalEndpoint).Port.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var window = new RecordedWindow();

            var code = await Program.RunAsync([], TextWriter.Null, Configured(settings), Token, window);

            Assert.Equal(Program.Problem, code);
            var internalError = new StartupConsole(Languages, TextWriter.Null, TextReader.Null, interactive: false).Messages.Of("fr")["ERREUR_INTERNE"];
            var (lines, problem) = Assert.Single(window.Shown);
            Assert.True(problem);
            Assert.Equal([internalError], lines);
        }
        finally
        {
            await RemoveAsync(folder);
        }
    }

    private static Dictionary<string, string?> Bare(DirectoryInfo folder) => new()
    {
        [$"{ProductOptions.Section}:Desktop:Folder"] = folder.FullName,
        [$"{ProductOptions.Section}:Desktop:OpenBrowser"] = "false",
    };

    /// <summary>
    /// The settings of a run of the executable and, in the place of the shell of Windows, a recorder: a run that came to
    /// listen would serve requests in desktop mode, and no run of the tests opens a real Explorer (ADR 104).
    /// </summary>
    private static Action<WebApplicationBuilder> Configured(Dictionary<string, string?> settings) => builder =>
    {
        builder.Configuration.AddInMemoryCollection(settings);
        builder.Services.AddSingleton<IFolderShell>(new FolderRecorder());
    };

    private static async Task RemoveAsync(DirectoryInfo folder)
    {
        for (var attempt = 1; ; attempt++)
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            try
            {
                folder.Delete(recursive: true);
                return;
            }
            catch (IOException) when (attempt < 10)
            {
                await Task.Delay(200, Token);
            }
        }
    }
}
