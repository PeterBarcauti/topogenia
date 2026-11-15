// FileLogger.cs
// One category of the file log: level, category and message on one line, an exception by its type and HResult only (ADR 036).

using System.Globalization;
using System.Text;

namespace Topogenia.Host.Logging;

/// <summary>
/// The logger of one category for <see cref="FileLoggerProvider"/>. An entry is its level, its category and the message
/// formatted by <c>ILogger</c>, on one line: a line break of the message is written as <c>\n</c> or <c>\r</c>, so that a
/// message cannot forge another entry. An exception is written by its full type name and its HResult only, never by its
/// message, its stack nor its <c>ToString()</c>, which may name the account, the base or the file (ADR 036); its inner
/// exceptions follow in the same way, one per line. The minimal level is filtered by the logging framework from the
/// <c>Logging</c> section; whatever that section says, the categories of the framework (<c>Microsoft.</c>, <c>System.</c>)
/// are written from <see cref="LogLevel.Error"/> only: below, they name the content root, the web root, the keys and the
/// query string of each request (spec 5.2, ADR 036).
/// </summary>
internal sealed class FileLogger : ILogger
{
    private readonly string category;
    private readonly FileLoggerProvider provider;

    public FileLogger(string category, FileLoggerProvider provider)
    {
        this.category = category;
        this.provider = provider;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) =>
        logLevel != LogLevel.None && (logLevel >= LogLevel.Error || !IsFramework(category));

    /// <summary>A category of the framework, written from the errors only.</summary>
    public static bool IsFramework(string category) =>
        category.StartsWith("Microsoft.", StringComparison.Ordinal) || category.StartsWith("System.", StringComparison.Ordinal);

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        if (!IsEnabled(logLevel))
            return;
        var entry = new StringBuilder();
        entry.Append(NameOf(logLevel)).Append(' ').Append(category).Append(' ');
        AppendOneLine(entry, formatter(state, exception));
        if (exception is not null)
            AppendFailure(entry, "exception", exception);
        provider.Write(entry.ToString());
    }

    private static string NameOf(LogLevel level) => level switch
    {
        LogLevel.Trace => "Trace",
        LogLevel.Debug => "Debug",
        LogLevel.Information => "Information",
        LogLevel.Warning => "Warning",
        LogLevel.Error => "Error",
        _ => "Critical",
    };

    private static void AppendOneLine(StringBuilder entry, string message)
    {
        foreach (var character in message)
        {
            if (character == '\n')
                entry.Append("\\n");
            else if (character == '\r')
                entry.Append("\\r");
            else
                entry.Append(character);
        }
    }

    /// <summary>One line per exception: its role, its full type name, its HResult; then its inner exceptions.</summary>
    private static void AppendFailure(StringBuilder entry, string role, Exception failure)
    {
        var type = failure.GetType();
        entry.Append("\n  ").Append(role).Append(' ').Append(type.FullName ?? type.Name)
            .Append(" HResult 0x").Append(failure.HResult.ToString("X8", CultureInfo.InvariantCulture));
        IEnumerable<Exception> inner = failure switch
        {
            AggregateException aggregate => aggregate.InnerExceptions,
            { InnerException: { } single } => [single],
            _ => [],
        };
        foreach (var each in inner)
            AppendFailure(entry, "inner", each);
    }
}
