// DesktopStartupTests.cs
// The start of the desktop mode: data file refused by its place, folders, schema refused, backup before migration failed, folders readable by others, unforeseen failure, refusals of the mode, safe defaults, settings file (spec 4.4, 5.1, 5.2, 6.1, 6.2, AR-98).

using System.Net;
using System.Net.Sockets;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Topogenia.Application.Ports;
using Topogenia.Host.Composition;
using Topogenia.Host.Desktop;
using Topogenia.Host.Errors;
using Topogenia.Host.Startup;
using Topogenia.Persistence.Sqlite.Files;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Desktop;

public sealed class DesktopStartupTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static readonly DataFileEnvironment Local = DataFileEnvironment.From(_ => null, _ => false) with { IsReadableByOthers = _ => false };

    private static readonly Domain.Values.LanguageSettings Languages =
        new(["fr", "en"], "fr", new Dictionary<string, string> { ["fr"] = "Acteur 1", ["en"] = "Actor 1" });

    private static DesktopPaths Paths() => new(Path.Combine(Path.GetTempPath(), "product"), Path.Combine(Path.GetTempPath(), "product", "data", "topogenia.db"));

    [Fact]
    public async Task A_data_file_in_a_synchronised_folder_is_refused_with_its_place_before_anything_is_opened()
    {
        var synchronised = Path.Combine(Path.GetTempPath(), "Synchronised folder");
        var paths = new DesktopPaths(synchronised, Path.Combine(synchronised, "topogenia.db"));
        var environment = DataFileEnvironment.From(name => name == "OneDrive" ? synchronised : null, _ => false);
        var migrations = new ScriptedMigrations();
        var database = new DesktopDatabase(paths, migrations, new ScriptedBackups(), environment, new CapturingLogger<DesktopDatabase>());
        var refusal = await Assert.ThrowsAsync<StartupRefusedException>(() => database.OpenAsync(Token));
        Assert.Equal((DataFileRefusals.SynchronisedFolder, (object?)synchronised), (refusal.Code, refusal.Details["place"]));
        Assert.Equal(0, migrations.Checks);
        Assert.DoesNotContain(synchronised, refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_schema_refused_stops_the_start_with_its_code_and_logs_the_numbers_of_the_engine_never_its_message()
    {
        var migrations = new ScriptedMigrations
        {
            Refusal = new SchemaRefusedException(SchemaCodes.MigrationChanged, "Initial", new EngineFailureException([26, 538])),
        };
        var logger = new CapturingLogger<DesktopDatabase>();
        var database = new DesktopDatabase(Paths(), migrations, new ScriptedBackups(), Local, logger);
        var refusal = await Assert.ThrowsAsync<StartupRefusedException>(() => database.OpenAsync(Token));
        Assert.Equal((SchemaCodes.MigrationChanged, (object?)"Initial"), (refusal.Code, refusal.Details["migration"]));
        var line = Assert.Single(logger.Lines);
        Assert.Contains("MIGRATION_MODIFIEE", line, StringComparison.Ordinal);
        Assert.Contains("26,538", line, StringComparison.Ordinal);
        Assert.DoesNotContain(Path.GetTempPath(), line, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_failed_backup_before_a_migration_stops_the_start_without_migrating()
    {
        var migrations = new ScriptedMigrations { State = new SchemaState(SchemaStatus.Behind, ["Initial"], ["Next"]) };
        var backups = new ScriptedBackups { Result = new BackupResult(false, false, null, "SAUVEGARDE_IMPOSSIBLE") };
        var database = new DesktopDatabase(Paths(), migrations, backups, Local, new CapturingLogger<DesktopDatabase>());
        var refusal = await Assert.ThrowsAsync<StartupRefusedException>(() => database.OpenAsync(Token));
        Assert.Equal((StartupCodes.MigrationWithoutBackup, (object?)"SAUVEGARDE_IMPOSSIBLE"), (refusal.Code, refusal.Details["failure"]));
        Assert.Equal(0, migrations.Migrations);
        Assert.Equal([BackupReason.BeforeMigration], backups.Reasons);
    }

    [Fact]
    public async Task A_folder_readable_by_the_other_accounts_of_the_machine_is_given_by_its_place_and_logged_without_its_path()
    {
        var paths = Paths();
        var dataFolder = Path.GetDirectoryName(paths.DataFile)!;
        var environment = Local with { IsReadableByOthers = folder => folder == dataFolder };
        var logger = new CapturingLogger<DesktopDatabase>();
        var places = await new DesktopDatabase(paths, new ScriptedMigrations(), new ScriptedBackups(), environment, logger).OpenAsync(Token);
        Assert.Equal([DesktopDatabase.DataPlace], places);
        var line = Assert.Single(logger.Lines, line => line.Contains("readable", StringComparison.Ordinal));
        Assert.Contains("folder of the data is readable", line, StringComparison.Ordinal);
        Assert.DoesNotContain(Path.GetTempPath(), line, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Each_exposed_folder_is_named_at_the_console_with_the_recommendation_of_the_place_by_default()
    {
        var paths = Paths();
        var backupsOnly = Local with { IsReadableByOthers = folder => folder == paths.BackupsFolder };
        Assert.Equal([DesktopDatabase.BackupsPlace], DesktopDatabase.ExposedPlaces(paths, backupsOnly));
        var both = Local with { IsReadableByOthers = _ => true };
        var places = DesktopDatabase.ExposedPlaces(paths, both);
        Assert.Equal([DesktopDatabase.DataPlace, DesktopDatabase.BackupsPlace], places);
        var output = new StringWriter();
        var console = new StartupConsole(Languages, output, TextReader.Null, interactive: false);
        StartupService.WarnOfExposedFolders(console, paths, places);
        var lines = output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        Assert.Contains($"le dossier du fichier de données ({Path.GetDirectoryName(paths.DataFile)})", lines[0], StringComparison.Ordinal);
        Assert.Contains($"le dossier des sauvegardes ({paths.BackupsFolder})", lines[1], StringComparison.Ordinal);
        Assert.All(lines, line => Assert.Contains("L'emplacement par défaut", line, StringComparison.Ordinal));
        Assert.Empty(DesktopDatabase.ExposedPlaces(paths, Local));
    }

    /// <summary>AR-98 at the real start of the desktop mode, on a data folder that the users of the machine may read (a real ACL).</summary>
    [Fact]
    [SupportedOSPlatform("windows")]
    public async Task The_start_of_the_desktop_mode_names_at_the_console_a_data_folder_that_the_users_of_the_machine_may_read()
    {
        var folder = NewFolder();
        var data = Directory.CreateDirectory(Path.Combine(folder.FullName, "data"));
        try
        {
            var rules = data.GetAccessControl();
            rules.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null), FileSystemRights.ReadData,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
            data.SetAccessControl(rules);
            var console = new StringWriter();
            await RunBareAsync(folder, _ => Task.CompletedTask, console);
            var line = Assert.Single(console.ToString().Split(Environment.NewLine), line => line.StartsWith("Attention", StringComparison.Ordinal));
            Assert.Contains($"le dossier du fichier de données ({data.FullName}) est lisible par les autres comptes", line, StringComparison.Ordinal);
            Assert.Contains("L'emplacement par défaut, dans le profil de l'utilisateur, est recommandé", line, StringComparison.Ordinal);
        }
        finally
        {
            await RemoveAsync(folder);
        }
    }

    /// <summary>
    /// A folder of the data file that cannot be created, a file in its place or a folder above it that the account may not
    /// write: the start is refused with its code, which no backup mends, and the exit code 1; the console names no path and
    /// shows no stack, the log gives the place and the type of the failure only (ADR 036, ADR 044).
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [SupportedOSPlatform("windows")]
    public async Task A_folder_of_the_data_file_that_cannot_be_created_refuses_the_start_with_its_code_and_never_a_path_nor_a_stack(bool denied)
    {
        var folder = NewFolder();
        var locked = Directory.CreateDirectory(Path.Combine(folder.FullName, "locked"));
        var deny = new FileSystemAccessRule(WindowsIdentity.GetCurrent().User!, FileSystemRights.CreateDirectories, AccessControlType.Deny);
        try
        {
            var settings = new Dictionary<string, string?>();
            if (denied)
            {
                var rules = locked.GetAccessControl();
                rules.AddAccessRule(deny);
                locked.SetAccessControl(rules);
                settings[$"{ProductOptions.Section}:Desktop:DataFile"] = Path.Combine(locked.FullName, "files", "topogenia.db");
            }
            else
            {
                await File.WriteAllTextAsync(Path.Combine(folder.FullName, "data"), "", Token);
            }
            var (code, output) = await RunProgramAsync(folder, settings);
            Assert.Equal(Program.Problem, code);
            Assert.Contains("Le compte n'a pas les droits nécessaires", output, StringComparison.Ordinal);
            AssertNeitherPathNorStack(folder, output);
            var log = await LogOfAsync(folder);
            var type = denied ? typeof(UnauthorizedAccessException) : typeof(IOException);
            Assert.Contains($"The folder of the data cannot be created: {type.FullName}", log, StringComparison.Ordinal);
            Assert.DoesNotContain(folder.FullName, log, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            var rules = locked.GetAccessControl();
            rules.RemoveAccessRule(deny);
            locked.SetAccessControl(rules);
            await RemoveAsync(folder);
        }
    }

    /// <summary>
    /// A failure the start does not foresee (here, the fixed port of the settings already taken): ERREUR_INTERNE in the
    /// language of the user and the exit code 1, the log names its type; neither the console nor the log shows a path, a
    /// message of the failure or a stack, and nothing leaves the process for the runtime to write elsewhere.
    /// </summary>
    [Fact]
    public async Task An_unforeseen_failure_of_the_start_says_the_internal_error_in_the_language_of_the_user_without_a_path_nor_a_stack()
    {
        var folder = NewFolder();
        using var taken = new TcpListener(IPAddress.Loopback, 0);
        taken.Start();
        try
        {
            var port = ((IPEndPoint)taken.LocalEndpoint).Port.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var (code, output) = await RunProgramAsync(folder, new Dictionary<string, string?> { [$"{ProductOptions.Section}:Desktop:Port"] = port });
            Assert.Equal(Program.Problem, code);
            var internalError = new StartupConsole(Languages, TextWriter.Null, TextReader.Null, interactive: false).Messages.Of("fr")["ERREUR_INTERNE"];
            Assert.Equal(internalError, output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)[^1]);
            AssertNeitherPathNorStack(folder, output);
            Assert.DoesNotContain(port, output, StringComparison.Ordinal);
            var log = await LogOfAsync(folder);
            Assert.Contains($"The host failed: {typeof(IOException).FullName}.", log, StringComparison.Ordinal);
            Assert.DoesNotContain(folder.FullName, log, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("   at ", log, StringComparison.Ordinal);
        }
        finally
        {
            await RemoveAsync(folder);
        }
    }

    [Fact]
    public async Task A_failed_backup_of_the_day_is_logged_and_the_start_goes_on()
    {
        var backups = new ScriptedBackups { Result = new BackupResult(false, false, null, "SAUVEGARDE_DOSSIER_REFUSE") };
        var logger = new CapturingLogger<DesktopDatabase>();
        await new DesktopDatabase(Paths(), new ScriptedMigrations(), backups, Local, logger).OpenAsync(Token);
        Assert.Equal([BackupReason.Daily], backups.Reasons);
        Assert.Contains("SAUVEGARDE_DOSSIER_REFUSE", Assert.Single(logger.Lines), StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_server_mode_with_sqlite_is_refused_at_the_start()
    {
        var section = ProductOptions.Section;
        var refusal = await Assert.ThrowsAsync<StartupRefusedException>(() => DesktopRun.StartAsync(new Dictionary<string, string?>
        {
            [$"{section}:Mode"] = nameof(HostMode.Server),
            [$"{section}:Database:Provider"] = nameof(DatabaseKind.Sqlite),
            [$"{section}:Database:ConnectionString"] = "Data Source=unused.db",
        }));
        Assert.Equal(StartupCodes.ServerModeWithSqlite, refusal.Code);
    }

    [Fact]
    public async Task The_whole_host_refuses_to_start_on_a_data_file_in_a_folder_named_by_onedrive()
    {
        var folder = Directory.CreateTempSubdirectory($"{TestDatabase.FolderPrefix}{Environment.ProcessId}-");
        try
        {
            var refusal = await Assert.ThrowsAsync<StartupRefusedException>(() => DesktopRun.StartAsync(new Dictionary<string, string?>
            {
                [$"{ProductOptions.Section}:Desktop:DataFile"] = Path.Combine(folder.FullName, "OneDrive - Fictive", "topogenia.db"),
            }));
            Assert.Equal(DataFileRefusals.SynchronisedFolder, refusal.Code);
            Assert.False(Directory.Exists(Path.Combine(folder.FullName, "OneDrive - Fictive")));
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    [Fact]
    public void Without_a_section_of_the_product_the_languages_take_their_defaults_from_the_code()
    {
        var empty = ProductOptions.Of(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build()).Languages.ToSettings();
        Assert.Equal(["cs", "de", "en", "es", "fr", "it", "hu", "nl", "pl", "pt", "ro", "sv", "tr", "el", "ru", "uk", "zh", "ja"], empty.Languages);
        Assert.Equal(("fr", "Acteur 1", "Actor 1"), (empty.DefaultLanguage, empty.FirstLaneName("fr"), empty.FirstLaneName("en")));
        var english = new LanguageOptions { Supported = ["en"] }.ToSettings();
        Assert.Equal(("en", "en", "Actor 1"), (Assert.Single(english.Languages), english.DefaultLanguage, english.FirstLaneName("en")));
    }

    [Fact]
    public void The_paths_of_the_product_default_to_the_local_application_data_and_resolve_a_relative_data_file_in_the_folder()
    {
        var defaults = DesktopPaths.Of(new DesktopOptions());
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        Assert.Equal(Path.Combine(local, "Topogenia"), defaults.Folder);
        Assert.Equal(Path.Combine(local, "Topogenia", "data", "topogenia.db"), defaults.DataFile);
        Assert.Equal((Path.Combine(local, "Topogenia", "backups"), Path.Combine(local, "Topogenia", "logs")), (defaults.BackupsFolder, defaults.LogsFolder));
        var moved = DesktopPaths.Of(new DesktopOptions { Folder = @"C:\Product", DataFile = @"files\mine.db" });
        Assert.Equal(@"C:\Product\files\mine.db", moved.DataFile);
    }

    /// <summary>
    /// A missing setting takes the safest value (spec 5.1): a configuration that names neither `Mode` nor the port (only
    /// the folder of the product, and no browser) starts the desktop mode, listening on a port chosen free at the start, on
    /// the loopback only. Two hosts so started at once listen on two ports: a fixed port by default would refuse the second.
    /// </summary>
    [Fact]
    public async Task Without_a_mode_nor_a_port_the_host_starts_in_desktop_mode_on_a_free_loopback_port()
    {
        var (firstFolder, secondFolder) = (NewFolder(), NewFolder());
        try
        {
            await RunBareAsync(firstFolder, first => RunBareAsync(secondFolder, second =>
            {
                foreach (var application in new[] { first, second })
                {
                    var options = application.Services.GetRequiredService<IOptions<ProductOptions>>().Value;
                    Assert.Equal((HostMode.Desktop, 0), (options.Mode, options.Desktop.Port));
                    Assert.NotNull(application.Services.GetRequiredService<DesktopSession>().LaunchToken);
                    var address = AddressOf(application);
                    Assert.Equal(IPAddress.Loopback, IPAddress.Parse(address.Host));
                    Assert.NotEqual(0, address.Port);
                }
                Assert.NotEqual(AddressOf(first).Port, AddressOf(second).Port);
                return Task.CompletedTask;
            }));
        }
        finally
        {
            await RemoveAsync(firstFolder);
            await RemoveAsync(secondFolder);
        }
    }

    /// <summary>
    /// The optional `settings.json` of the folder of the product (spec 6.1, R544-545) is read at its place by the start,
    /// whose configuration gives none of its keys: the host listens on its port, creates the data file where it says
    /// (relative to the folder) and answers in its language.
    /// </summary>
    [Fact]
    public async Task A_settings_file_in_the_folder_of_the_product_gives_the_start_its_port_its_data_file_and_its_language()
    {
        var folder = NewFolder();
        try
        {
            var port = FreePort();
            await File.WriteAllTextAsync(Path.Combine(folder.FullName, DesktopPaths.SettingsFileName),
                JsonSerializer.Serialize(new Dictionary<string, object> { ["Port"] = port, ["DataFile"] = "files/mine.db", ["Language"] = "en" }), Token);
            await RunBareAsync(folder, async application =>
            {
                Assert.Equal(new Uri($"http://127.0.0.1:{port}"), AddressOf(application));
                Assert.True(File.Exists(Path.Combine(folder.FullName, "files", "mine.db")));
                Assert.False(File.Exists(Path.Combine(folder.FullName, "data", "topogenia.db")));
                Assert.Equal("en", application.Services.GetRequiredService<Domain.Values.LanguageSettings>().DefaultLanguage);
                using var client = new HttpClient(new SocketsHttpHandler { UseCookies = false, AllowAutoRedirect = false }) { BaseAddress = AddressOf(application) };
                var token = application.Services.GetRequiredService<DesktopSession>().LaunchToken;
                using var launched = await client.GetAsync(new Uri($"{DesktopSession.LaunchPath}?{DesktopSession.TokenParameter}={token}", UriKind.Relative), Token);
                using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/api/nowhere", UriKind.Relative));
                request.Headers.Add("Cookie", launched.Headers.GetValues("Set-Cookie").Single().Split(';')[0]);
                using var response = await client.SendAsync(request, Token);
                var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token)).RootElement;
                Assert.Equal((HttpStatusCode.NotFound, "INTROUVABLE"), (response.StatusCode, body.GetProperty("code").GetString()));
                Assert.Equal(ErrorMessages.Embedded().Of("en")["INTROUVABLE"], body.GetProperty("message").GetString());
            });
        }
        finally
        {
            await RemoveAsync(folder);
        }
    }

    private static DirectoryInfo NewFolder() => Directory.CreateTempSubdirectory($"{TestDatabase.FolderPrefix}{Environment.ProcessId}-");

    /// <summary>
    /// The host built like the executable (<see cref="HostApplication"/>) on a configuration that names only the folder of
    /// the product and no browser: unlike <see cref="DesktopRun"/>, neither the mode, nor the port, nor the language. It
    /// runs <paramref name="work"/>, then stops as the user quits it; the console of the start goes to <paramref name="console"/>
    /// when it is given.
    /// </summary>
    private static async Task RunBareAsync(DirectoryInfo folder, Func<WebApplication, Task> work, TextWriter? console = null)
    {
        var application = HostApplication.Build([], builder =>
        {
            builder.Configuration.AddInMemoryCollection(Bare(folder));
            if (console is not null)
                WriteConsoleTo(builder, console);
        });
        try
        {
            await application.StartAsync(Token);
            await work(application);
        }
        finally
        {
            await application.StopAsync(CancellationToken.None);
            await application.DisposeAsync();
        }
    }

    private static Dictionary<string, string?> Bare(DirectoryInfo folder) => new()
    {
        [$"{ProductOptions.Section}:Desktop:Folder"] = folder.FullName,
        [$"{ProductOptions.Section}:Desktop:OpenBrowser"] = "false",
    };

    /// <summary>
    /// The console of the start (<see cref="StartupConsole"/>) writes to <paramref name="output"/>: registered once the host
    /// has registered its own (the container is configured after the services); the start itself is left as it is.
    /// </summary>
    private static void WriteConsoleTo(WebApplicationBuilder builder, TextWriter output) =>
        builder.Host.ConfigureContainer<IServiceCollection>((_, services) => services.AddSingleton(provider =>
            new StartupConsole(provider.GetRequiredService<Domain.Values.LanguageSettings>(), output, TextReader.Null, interactive: false)));

    /// <summary>
    /// The executable run as the user runs it (<see cref="Program.RunAsync"/>) on <see cref="Bare"/> and <paramref name="settings"/>:
    /// its exit code and its console, that of the start included. A start that does not fail stops after two minutes.
    /// </summary>
    private static async Task<(int Code, string Output)> RunProgramAsync(DirectoryInfo folder, IReadOnlyDictionary<string, string?> settings)
    {
        var configuration = Bare(folder);
        foreach (var (key, value) in settings)
            configuration[key] = value;
        var output = new StringWriter();
        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(Token);
        bounded.CancelAfter(TimeSpan.FromMinutes(2));
        var code = await Program.RunAsync([], output, builder =>
        {
            builder.Configuration.AddInMemoryCollection(configuration);
            WriteConsoleTo(builder, output);
        }, bounded.Token);
        return (code, output.ToString());
    }

    private static void AssertNeitherPathNorStack(DirectoryInfo folder, string output)
    {
        Assert.DoesNotContain(folder.FullName, output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Path.GetTempPath(), output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("   at ", output, StringComparison.Ordinal);
        Assert.DoesNotContain("Exception", output, StringComparison.Ordinal);
    }

    /// <summary>The content of the file log of the product, once the run is over.</summary>
    private static async Task<string> LogOfAsync(DirectoryInfo folder)
    {
        var contents = new List<string>();
        foreach (var file in Directory.GetFiles(Path.Combine(folder.FullName, "logs")))
            contents.Add(await File.ReadAllTextAsync(file, Token));
        return string.Join('\n', contents);
    }

    private static Uri AddressOf(WebApplication application) =>
        new(Assert.Single(application.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses));

    /// <summary>A port of the loopback free at the time of the call.</summary>
    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    /// <summary>Removes a folder of a test once the pools of SQLite have let its data file go.</summary>
    private static async Task RemoveAsync(DirectoryInfo folder)
    {
        for (var attempt = 1; ; attempt++)
        {
            SqliteConnection.ClearAllPools();
            folder.Refresh();
            if (!folder.Exists)
                return;
            try
            {
                folder.Delete(recursive: true);
                return;
            }
            catch (IOException) when (attempt < 50)
            {
                await Task.Delay(100);
            }
        }
    }

    private sealed class ScriptedMigrations : ISchemaMigrations
    {
        public SchemaRefusedException? Refusal { get; init; }

        public SchemaState State { get; init; } = new(SchemaStatus.UpToDate, ["Initial"], []);

        public int Checks { get; private set; }

        public int Migrations { get; private set; }

        public Task<SchemaState> CheckAsync(CancellationToken cancellationToken)
        {
            Checks++;
            return Refusal is null ? Task.FromResult(State) : throw Refusal;
        }

        public Task<SchemaState> MigrateAsync(CancellationToken cancellationToken)
        {
            Migrations++;
            return Task.FromResult(State with { Status = SchemaStatus.UpToDate, Pending = [] });
        }
    }
}

/// <summary>A backup service that answers what the test sets and records the reasons it was asked for.</summary>
internal sealed class ScriptedBackups : IBackupService
{
    public BackupResult Result { get; init; } = new(true, false, "backup.db", null);

    public RestoreResult Restore { get; init; } = new(true, null);

    public List<BackupReason> Reasons { get; } = [];

    public List<string> Restored { get; } = [];

    public Task<BackupResult> BackupAsync(BackupReason reason, CancellationToken cancellationToken)
    {
        Reasons.Add(reason);
        return Task.FromResult(Result);
    }

    public Task<RestoreResult> RestoreAsync(string backupFile, CancellationToken cancellationToken)
    {
        Restored.Add(backupFile);
        return Task.FromResult(Restore);
    }
}

/// <summary>A logger that keeps the formatted lines, the exception included as the provider would see it.</summary>
internal sealed class CapturingLogger<T> : ILogger<T>
{
    public List<string> Lines { get; } = [];

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        Lines.Add(formatter(state, exception));
    }
}
