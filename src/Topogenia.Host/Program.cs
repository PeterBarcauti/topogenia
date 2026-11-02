// Program.cs
// Entry point of the host process: runs a command of the command line, or builds the web application for its mode and runs it; a non-zero code when refused; without a console, the boxes of Windows tell it (F-69).

using Microsoft.Extensions.Options;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Values;
using Topogenia.Host.Composition;
using Topogenia.Host.Desktop;
using Topogenia.Host.Startup;

// First, before anything writes to it: the console made for the desktop mode alone is released (F-69, ADR 090).
var windowed = ConsoleWindow.Detach(args);
return await Program.RunAsync(args, windowed ? TextWriter.Null : Console.Out, null, CancellationToken.None, windowed ? new ConsoleWindow() : null);

/// <summary>The program of the host, named so that the tests of the API can start it.</summary>
public partial class Program
{
    public const int Success = 0;

    public const int Problem = 1;

    /// <summary>
    /// Runs the executable: a command of the command line (<see cref="HostCommands"/>), else the application of its mode
    /// until it stops. A refusal of the start has already been written to the console in the language of the user (AR-15);
    /// any other failure, unforeseen, writes <see cref="ErrorCodes.InternalError"/> to <paramref name="output"/> and its type
    /// to the log, never its message nor its stack, which may name a file, a base or an account (ADR 036, ADR 044): nothing
    /// leaves the process unhandled, so the runtime writes nothing to the console nor to the event log of Windows.
    /// <paramref name="configure"/> and <paramref name="cancellationToken"/> let a test add its configuration and bound the run.
    /// With <paramref name="window"/> (no console a person sees, F-69, ADR 090), the desktop mode keeps the sentences of
    /// its start for that window: a refusal, invalid settings or a failure are shown in a box of Windows.
    /// </summary>
    public static async Task<int> RunAsync(string[] args, TextWriter output, Action<WebApplicationBuilder>? configure, CancellationToken cancellationToken,
        IStartupWindow? window = null)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        WebApplication? application = null;
        try
        {
            if (HostCommands.Parse(args) is not null)
                return await HostCommands.RunAsync(args, output, configure, cancellationToken);
            application = HostApplication.Build(args, window is null ? configure : builder =>
            {
                // Before the console of the services (TryAdd): the desktop mode tells its start through the window.
                builder.Services.AddSingleton(provider => provider.GetRequiredService<IOptions<ProductOptions>>().Value.Mode == HostMode.Desktop
                    ? StartupConsole.Without(provider.GetRequiredService<LanguageSettings>(), window)
                    : StartupConsole.Of(provider.GetRequiredService<LanguageSettings>()));
                configure?.Invoke(builder);
            });
            await application.StartAsync(cancellationToken);
            await application.WaitForShutdownAsync(cancellationToken);
            return Success;
        }
        catch (StartupHandedOverException)
        {
            // Spec 6.1: the application already running opened the browser on itself; the console says it.
            return Success;
        }
        catch (StartupRefusedException)
        {
            // The console already says why, in the language of the user (StartupService); without one, its box.
            if (application?.Services.GetService<StartupConsole>() is { Window: { } shown } console)
                shown.Show(console.TakeKept(), problem: true);
            return Problem;
        }
        catch (DesktopSettingsException settings)
        {
            // The settings of the user could not be read: the console says it, in the language of the system, with the property at fault.
            var console = new StartupConsole(new LanguageOptions().ToSettings(), output, TextReader.Null, interactive: false);
            var values = new Dictionary<string, object?> { ["property"] = settings.Property ?? DesktopPaths.SettingsFileName };
            console.Write(DesktopSettings.InvalidSettings, values);
            window?.Show([console.Messages.Format(DesktopSettings.InvalidSettings, console.Language, values)], problem: true);
            return Problem;
        }
        catch (Exception failure) when (failure is not HostAbortedException)
        {
            // HostAbortedException is the stop of the tests of the API once they hold the application they built.
            Fail(application?.Services, failure, output, window);
            return Problem;
        }
        finally
        {
            if (application is not null)
                await application.DisposeAsync();
        }
    }

    /// <summary>
    /// The log gets the type of the failure, the console the sentence of ERREUR_INTERNE, the window too when there is no
    /// console; the languages of the code when the services are unusable.
    /// </summary>
    private static void Fail(IServiceProvider? services, Exception failure, TextWriter output, IStartupWindow? window)
    {
        var languages = new LanguageOptions().ToSettings();
        if (services is not null)
        {
            try
            {
                languages = services.GetRequiredService<LanguageSettings>();
                LogFailed(services.GetRequiredService<ILoggerFactory>().CreateLogger<Program>(), failure.GetType().FullName ?? failure.GetType().Name);
            }
            catch (Exception unusable) when (unusable is not OutOfMemoryException)
            {
                // The services may be the cause of the failure (an invalid configuration): the console still says it.
            }
        }
        var console = new StartupConsole(languages, output, TextReader.Null, interactive: false);
        console.Write(ErrorCodes.InternalError);
        window?.Show([console.Messages.Format(ErrorCodes.InternalError, console.Language, null)], problem: true);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "The host failed: {Type}.")]
    private static partial void LogFailed(ILogger logger, string type);
}
