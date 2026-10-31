// StartupConsole.cs
// The console of the host at its start: the sentences of the codes in the language of the user, and his answers when he is there.

using System.Globalization;
using Topogenia.Domain.Values;
using Topogenia.Host.Errors;

namespace Topogenia.Host.Startup;

/// <summary>
/// Writes the messages of the start (`Startup/messages.*.json`) in the language of the interface closest to the culture of
/// the process (AR-51: else the default language of the configuration), and reads an answer only when a person is at the
/// console (an interactive session whose input is not redirected): a service, a test or a redirected run never waits.
/// The console is the user's own window: it may show the folder of the backups; the logs never get a path (ADR 036).
/// </summary>
public sealed class StartupConsole
{
    private readonly TextWriter output;
    private readonly TextReader input;

    public StartupConsole(LanguageSettings languages, TextWriter output, TextReader input, bool interactive)
    {
        ArgumentNullException.ThrowIfNull(languages);
        this.output = output;
        this.input = input;
        Interactive = interactive;
        Language = languages.EffectiveLanguage(null, CultureInfo.CurrentUICulture.Name);
        Messages = ErrorMessages.Embedded($"{typeof(StartupConsole).Namespace}.messages.");
    }

    /// <summary>The console of the process.</summary>
    public static StartupConsole Of(LanguageSettings languages) =>
        new(languages, Console.Out, Console.In, Environment.UserInteractive && !Console.IsInputRedirected);

    public bool Interactive { get; }

    public string Language { get; }

    public ErrorMessages Messages { get; }

    public void Write(string code, IReadOnlyDictionary<string, object?>? values = null) => output.WriteLine(Messages.Format(code, Language, values));

    /// <summary>A message already written in the language of the console (the refusal of a command, from the messages of the API).</summary>
    public void WriteText(string text) => output.WriteLine(text);

    /// <summary>The answer of the person at the console, or null when nobody can answer.</summary>
    public string? Ask(string code)
    {
        if (!Interactive)
            return null;
        Write(code);
        return input.ReadLine();
    }
}
