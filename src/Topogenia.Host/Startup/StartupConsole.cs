// StartupConsole.cs
// The console of the host at its start: the sentences of the codes in the language of the user, and his answers when he is there; without a console, kept for the window that tells them (F-69).

using System.Globalization;
using Topogenia.Domain.Values;
using Topogenia.Host.Errors;

namespace Topogenia.Host.Startup;

/// <summary>
/// Writes the messages of the start (`Startup/messages.*.json`) in the language of the interface closest to the culture of
/// the process (AR-51: else the default language of the configuration), and reads an answer only when a person is at the
/// console (an interactive session whose input is not redirected): a service, a test or a redirected run never waits.
/// The console is the user's own window: it may show the folder of the backups; the logs never get a path (ADR 036).
/// In desktop mode without a console (<see cref="Windowed"/>, F-69, ADR 090), the sentences are kept until the start
/// tells them: in a box of Windows when the start is refused, by the icon of the application once it runs; a question
/// opens a console with the sentences before it, closed once answered.
/// </summary>
public sealed class StartupConsole
{
    private readonly TextWriter output;
    private readonly TextReader input;
    private readonly IStartupWindow? window;
    private readonly List<string> kept = [];

    public StartupConsole(LanguageSettings languages, TextWriter output, TextReader input, bool interactive)
        : this(languages, output, input, interactive, null)
    {
    }

    private StartupConsole(LanguageSettings languages, TextWriter output, TextReader input, bool interactive, IStartupWindow? window)
    {
        ArgumentNullException.ThrowIfNull(languages);
        this.output = output;
        this.input = input;
        this.window = window;
        Interactive = interactive;
        Language = languages.EffectiveLanguage(null, CultureInfo.CurrentUICulture.Name);
        Messages = ErrorMessages.Embedded($"{typeof(StartupConsole).Namespace}.messages.");
    }

    /// <summary>The console of the process.</summary>
    public static StartupConsole Of(LanguageSettings languages) =>
        new(languages, Console.Out, Console.In, Environment.UserInteractive && !Console.IsInputRedirected);

    /// <summary>The start of a desktop mode without a console: its sentences kept for <paramref name="window"/>, a person there to answer.</summary>
    public static StartupConsole Without(LanguageSettings languages, IStartupWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);
        return new(languages, TextWriter.Null, TextReader.Null, interactive: true, window);
    }

    public bool Interactive { get; }

    /// <summary>True when no console shows the sentences: the start keeps them for its window.</summary>
    public bool Windowed => window is not null;

    /// <summary>The window that tells the sentences when there is no console.</summary>
    public IStartupWindow? Window => window;

    public string Language { get; }

    public ErrorMessages Messages { get; }

    public void Write(string code, IReadOnlyDictionary<string, object?>? values = null) => WriteText(Messages.Format(code, Language, values));

    /// <summary>A message already written in the language of the console (the refusal of a command, from the messages of the API).</summary>
    public void WriteText(string text)
    {
        if (window is null)
        {
            output.WriteLine(text);
            return;
        }
        lock (kept)
            kept.Add(text);
    }

    /// <summary>The sentences kept since the last call, in their order; none with a console.</summary>
    public IReadOnlyList<string> TakeKept()
    {
        lock (kept)
        {
            var taken = kept.ToList();
            kept.Clear();
            return taken;
        }
    }

    /// <summary>The answer of the person at the console, or null when nobody can answer.</summary>
    public string? Ask(string code)
    {
        if (!Interactive)
            return null;
        if (window is null)
        {
            Write(code);
            return input.ReadLine();
        }
        return window.Ask([.. TakeKept(), Messages.Format(code, Language, null)]);
    }
}
