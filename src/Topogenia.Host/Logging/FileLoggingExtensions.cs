// FileLoggingExtensions.cs
// Registers the daily file log of the host in a given folder (ADR 021).

using Topogenia.Application.Ports;

namespace Topogenia.Host.Logging;

/// <summary>Registration of <see cref="FileLoggerProvider"/> on a logging builder.</summary>
public static class FileLoggingExtensions
{
    /// <summary>
    /// Adds the daily file log, written in <paramref name="folder"/> with the time of the registered <see cref="IClock"/>.
    /// Its minimal level follows the <c>Logging</c> section, the provider alias being <c>File</c>.
    /// </summary>
    public static ILoggingBuilder AddDailyLogFile(this ILoggingBuilder builder, string folder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        builder.Services.AddSingleton<ILoggerProvider>(services => new FileLoggerProvider(folder, services.GetRequiredService<IClock>()));
        return builder;
    }
}
