// HostCommandsTests.cs
// The command line of the executable: --migrate of the server mode and --check of both modes, their messages and their exit codes (spec 6.3, A-4-38).

using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Topogenia.Application.Ports;
using Topogenia.Host.Composition;
using Topogenia.Host.Desktop;
using Topogenia.Host.Startup;
using Topogenia.Host.Tests.Desktop;
using Topogenia.Persistence.Schema;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Startup;

public sealed class HostCommandsTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static readonly string Section = ProductOptions.Section;

    private static Dictionary<string, string?> Languages() => new()
    {
        [$"{Section}:Languages:Supported:0"] = "fr",
        [$"{Section}:Languages:Supported:1"] = "en",
        [$"{Section}:Languages:Default"] = "fr",
        [$"{Section}:Languages:FirstLaneNames:fr"] = "Acteur 1",
        [$"{Section}:Languages:FirstLaneNames:en"] = "Actor 1",
    };

    private static Dictionary<string, string?> Desktop(DirectoryInfo folder) => new(Languages())
    {
        [$"{Section}:Mode"] = nameof(HostMode.Desktop),
        [$"{Section}:Desktop:Folder"] = folder.FullName,
        [$"{Section}:Desktop:OpenBrowser"] = "false",
    };

    private static Dictionary<string, string?> Server(string connectionString, DirectoryInfo folder) => new(Languages())
    {
        [$"{Section}:Mode"] = nameof(HostMode.Server),
        [$"{Section}:Database:Provider"] = nameof(DatabaseKind.SqlServer),
        [$"{Section}:Database:ConnectionString"] = connectionString,
        [$"{Section}:Server:AllowedHosts:0"] = "topogenia.test",
        [$"{Section}:Server:LogsFolder"] = Path.Combine(folder.FullName, "logs"),
    };

    /// <summary>Runs the command with the configuration given; the exit code and what the console said.</summary>
    private static async Task<(int Code, string Output)> RunAsync(IReadOnlyDictionary<string, string?> configuration, params string[] args)
    {
        var output = new StringWriter();
        var code = await HostCommands.RunAsync(args, output, builder => builder.Configuration.AddInMemoryCollection(configuration), Token);
        return (code, output.ToString());
    }

    private static DirectoryInfo NewFolder() => Directory.CreateTempSubdirectory($"{TestDatabase.FolderPrefix}{Environment.ProcessId}-");

    [Fact]
    public void The_commands_are_found_in_any_case_and_the_other_arguments_go_to_the_configuration()
    {
        Assert.Null(HostCommands.Parse(["--urls", "http://localhost:5000"]));
        var parsed = HostCommands.Parse(["--CHECK", "--Topogenia:Mode=Server"])!.Value;
        Assert.Equal([HostCommands.Check], parsed.Commands);
        Assert.Equal(["--Topogenia:Mode=Server"], parsed.Others);
    }

    [Fact]
    public async Task Two_commands_at_once_are_a_usage_error()
    {
        var folder = NewFolder();
        try
        {
            var (code, output) = await RunAsync(Desktop(folder), HostCommands.Migrate, HostCommands.Check);
            Assert.Equal(HostCommands.Usage, code);
            Assert.Contains("Une seule commande à la fois", output, StringComparison.Ordinal);
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task The_desktop_mode_has_no_migrate_command_its_data_file_is_migrated_at_the_start()
    {
        var folder = NewFolder();
        try
        {
            var (code, output) = await RunAsync(Desktop(folder), HostCommands.Migrate);
            Assert.Equal(HostCommands.Problem, code);
            Assert.Contains("migré au démarrage de l'application", output, StringComparison.Ordinal);
            Assert.False(File.Exists(Path.Combine(folder.FullName, "data", "topogenia.db")));
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task The_check_of_the_desktop_mode_accepts_a_data_file_still_to_create_without_creating_it()
    {
        var folder = NewFolder();
        try
        {
            var (code, output) = await RunAsync(Desktop(folder), HostCommands.Check);
            Assert.Equal((HostCommands.Success, true), (code, output.Contains("il sera créé au premier démarrage", StringComparison.Ordinal)));
            Assert.False(File.Exists(Path.Combine(folder.FullName, "data", "topogenia.db")));
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task The_check_of_the_desktop_mode_refuses_a_data_file_held_by_the_running_application_and_passes_once_it_is_stopped()
    {
        await using var run = await DesktopRun.StartAsync();
        var configuration = Desktop(run.Folder);

        var (held, heldOutput) = await RunAsync(configuration, HostCommands.Check);
        // An idle application releases its connections, not its named lock (ADR 050): the check still refuses.
        SqliteConnection.ClearAllPools();
        var (idle, _) = await RunAsync(configuration, HostCommands.Check);
        await run.StopAsync();
        var (stopped, stoppedOutput) = await RunAsync(configuration, HostCommands.Check);

        Assert.Equal((HostCommands.Problem, HostCommands.Problem), (held, idle));
        Assert.Contains("fermer l'application, puis relancer --check", heldOutput, StringComparison.Ordinal);
        Assert.Equal(HostCommands.Success, stopped);
        Assert.Contains("Vérification réussie : la configuration et la base de données", stoppedOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_second_start_on_the_same_data_file_opens_the_first_one_and_ends_with_a_zero_code()
    {
        await using var run = await DesktopRun.StartAsync();
        var first = run.LaunchToken;
        using var output = new StringWriter();
        var code = await Program.RunAsync([], TextWriter.Null, builder =>
        {
            builder.Configuration.AddInMemoryCollection(Desktop(run.Folder));
            builder.Services.AddSingleton(provider => new StartupConsole(provider.GetRequiredService<Topogenia.Domain.Values.LanguageSettings>(), output, TextReader.Null, interactive: false));
        }, Token);
        Assert.Equal(Program.Success, code);
        Assert.Equal("L'application est déjà lancée : elle s'ouvre dans le navigateur.", output.ToString().Trim());
        Assert.NotNull(run.LaunchToken);
        Assert.NotEqual(first, run.LaunchToken);
    }

    /// <summary>A settings file with a wrong property stops the start with its sentence and the property, never the bare code.</summary>
    [Fact]
    public async Task A_settings_file_with_a_wrong_property_stops_the_start_with_its_sentence_at_the_console()
    {
        var folder = Directory.CreateTempSubdirectory($"{TestDatabase.FolderPrefix}{Environment.ProcessId}-");
        try
        {
            await File.WriteAllTextAsync(Path.Combine(folder.FullName, DesktopPaths.SettingsFileName), """{ "Port": 70000 }""", Token);
            using var output = new StringWriter();
            var code = await Program.RunAsync([], output, builder => builder.Configuration.AddInMemoryCollection(Desktop(folder)), Token);
            Assert.Equal(Program.Problem, code);
            Assert.Equal("Le fichier de réglages settings.json n'est pas valide (Port) : le corriger ou le supprimer.", output.ToString().Trim());
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task A_second_start_on_a_data_file_whose_lock_is_held_by_a_process_that_does_not_answer_is_refused()
    {
        await using var run = await DesktopRun.StartAsync();
        var paths = run.Services.GetRequiredService<DesktopPaths>();
        await run.StopAsync();
        using var held = new DesktopInstance(paths);
        Assert.True(held.TryAcquire());
        await using var second = HostApplication.Build([], builder => builder.Configuration.AddInMemoryCollection(Desktop(run.Folder)));
        var refusal = await Assert.ThrowsAsync<StartupRefusedException>(() => second.StartAsync(Token));
        Assert.Equal(StartupCodes.AlreadyRunning, refusal.Code);
    }

    [Fact]
    public async Task The_check_of_the_desktop_mode_reports_a_data_file_behind_without_migrating_it()
    {
        await using var database = await TestDatabase.MigratedAsync(EngineKind.Sqlite);
        await using (var context = database.Engine.CreateMigrationContext())
            await context.GetService<IMigrator>().MigrateAsync(context.Database.GetMigrations().ToList()[^2], Token);
        SqliteConnection.ClearAllPools();
        var folder = NewFolder();
        try
        {
            var configuration = Desktop(folder);
            configuration[$"{Section}:Desktop:DataFile"] = database.Name;
            var (code, output) = await RunAsync(configuration, HostCommands.Check);
            Assert.Equal(HostCommands.Success, code);
            Assert.Contains("sera migré au prochain démarrage, après sa sauvegarde (1 mise(s) à jour)", output, StringComparison.Ordinal);
            var state = await new SchemaMigrations(database.Engine, new FixedClock()).CheckAsync(Token);
            Assert.Equal((SchemaStatus.Behind, 1), (state.Status, state.Pending.Count));
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task The_check_of_the_desktop_mode_refuses_a_damaged_or_read_only_data_file_with_its_message_and_never_a_stack()
    {
        var folder = NewFolder();
        var dataFile = Path.Combine(folder.FullName, "data", "topogenia.db");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(dataFile)!);
            await File.WriteAllBytesAsync(dataFile, [.. Enumerable.Range(0, 8192).Select(index => (byte)(index * 7))], Token);
            var (damaged, damagedOutput) = await RunAsync(Desktop(folder), HostCommands.Check);
            Assert.Equal(HostCommands.Problem, damaged);
            Assert.Contains("ne peut pas être ouvert", damagedOutput, StringComparison.Ordinal);

            // The command ends with its process, which closes its connections; here, the pool of the test process keeps them.
            SqliteConnection.ClearAllPools();
            File.Delete(dataFile);
            Assert.Equal(HostCommands.Success, (await RunAsync(Desktop(folder), HostCommands.Check)).Code);
            var created = HostApplication.Build([], builder => builder.Configuration.AddInMemoryCollection(Desktop(folder)));
            await using (created)
            {
                await created.StartAsync(Token);
                await created.StopAsync(Token);
            }
            File.SetAttributes(dataFile, FileAttributes.ReadOnly);
            var (readOnly, readOnlyOutput) = await RunAsync(Desktop(folder), HostCommands.Check);
            Assert.Equal(HostCommands.Problem, readOnly);
            foreach (var output in new[] { damagedOutput, readOnlyOutput })
            {
                Assert.DoesNotContain(folder.FullName, output, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("   at ", output, StringComparison.Ordinal);
            }
        }
        finally
        {
            if (File.Exists(dataFile))
                File.SetAttributes(dataFile, FileAttributes.Normal);
            SqliteConnection.ClearAllPools();
            folder.Delete(recursive: true);
        }
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public async Task The_check_of_the_desktop_mode_warns_of_a_folder_that_the_users_of_the_machine_may_read()
    {
        var folder = NewFolder();
        var data = Directory.CreateDirectory(Path.Combine(folder.FullName, "data"));
        var users = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
        try
        {
            var rules = data.GetAccessControl();
            rules.AddAccessRule(new FileSystemAccessRule(users, FileSystemRights.ReadData, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None, AccessControlType.Allow));
            data.SetAccessControl(rules);
            var (code, output) = await RunAsync(Desktop(folder), HostCommands.Check);
            Assert.Equal(HostCommands.Success, code);
            Assert.Contains("Attention : le dossier du fichier de données", output, StringComparison.Ordinal);
            Assert.DoesNotContain("le dossier des sauvegardes", output, StringComparison.Ordinal);
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task The_check_of_the_desktop_mode_refuses_a_data_file_in_a_synchronised_folder()
    {
        var folder = NewFolder();
        try
        {
            var configuration = Desktop(folder);
            configuration[$"{Section}:Desktop:DataFile"] = Path.Combine(folder.FullName, "OneDrive - Fictive", "topogenia.db");
            var (code, output) = await RunAsync(configuration, HostCommands.Check);
            Assert.Equal(HostCommands.Problem, code);
            Assert.Contains("Le fichier de données est dans un dossier synchronisé", output, StringComparison.Ordinal);
            Assert.DoesNotContain("   at ", output, StringComparison.Ordinal);
            Assert.False(Directory.Exists(Path.Combine(folder.FullName, "OneDrive - Fictive")));
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task Migrate_creates_and_migrates_a_missing_server_database_then_has_nothing_to_do_and_the_check_passes()
    {
        await using var database = TestDatabase.Create(EngineKind.SqlServer);
        var folder = NewFolder();
        try
        {
            var configuration = Server(database.ConnectionString, folder);

            var (first, firstOutput) = await RunAsync(configuration, HostCommands.Migrate);
            var applied = (await new SchemaMigrations(database.Engine, new FixedClock()).CheckAsync(Token)).Applied.Count;
            var (second, secondOutput) = await RunAsync(configuration, HostCommands.Migrate);
            var (check, checkOutput) = await RunAsync(configuration, HostCommands.Check);

            Assert.Equal(HostCommands.Success, first);
            Assert.Contains($"Base de données migrée : {applied} mise(s) à jour", firstOutput, StringComparison.Ordinal);
            Assert.Equal(HostCommands.Success, second);
            Assert.Contains("déjà à jour", secondOutput, StringComparison.Ordinal);
            Assert.Equal(HostCommands.Success, check);
            Assert.Contains("Vérification réussie", checkOutput, StringComparison.Ordinal);
            Assert.DoesNotContain(database.Name, firstOutput + secondOutput + checkOutput, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task The_check_of_the_server_mode_refuses_a_database_behind_and_a_configuration_without_its_connection_string()
    {
        await using var database = await TestDatabase.CreatedAsync(EngineKind.SqlServer);
        var folder = NewFolder();
        try
        {
            var (behind, behindOutput) = await RunAsync(Server(database.ConnectionString, folder), HostCommands.Check);
            var blank = Server(" ", folder);
            var (missing, missingOutput) = await RunAsync(blank, HostCommands.Check);
            var (migrateMissing, migrateMissingOutput) = await RunAsync(blank, HostCommands.Migrate);

            Assert.Equal(HostCommands.Problem, behind);
            Assert.Contains("--migrate", behindOutput, StringComparison.Ordinal);
            Assert.Equal((HostCommands.Problem, HostCommands.Problem), (missing, migrateMissing));
            foreach (var output in new[] { missingOutput, migrateMissingOutput })
            {
                // The refusal of the configuration itself (CHAINE_CONNEXION_ABSENTE), never a failure of the command.
                Assert.Equal("Le mode Serveur exige la chaîne de connexion à SQL Server (réglage Database:ConnectionString) : la renseigner dans la configuration.",
                    output.TrimEnd());
                Assert.DoesNotContain(folder.FullName, output, StringComparison.OrdinalIgnoreCase);
            }
            var state = await new SchemaMigrations(database.Engine, new FixedClock()).CheckAsync(Token);
            Assert.Equal((SchemaStatus.Behind, 0), (state.Status, state.Applied.Count));
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task Migrate_needs_no_list_of_the_names_of_the_server_but_the_check_does()
    {
        await using var database = await TestDatabase.MigratedAsync(EngineKind.SqlServer);
        var folder = NewFolder();
        try
        {
            var configuration = Server(database.ConnectionString, folder);
            configuration[$"{Section}:Server:AllowedHosts:0"] = "*";
            var (migrate, _) = await RunAsync(configuration, HostCommands.Migrate);
            var (check, checkOutput) = await RunAsync(configuration, HostCommands.Check);
            Assert.Equal((HostCommands.Success, HostCommands.Problem), (migrate, check));
            Assert.Contains("Server:AllowedHosts", checkOutput, StringComparison.Ordinal);
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }
}
