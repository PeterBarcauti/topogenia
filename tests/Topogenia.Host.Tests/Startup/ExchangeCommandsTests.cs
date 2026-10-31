// ExchangeCommandsTests.cs
// The commands --export and --import of the executable (spec 6.3, ADR 055): their form, a round trip in the desktop mode, the rights in the server mode.

using System.IO.Compression;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Topogenia.Host.Composition;
using Topogenia.Host.Desktop;
using Topogenia.Host.Startup;
using Topogenia.Host.Tests.Desktop;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Startup;

public sealed class ExchangeCommandsTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static readonly string Section = ProductOptions.Section;

    private static string Sample => RepositoryPaths.Of("samples", "conges.topogenia");

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

    private static async Task<(int Code, string Output)> RunAsync(IReadOnlyDictionary<string, string?> configuration, params string[] args)
    {
        var output = new StringWriter();
        var code = await HostCommands.RunAsync(args, output, builder => builder.Configuration.AddInMemoryCollection(configuration), Token);
        SqliteConnection.ClearAllPools();
        return (code, output.ToString());
    }

    private static DirectoryInfo NewFolder() => Directory.CreateTempSubdirectory($"{TestDatabase.FolderPrefix}{Environment.ProcessId}-");

    /// <summary>The data file of the desktop mode created as by a first start of the application.</summary>
    private static async Task CreateDataFileAsync(IReadOnlyDictionary<string, string?> configuration)
    {
        await using var application = HostApplication.Build([], builder => builder.Configuration.AddInMemoryCollection(configuration));
        await application.StartAsync(Token);
        await application.StopAsync(Token);
        SqliteConnection.ClearAllPools();
    }

    /// <summary>The content entries of a package, the manifest left out (spec 7.4: its metadata are outside the comparison).</summary>
    private static Dictionary<string, string> Content(string package)
    {
        using var archive = ZipFile.OpenRead(package);
        return archive.Entries.Where(entry => entry.FullName != "manifest.json").ToDictionary(entry => entry.FullName, entry =>
        {
            using var reader = new StreamReader(entry.Open());
            return reader.ReadToEnd();
        });
    }

    [Fact]
    public void The_operands_and_options_of_an_exchange_command_never_go_to_the_configuration()
    {
        var export = HostCommands.Parse(["--EXPORT", "VTE", "ventes.topogenia", "--With-People", "--Topogenia:Mode=Desktop"])!.Value;
        var import = HostCommands.Parse(["--import", "ventes.topogenia", "--code", "VTEB", "--without-members"])!.Value;

        Assert.Equal([HostCommands.Export], export.Commands);
        Assert.Equal(["--Topogenia:Mode=Desktop"], export.Others);
        Assert.Equal([HostCommands.Import], import.Commands);
        Assert.Empty(import.Others);
    }

    /// <summary>The arguments of a command, separated by spaces.</summary>
    public static TheoryData<string> WrongForms() =>
    [
        "--export VTE ventes.topogenia",
        "--export VTE ventes.topogenia --with-people --without-people",
        "--export VTE --with-people",
        "--export VTE ventes.topogenia --without-people --with-members",
        "--export VTE ventes.topogenia --with-people --code VTEB",
        "--import ventes.topogenia",
        "--import ventes.topogenia --with-members --with-members",
        "--import ventes.topogenia --with-members --code VTEA --code VTEB",
        "--import --without-members",
    ];

    [Theory]
    [MemberData(nameof(WrongForms))]
    public async Task A_command_without_its_operands_or_with_a_question_unanswered_or_answered_twice_is_a_usage_error(string line)
    {
        var args = line.Split(' ');
        var folder = NewFolder();
        try
        {
            var (code, output) = await RunAsync(Desktop(folder), args);

            Assert.Equal(HostCommands.Usage, code);
            Assert.StartsWith("Forme de la commande : " + args[0], output, StringComparison.Ordinal);
            Assert.Empty(folder.EnumerateFiles("*", SearchOption.AllDirectories));
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task In_the_desktop_mode_a_package_imported_then_exported_gives_the_same_content_and_nothing_is_replaced()
    {
        var folder = NewFolder();
        try
        {
            var configuration = Desktop(folder);
            var (absent, absentOutput) = await RunAsync(configuration, HostCommands.Import, Sample, HostCommands.WithoutMembers);
            await CreateDataFileAsync(configuration);
            var exported = Path.Combine(folder.FullName, "copie.topogenia");

            var (imported, importedOutput) = await RunAsync(configuration, HostCommands.Import, Sample, HostCommands.WithoutMembers);
            var (taken, takenOutput) = await RunAsync(configuration, HostCommands.Import, Sample, HostCommands.WithoutMembers);
            var (other, otherOutput) = await RunAsync(configuration, HostCommands.Import, Sample, HostCommands.WithoutMembers, HostCommands.CodeOption, "CONGEB");
            var (export, exportOutput) = await RunAsync(configuration, HostCommands.Export, "CONGES", exported, HostCommands.WithoutPeople);
            var (again, againOutput) = await RunAsync(configuration, HostCommands.Export, "CONGEB", exported, HostCommands.WithoutPeople);
            var (unknown, unknownOutput) = await RunAsync(configuration, HostCommands.Export, "ABSENT", Path.Combine(folder.FullName, "x.topogenia"),
                HostCommands.WithoutPeople);
            var (malformed, malformedOutput) = await RunAsync(configuration, HostCommands.Export, "conges-1", Path.Combine(folder.FullName, "y.topogenia"),
                HostCommands.WithoutPeople);

            Assert.Equal(HostCommands.Problem, absent);
            Assert.StartsWith("Le fichier de données n'existe pas encore", absentOutput, StringComparison.Ordinal);
            Assert.Equal((HostCommands.Success, "Paquet importé : projet CONGES créé."), (imported, importedOutput.TrimEnd()));
            Assert.Equal((HostCommands.Problem, "Le code projet CONGES est déjà utilisé."), (taken, takenOutput.TrimEnd()));
            Assert.Equal((HostCommands.Success, "Paquet importé : projet CONGEB créé."), (other, otherOutput.TrimEnd()));
            Assert.Equal(HostCommands.Success, export);
            Assert.Contains("Projet CONGES exporté dans", exportOutput, StringComparison.Ordinal);
            Assert.Equal(Content(Sample), Content(exported));
            Assert.Equal(HostCommands.Problem, again);
            Assert.Contains("existe déjà", againOutput, StringComparison.Ordinal);
            Assert.Equal((HostCommands.Problem, "Élément introuvable."), (unknown, unknownOutput.TrimEnd()));
            Assert.Equal((HostCommands.Problem, "Le code projet doit compter 3 à 6 lettres majuscules."), (malformed, malformedOutput.TrimEnd()));
            Assert.Empty(folder.EnumerateFiles("*.part", SearchOption.AllDirectories));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            folder.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task In_the_desktop_mode_a_command_waits_for_the_running_application_to_stop()
    {
        await using var run = await DesktopRun.StartAsync();

        var (code, output) = await RunAsync(Desktop(run.Folder), HostCommands.Import, Sample, HostCommands.WithoutMembers);

        Assert.Equal(HostCommands.Problem, code);
        Assert.Contains("fermer l'application, puis relancer la commande", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task In_the_server_mode_the_account_of_the_command_needs_the_rights_of_the_application()
    {
        await using var database = await TestDatabase.MigratedAsync(EngineKind.SqlServer);
        var folder = NewFolder();
        try
        {
            var configuration = Server(database.ConnectionString, folder);
            var (refused, refusedOutput) = await RunAsync(configuration, HostCommands.Import, Sample, HostCommands.WithMembers);
            configuration[$"{Section}:Administrators:0"] = new ProcessAccount().Login;
            var exported = Path.Combine(folder.FullName, "copie.topogenia");

            var (imported, importedOutput) = await RunAsync(configuration, HostCommands.Import, Sample, HostCommands.WithoutMembers);
            var (export, exportOutput) = await RunAsync(configuration, HostCommands.Export, "CONGES", exported, HostCommands.WithoutPeople);

            Assert.Equal((HostCommands.Problem, "Droits insuffisants pour cette action."), (refused, refusedOutput.TrimEnd()));
            Assert.True((imported, export) == (HostCommands.Success, HostCommands.Success), importedOutput + exportOutput);
            Assert.Equal(Content(Sample), Content(exported));
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }
}
