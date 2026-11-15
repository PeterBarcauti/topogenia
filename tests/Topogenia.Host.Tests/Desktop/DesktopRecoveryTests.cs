// DesktopRecoveryTests.cs
// The restore offered at the console after a refusal that a backup mends, the settings file of the user, and the messages of the start (ADR 029, spec 6.1).

using Microsoft.Extensions.Logging.Abstractions;
using Topogenia.Application.Ports;
using Topogenia.Domain.Values;
using Topogenia.Host.Composition;
using Topogenia.Host.Desktop;
using Topogenia.Host.Errors;
using Topogenia.Host.Startup;
using Topogenia.Persistence.Sqlite.Backup;
using Topogenia.Persistence.Sqlite.Files;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Desktop;

public sealed class DesktopRecoveryTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static readonly LanguageSettings Languages =
        new(["fr", "en"], "fr", new Dictionary<string, string> { ["fr"] = "Acteur 1", ["en"] = "Actor 1" });

    private static (DesktopPaths Paths, DirectoryInfo Folder) Folder(params string[] backups)
    {
        var folder = Directory.CreateTempSubdirectory($"{TestDatabase.FolderPrefix}{Environment.ProcessId}-");
        var paths = new DesktopPaths(folder.FullName, Path.Combine(folder.FullName, "data", "topogenia.db"));
        Directory.CreateDirectory(paths.BackupsFolder);
        var time = DateTime.UtcNow.AddDays(-backups.Length);
        foreach (var name in backups)
        {
            var file = Path.Combine(paths.BackupsFolder, name);
            File.WriteAllText(file, "");
            File.SetLastWriteTimeUtc(file, time = time.AddDays(1));
        }
        return (paths, folder);
    }

    [Fact]
    public async Task The_person_at_the_console_chooses_a_backup_newest_first_and_it_is_restored()
    {
        var (paths, folder) = Folder("topogenia_monday.db", "topogenia_before_Initial.db", "other.db");
        try
        {
            var output = new StringWriter();
            var backups = new ScriptedBackups { Restore = new RestoreResult(true, null) { KeptFile = "topogenia.db.refused-20261024-201500" } };
            var recovery = new DesktopRecovery(paths, backups, new StartupConsole(Languages, output, new StringReader("2"), interactive: true), NullLogger<DesktopRecovery>.Instance);
            Assert.True(await recovery.OfferAsync(new StartupRefusedException(SchemaCodes.SchemaChanged), Token));
            Assert.Equal(Path.Combine(paths.BackupsFolder, "topogenia_monday.db"), Assert.Single(backups.Restored));
            var text = output.ToString();
            Assert.True(text.IndexOf("topogenia_before_Initial.db", StringComparison.Ordinal) < text.IndexOf("topogenia_monday.db", StringComparison.Ordinal));
            Assert.DoesNotContain("other.db", text, StringComparison.Ordinal);
            Assert.Contains("Sauvegarde restaurée", text, StringComparison.Ordinal);
            // AR-96: the offer warns of the changes after the backup, and the kept file is named once restored.
            Assert.Contains("après la sauvegarde choisie n'y sont pas", text, StringComparison.Ordinal);
            Assert.Contains("sous le nom topogenia.db.refused-20261024-201500", text, StringComparison.Ordinal);
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    [Theory]
    [InlineData(SchemaCodes.NewerDatabase, "1", true)]
    [InlineData(SchemaCodes.SchemaChanged, "", true)]
    [InlineData(SchemaCodes.SchemaChanged, "9", true)]
    [InlineData(SchemaCodes.SchemaChanged, "1", false)]
    public async Task Nothing_is_restored_for_a_refusal_no_backup_mends_without_a_choice_or_without_anybody_at_the_console(string code, string answer, bool interactive)
    {
        var (paths, folder) = Folder("topogenia_monday.db");
        try
        {
            var backups = new ScriptedBackups();
            var recovery = new DesktopRecovery(paths, backups, new StartupConsole(Languages, new StringWriter(), new StringReader(answer), interactive), NullLogger<DesktopRecovery>.Instance);
            Assert.False(await recovery.OfferAsync(new StartupRefusedException(code), Token));
            Assert.Empty(backups.Restored);
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task A_refused_restore_says_its_code_and_the_refusal_stands()
    {
        var (paths, folder) = Folder("topogenia_monday.db");
        try
        {
            var output = new StringWriter();
            // The form the service really gives: the code, then the detail (SqliteRestore).
            var backups = new ScriptedBackups { Restore = new RestoreResult(false, RestoreFailures.BackupCorrupted + ": integrity_check 2 problems") };
            var recovery = new DesktopRecovery(paths, backups, new StartupConsole(Languages, output, new StringReader("1"), interactive: true), NullLogger<DesktopRecovery>.Instance);
            Assert.False(await recovery.OfferAsync(new StartupRefusedException(SchemaCodes.DatabaseUnreachable), Token));
            Assert.Contains("La sauvegarde choisie est endommagée", output.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain("inattendue", output.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    [Fact]
    public void The_settings_file_gives_the_port_the_data_file_and_the_language()
    {
        var values = DesktopSettings.Read("""{ "Port": 5123, "DataFile": " data\\mine.db ", "Language": "en" }""");
        var section = ProductOptions.Section;
        Assert.Equal("5123", values[$"{section}:Desktop:Port"]);
        Assert.Equal(@"data\mine.db", values[$"{section}:Desktop:DataFile"]);
        Assert.Equal("en", values[$"{section}:Languages:Default"]);
    }

    [Theory]
    [InlineData("""{ "Prot": 5123 }""", "Prot")]
    [InlineData("""{ "Port": 70000 }""", "Port")]
    [InlineData("""{ "Port": "5123" }""", "Port")]
    [InlineData("""{ "DataFile": "" }""", "DataFile")]
    [InlineData("[1]", null)]
    public void A_settings_file_with_a_wrong_property_stops_the_start_with_the_property(string json, string? property)
    {
        var refusal = Assert.Throws<DesktopSettingsException>(() => DesktopSettings.Read(json));
        Assert.Equal(property, refusal.Property);
    }

    [Fact]
    public void A_settings_file_that_is_no_json_stops_the_start() =>
        Assert.Throws<DesktopSettingsException>(() => DesktopSettings.Read("{ Port: "));

    public static TheoryData<string> Codes() =>
    [
        SchemaCodes.NewerDatabase, SchemaCodes.MigrationChanged, SchemaCodes.SchemaChanged, SchemaCodes.FingerprintMissing, SchemaCodes.DatabaseBehind,
        SchemaCodes.InsufficientRights, SchemaCodes.DatabaseUnreachable, SchemaCodes.DatabaseBusy, SchemaCodes.MigrationLockHeld,
        DataFileRefusals.NetworkShare, DataFileRefusals.SynchronisedFolder, DataFileRefusals.UnsupportedPath, DataFileRefusals.MultipleNames,
        RestoreFailures.BackupMissing, RestoreFailures.BackupCorrupted, RestoreFailures.BackupRefused, RestoreFailures.DataFileOpen,
        RestoreFailures.UnsupportedPath, RestoreFailures.Impossible, "RESTAURATION_PROPOSEE", "RESTAURATION_FICHIER_GARDE",
        "DOSSIER_DONNEES_EXPOSE", "DOSSIER_SAUVEGARDES_EXPOSE", StartupCodes.InvalidMigrationTimeout, StartupCodes.AlreadyRunning, "COMMANDES_MULTIPLES",
        "MIGRATE_MODE_POSTE", "MIGRATION_FAITE", "MIGRATION_INUTILE", "VERIFICATION_REUSSIE", "VERIFICATION_FICHIER_A_CREER",
        "VERIFICATION_FICHIER_A_MIGRER", "VERIFICATION_FICHIER_OUVERT", "USAGE_EXPORT", "USAGE_IMPORT", "COMMANDE_FICHIER_ABSENT", "COMMANDE_FICHIER_OUVERT",
        "COMMANDE_FICHIER_A_MIGRER", "EXPORT_FICHIER_EXISTANT", "IMPORT_FICHIER_INTROUVABLE", "EXPORT_FAIT", "IMPORT_FAIT",
        StartupCodes.ServerModeWithSqlite, StartupCodes.MigrationWithoutBackup, DesktopSettings.InvalidSettings,
        StartupCodes.InvalidAllowedHosts, StartupCodes.MissingConnectionString, "ERREUR_INTERNE",
        "SERVEUR_BASE_PLUS_RECENTE", "SERVEUR_MIGRATION_MODIFIEE", "SERVEUR_SCHEMA_MODIFIE", "SERVEUR_EMPREINTE_ABSENTE",
        "SERVEUR_DROITS_INSUFFISANTS", "SERVEUR_BASE_INACCESSIBLE", global::Topogenia.Host.Server.ServerDatabase.ConnectionRefusedMessage,
    ];

    [Theory]
    [MemberData(nameof(Codes))]
    public void Every_refusal_of_the_start_has_a_message_in_each_language_with_the_same_values(string code)
    {
        var messages = ErrorMessages.Embedded("Topogenia.Host.Startup.messages.");
        Assert.Equal(["cs", "de", "el", "en", "es", "fr", "hu", "it", "ja", "nl", "pl", "pt", "ro", "ru", "sv", "tr", "uk", "zh"],
            messages.Languages.Order(StringComparer.Ordinal));
        foreach (var language in messages.Languages)
        {
            Assert.True(messages.Of(language).ContainsKey(code), $"{language} {code}");
            Assert.Equal(ErrorMessages.PlaceholdersOf(messages.Of("fr")[code]).Order(StringComparer.Ordinal),
                ErrorMessages.PlaceholdersOf(messages.Of(language)[code]).Order(StringComparer.Ordinal));
            Assert.DoesNotContain("Topogenia", messages.Of(language)[code], StringComparison.OrdinalIgnoreCase);
        }
    }
}
