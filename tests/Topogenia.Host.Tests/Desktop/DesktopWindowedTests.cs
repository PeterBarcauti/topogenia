// DesktopWindowedTests.cs
// The desktop mode without a console (F-69, ADR 090): the start keeps its sentences for its window, a refusal is shown in a box, the icon near the clock opens and quits the application.

using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Topogenia.Domain.Values;
using Topogenia.Host.Composition;
using Topogenia.Host.Desktop;
using Topogenia.Host.Startup;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Desktop;

public sealed class DesktopWindowedTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static readonly LanguageSettings Languages = new LanguageOptions().ToSettings();

    /// <summary>The window of a test: what it was given to show and to ask, and the answer it gives.</summary>
    private sealed class RecordedWindow(string? answer = null) : IStartupWindow
    {
        public List<(IReadOnlyList<string> Lines, bool Problem)> Shown { get; } = [];

        public List<IReadOnlyList<string>> Asked { get; } = [];

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
        }
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

            var code = await Program.RunAsync([], output, builder => builder.Configuration.AddInMemoryCollection(Bare(folder)), Token, window);

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

            var code = await Program.RunAsync([], TextWriter.Null, builder => builder.Configuration.AddInMemoryCollection(settings), Token, window);

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
