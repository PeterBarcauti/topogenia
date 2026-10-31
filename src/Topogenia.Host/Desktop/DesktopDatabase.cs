// DesktopDatabase.cs
// The start of the data file of the desktop mode: place of the file, backup, migration, daily backup, each refusal by its code (spec 4.4, 5.2).

using Topogenia.Application.Ports;
using Topogenia.Host.Startup;
using Topogenia.Persistence.Sqlite.Files;
using Topogenia.Persistence.Sqlite.Startup;

namespace Topogenia.Host.Desktop;

/// <summary>
/// Opens the data file before the first request:
/// <list type="number">
/// <item>a file on a network share, in a synchronised folder, reached by a path the check does not take or holding
/// several names is refused with the code of <see cref="DataFileRefusals"/> and the place at fault (spec 5.2);</item>
/// <item>the folders of the product are created (DROITS_INSUFFISANTS when one cannot be), then
/// <see cref="DesktopDatabaseStartup"/> checks the schema, backs the file up before a migration (none, and a stop, when
/// that backup fails: MIGRATION_SANS_SAUVEGARDE, AR-14), migrates and makes the backup of the day, whose failure is only
/// logged (A-4-32);</item>
/// <item>a schema that must not be used is refused with its code of <see cref="SchemaCodes"/>.</item>
/// <item>once open, the folders of the data file and of the backups readable by the broad groups of the machine are
/// given back and logged by their place, never their path: the start only warns of them (AR-98).</item>
/// </list>
/// The logs get codes, numbers of the engine and types only: never a path, nor the message of an exception (ADR 036).
/// </summary>
public sealed partial class DesktopDatabase(DesktopPaths paths, ISchemaMigrations migrations, IBackupService backups, DataFileEnvironment environment,
    ILogger<DesktopDatabase> logger)
{
    /// <summary>The places of the folders readable by the other accounts of the machine (AR-98).</summary>
    public const string DataPlace = "data";

    /// <inheritdoc cref="DataPlace"/>
    public const string BackupsPlace = "backups";

    /// <summary>Opens the data file; gives the places (<see cref="DataPlace"/>, <see cref="BackupsPlace"/>) of the folders readable by others.</summary>
    public async Task<IReadOnlyList<string>> OpenAsync(CancellationToken cancellationToken)
    {
        if (DesktopDataFile.Check(paths.DataFile, environment) is { } refusal)
            throw new StartupRefusedException(refusal.Code, new Dictionary<string, object?> { ["place"] = refusal.Place });
        try
        {
            CreateFolder(Path.GetDirectoryName(paths.DataFile)!, DataPlace);
            CreateFolder(paths.BackupsFolder, BackupsPlace);
            var report = await new DesktopDatabaseStartup(migrations, backups).RunAsync(cancellationToken);
            if (report.BeforeMigration is { Done: true })
                LogMigrated(logger, report.Schema.Applied.Count);
            if (!report.Daily.Done && !report.Daily.Skipped)
                LogDailyBackupFailed(logger, report.Daily.Failure ?? "", report.Daily.Cause?.GetType().FullName ?? "");
        }
        catch (SchemaRefusedException refused)
        {
            LogSchemaRefused(logger, refused.Code, refused.Migration ?? "", refused.InnerException is EngineFailureException engineFailure
                ? string.Join(",", engineFailure.Numbers) : refused.InnerException?.GetType().FullName ?? "");
            throw new StartupRefusedException(refused.Code, refused.Migration is null ? null : new Dictionary<string, object?> { ["migration"] = refused.Migration });
        }
        catch (MigrationBackupFailedException failed)
        {
            LogBackupBeforeMigrationFailed(logger, failed.Result?.Failure ?? "");
            throw new StartupRefusedException(StartupCodes.MigrationWithoutBackup, new Dictionary<string, object?> { ["failure"] = failed.Result?.Failure });
        }
        var exposed = ExposedPlaces(paths, environment);
        foreach (var place in exposed)
            LogReadableByOthers(logger, place);
        return exposed;
    }

    /// <summary>
    /// A folder of the product that cannot be created (access denied, a file in its place) refuses the start with
    /// DROITS_INSUFFISANTS, which no backup mends; the log gets its place and the type of the failure, never its path.
    /// </summary>
    private void CreateFolder(string folder, string place)
    {
        try
        {
            Directory.CreateDirectory(folder);
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            LogFolderRefused(logger, place, failure.GetType().FullName ?? failure.GetType().Name);
            throw new StartupRefusedException(SchemaCodes.InsufficientRights);
        }
    }

    /// <summary>The places of the folders of the data file and of the backups that the other accounts of the machine may read (AR-98).</summary>
    public static IReadOnlyList<string> ExposedPlaces(DesktopPaths paths, DataFileEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(environment);
        return [.. new[] { (Place: DataPlace, Folder: Path.GetDirectoryName(paths.DataFile)!), (Place: BackupsPlace, Folder: paths.BackupsFolder) }
            .Where(folder => environment.IsReadableByOthers(folder.Folder)).Select(folder => folder.Place)];
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "The folder of the {Place} cannot be created: {Type}; the start is refused.")]
    private static partial void LogFolderRefused(ILogger logger, string place, string type);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The folder of the {Place} is readable by the other accounts of the machine; the application starts.")]
    private static partial void LogReadableByOthers(ILogger logger, string place);

    [LoggerMessage(Level = LogLevel.Information, Message = "Data file migrated after its backup; {Count} migrations applied in all.")]
    private static partial void LogMigrated(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Daily backup failed: {Failure} ({Cause}); the application starts.")]
    private static partial void LogDailyBackupFailed(ILogger logger, string failure, string cause);

    [LoggerMessage(Level = LogLevel.Error, Message = "Data file refused by the schema check: {Code} {Migration} ({Engine}).")]
    private static partial void LogSchemaRefused(ILogger logger, string code, string migration, string engine);

    [LoggerMessage(Level = LogLevel.Error, Message = "Backup before the migration failed: {Failure}; the data file was not migrated.")]
    private static partial void LogBackupBeforeMigrationFailed(ILogger logger, string failure);
}
