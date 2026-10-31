// DesktopRecovery.cs
// The restore of a backup offered before the data file is opened, when the start refused a file that a backup can replace (ADR 029).

using System.Globalization;
using Topogenia.Application.Ports;
using Topogenia.Domain.Errors;
using Topogenia.Host.Startup;

namespace Topogenia.Host.Desktop;

/// <summary>
/// After a refusal of the start that a backup can mend (the schema or a migration changed, an update applied without the
/// application, a file that does not open or whose log is refused, the lock left by an interrupted migration), the person
/// at the console is shown the backups of the data file, newest first, and may choose one: it is restored while nothing
/// has the file open (<see cref="IBackupService.RestoreAsync"/>), and the start runs again. The offer says that the changes
/// made after the backup are not in it and that the replaced file is kept with its journal, whose name is given once
/// restored (AR-96: a refused log may hold work, never erased). Nobody at the console, no
/// backup, or no choice: nothing is restored, the refusal stands. A refused restore says its code and leaves the file.
/// </summary>
public sealed partial class DesktopRecovery(DesktopPaths paths, IBackupService backups, StartupConsole console, ILogger<DesktopRecovery> logger)
{
    /// <summary>The refusals a backup can mend.</summary>
    public static IReadOnlyList<string> RestorableCodes { get; } =
    [
        SchemaCodes.SchemaChanged, SchemaCodes.MigrationChanged, SchemaCodes.FingerprintMissing, SchemaCodes.DatabaseUnreachable,
        SchemaCodes.MigrationLockHeld,
    ];

    /// <summary>True when a backup was restored and the start may run again.</summary>
    public async Task<bool> OfferAsync(StartupRefusedException refusal, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(refusal);
        if (!RestorableCodes.Contains(refusal.Code) || !console.Interactive)
            return false;
        var files = Backups();
        if (files.Count == 0)
            return false;
        console.Write("RESTAURATION_PROPOSEE");
        for (var index = 0; index < files.Count; index++)
        {
            console.Write("RESTAURATION_CHOIX", new Dictionary<string, object?>
            {
                ["number"] = index + 1,
                ["date"] = files[index].LastWriteTime.ToString("g", CultureInfo.CurrentCulture),
                ["file"] = files[index].Name,
            });
        }
        var answer = console.Ask("RESTAURATION_QUESTION");
        if (!int.TryParse(answer, NumberStyles.None, CultureInfo.InvariantCulture, out var choice) || choice < 1 || choice > files.Count)
            return false;
        var result = await backups.RestoreAsync(files[choice - 1].FullName, cancellationToken);
        if (result.Done)
        {
            console.Write("RESTAURATION_FAITE");
        }
        else
        {
            // The service gives "CODE: detail" (spec 5.2: codes and details without path): the console says the sentence
            // of the code, the log keeps the detail.
            var failure = result.Failure ?? ErrorCodes.InternalError;
            var code = failure.Split(':', 2)[0].Trim();
            LogRestoreFailed(logger, failure, result.Cause?.GetType().FullName ?? "");
            console.Write(code);
        }
        if (result.KeptFile is { } kept)
            console.Write("RESTAURATION_FICHIER_GARDE", new Dictionary<string, object?> { ["file"] = kept });
        return result.Done;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Restoration of a backup refused: {Failure} ({Cause}); the data file stays as it was.")]
    private static partial void LogRestoreFailed(ILogger logger, string failure, string cause);

    /// <summary>The backups of the data file (daily and before a migration), newest first.</summary>
    public IReadOnlyList<FileInfo> Backups()
    {
        var folder = new DirectoryInfo(paths.BackupsFolder);
        if (!folder.Exists)
            return [];
        var pattern = Path.GetFileNameWithoutExtension(paths.DataFile) + "_*" + Path.GetExtension(paths.DataFile);
        return [.. folder.EnumerateFiles(pattern).OrderByDescending(file => file.LastWriteTimeUtc)];
    }
}
