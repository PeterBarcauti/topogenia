// Program.cs
// Entry point of the host process: runs a command of the command line, or builds the web application for its mode and runs it; a non-zero code when refused.

using Topogenia.Domain.Errors;
using Topogenia.Domain.Values;
using Topogenia.Host.Composition;
using Topogenia.Host.Desktop;
using Topogenia.Host.Startup;

return await Program.RunAsync(args, Console.Out, null, CancellationToken.None);

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
    /// </summary>
    public static async Task<int> RunAsync(string[] args, TextWriter output, Action<WebApplicationBuilder>? configure, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        WebApplication? application = null;
        try
        {
            if (HostCommands.Parse(args) is not null)
                return await HostCommands.RunAsync(args, output, configure, cancellationToken);
            application = HostApplication.Build(args, configure);
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
            // The console already says why, in the language of the user (StartupService).
            return Problem;
        }
        catch (DesktopSettingsException settings)
        {
            // The settings of the user could not be read: the console says it, in the language of the system, with the property at fault.
            new StartupConsole(new LanguageOptions().ToSettings(), output, TextReader.Null, interactive: false)
                .Write(DesktopSettings.InvalidSettings, new Dictionary<string, object?> { ["property"] = settings.Property ?? DesktopPaths.SettingsFileName });
            return Problem;
        }
        catch (Exception failure) when (failure is not HostAbortedException)
        {
            // HostAbortedException is the stop of the tests of the API once they hold the application they built.
            Fail(application?.Services, failure, output);
            return Problem;
        }
        finally
        {
            if (application is not null)
                await application.DisposeAsync();
        }
    }

    /// <summary>The log gets the type of the failure, the console the sentence of ERREUR_INTERNE; the languages of the code when the services are unusable.</summary>
    private static void Fail(IServiceProvider? services, Exception failure, TextWriter output)
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
        new StartupConsole(languages, output, TextReader.Null, interactive: false).Write(ErrorCodes.InternalError);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "The host failed: {Type}.")]
    private static partial void LogFailed(ILogger logger, string type);
}
