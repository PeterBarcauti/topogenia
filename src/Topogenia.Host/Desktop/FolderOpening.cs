// FolderOpening.cs
// The port of « Open the folder » of a document in desktop mode: the rules of the path, on its text alone, then one click at a time on a thread of its own, the look and the shell each bounded in time (ADR 104).

using System.Globalization;
using System.Text;

namespace Topogenia.Host.Desktop;

/// <summary>What became of a path asked to be shown as a folder (ADR 104).</summary>
public enum FolderOutcome
{
    /// <summary>The shell of Windows was asked to show the folder.</summary>
    Shown,

    /// <summary>The path, or the folder to show written with its final separator, is longer than <see cref="FolderPaths.MaxLength"/>.</summary>
    TooLong,

    /// <summary>The path is not written as the folder Windows would open: nothing was looked at.</summary>
    Unsupported,

    /// <summary>Neither the path nor its parent is a folder.</summary>
    NotFound,

    /// <summary>Windows refused the access to the folder.</summary>
    Denied,

    /// <summary>The server or the drive did not answer, in time or at all.</summary>
    Unreachable,

    /// <summary>Another click still waits for the answer of Windows: nothing was looked at.</summary>
    Busy,
}

/// <summary>What a look at a path found on the disk or on the network.</summary>
public enum FolderLook
{
    /// <summary>A folder.</summary>
    Folder,

    /// <summary>Something that is not a folder: a file.</summary>
    NotFolder,

    /// <summary>Nothing of that name.</summary>
    Missing,

    /// <summary>Windows refused to tell.</summary>
    Denied,

    /// <summary>The server or the drive did not answer.</summary>
    Unreachable,
}

/// <summary>
/// Shows in the Explorer of Windows the folder that a path names, or the folder that holds what it names; it never opens
/// what the path names. The only way from the location of a document to the disk and to the shell (ADR 104): the route
/// « Open the folder » of the desktop mode is its only caller, which an architecture rule holds
/// (`SourceRulesTests`). The server mode registers none.
/// </summary>
public interface IFolderOpener
{
    Task<FolderOutcome> ShowAsync(string path, CancellationToken cancellationToken);
}

/// <summary>The disk and the shares of the network, looked at: whether a path is a folder. A look may wait as long as Windows waits for a server.</summary>
public interface IFolderDisk
{
    FolderLook Look(string path);
}

/// <summary>The shell of Windows asked to show a folder, given with its final separator; never a file. It may wait as long as Windows does.</summary>
public interface IFolderShell
{
    FolderOutcome Show(string folder);
}

/// <summary>How long a look may take before the server or the drive is said not to answer; the shell, once asked, is given as long.</summary>
public sealed record FolderOpeningOptions(TimeSpan LookTimeout)
{
    /// <summary>Three seconds: a file server that does not answer must not freeze the sheet.</summary>
    public static FolderOpeningOptions Default { get; } = new(TimeSpan.FromSeconds(3));
}

/// <summary>
/// A path read by <see cref="FolderPaths.Read"/>: its refusal, or what it names (without a final separator, a drive
/// kept whole) and the folder that holds it (null for a drive or a share alone).
/// </summary>
public sealed record FolderPath(FolderOutcome? Refusal, string Item, string? Parent);

/// <summary>
/// The rules of a path, on its text alone: nothing here reaches the disk nor the network, and no function of the
/// framework that may do so is called (a full path asked to the framework reads the disk as soon as the path holds a
/// tilde). The separators are written as Windows writes them (a slash, a run of them); nothing else is changed. A path
/// that Windows would read as another object than the folder written is refused, never mended: the folder shown is,
/// character for character, the path entered or its parent.
/// </summary>
public static class FolderPaths
{
    /// <summary>The longest path Windows opens everywhere (MAX_PATH less its final zero).</summary>
    public const int MaxLength = 259;

    private const char Separator = '\\';

    /// <summary>The digits that end the name of a serial or parallel port: Windows reads the three superscript digits as digits too.</summary>
    private const string PortDigits = "0123456789\u00B9\u00B2\u00B3";

    /// <summary>The shares of a server that are no folders: its named pipes, its mail slots, the share of its processes, and the root of a WebDAV server, which is an address of the web.</summary>
    private static readonly string[] DeviceShares = ["pipe", "mailslot", "ipc$", "DavWWWRoot"];

    /// <summary>The servers that are no file servers: the files of a Linux system of this computer, which a look would start.</summary>
    private static readonly string[] DeviceServers = ["wsl.localhost"];

    /// <summary>
    /// The characters Unicode says are not to be drawn (`Default_Ignorable_Code_Point`, `DerivedCoreProperties.txt`), as
    /// ranges of code points: the fillers of the Korean script, the selectors of a variant, the marks and the vowels
    /// never drawn that no category of the framework tells, and the code points kept for such characters.
    /// </summary>
    private static readonly (int First, int Last)[] Undrawn =
    [
        (0x00AD, 0x00AD), (0x034F, 0x034F), (0x061C, 0x061C), (0x115F, 0x1160), (0x17B4, 0x17B5), (0x180B, 0x180F), (0x200B, 0x200F),
        (0x202A, 0x202E), (0x2060, 0x206F), (0x3164, 0x3164), (0xFE00, 0xFE0F), (0xFEFF, 0xFEFF), (0xFFA0, 0xFFA0), (0xFFF0, 0xFFF8),
        (0x1BCA0, 0x1BCA3), (0x1D173, 0x1D17A), (0xE0000, 0xE0FFF),
    ];

    /// <summary>The devices of Windows that a name reaches in any folder, whatever its extension; the ports come besides (<see cref="PortDigits"/>).</summary>
    private static readonly string[] Devices = ["CON", "PRN", "AUX", "NUL", "CONIN$", "CONOUT$"];

    /// <summary>The two kinds of ports named by three letters and a digit.</summary>
    private static readonly string[] Ports = ["COM", "LPT"];

    /// <summary>The errors of Windows that say the folder is not there (`winerror.h`): no file, no path, no drive, no share, a name that is none, no folder, nothing to show it.</summary>
    private static readonly int[] MissingErrors = [2, 3, 15, 67, 87, 123, 161, 206, 267, 1155];

    /// <summary>The errors of Windows that say the account may not look: access, network access, logon, account restrictions.</summary>
    private static readonly int[] DeniedErrors = [5, 65, 86, 1326, 1327, 1328, 1329, 1330, 1331];

    public static FolderPath Read(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (path.Length > MaxLength)
            return new FolderPath(FolderOutcome.TooLong, path, null);
        if (Hidden(path) || Parts(path) is not var (root, segments) || segments.Any(Reshaped))
            return new FolderPath(FolderOutcome.Unsupported, path, null);
        return new FolderPath(null, Join(root, segments), segments.Count == 0 ? null : Join(root, segments[..^1]));
    }

    /// <summary>
    /// Whether a folder, written as it is given to the shell, is a root: a drive (`X:\`) or a share (`\\server\share\`)
    /// alone. A root is a folder whatever is put on the disk, and the only folder a disc gives verbs of its own.
    /// </summary>
    public static bool Root(string folder) => Read(folder) is { Refusal: null, Parent: null };

    /// <summary>A folder written so that it cannot name a file: with its final separator.</summary>
    public static string AsFolder(string folder)
    {
        ArgumentNullException.ThrowIfNull(folder);
        return folder.EndsWith(Separator) ? folder : folder + Separator;
    }

    /// <summary>What an error of Windows says of a path (the code of the error, as <c>GetLastError</c> gives it): anything unknown is a server or a drive that does not answer.</summary>
    public static FolderLook LookOf(int error) =>
        MissingErrors.Contains(error) ? FolderLook.Missing : DeniedErrors.Contains(error) ? FolderLook.Denied : FolderLook.Unreachable;

    /// <summary>What the failure of a look says of a path, read on its result code; a failure that is not an error of Windows is a name that names nothing.</summary>
    public static FolderLook LookOf(Exception failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        const int windows = 7;
        return ((failure.HResult >> 16) & 0x1FFF) == windows ? LookOf(failure.HResult & 0xFFFF) : FolderLook.Missing;
    }

    /// <summary>What an error of Windows says of the folder the shell was asked to show (the code of the error, as <c>GetLastError</c> gives it).</summary>
    public static FolderOutcome OutcomeOf(int error) => LookOf(error) switch
    {
        FolderLook.Missing => FolderOutcome.NotFound,
        FolderLook.Denied => FolderOutcome.Denied,
        _ => FolderOutcome.Unreachable,
    };

    /// <summary>
    /// Whether a text holds what is not shown as it is written, read character by character and not by halves: a
    /// character beyond the basic plane is two halves that say nothing of it, and a half alone is no character.
    /// </summary>
    private static bool Hidden(string path)
    {
        for (var index = 0; index < path.Length; index++)
        {
            if (!Rune.TryGetRuneAt(path, index, out var character) || Hidden(character))
                return true;
            index += character.Utf16SequenceLength - 1;
        }
        return false;
    }

    /// <summary>
    /// A character that is not shown as it is written: a control character, a separator of lines or of paragraphs, a
    /// character Unicode says is not to be drawn (<see cref="Undrawn"/>), and the format characters of every plane,
    /// which hide or reorder a text (the override that writes a name backwards, a space of no width, a mark of byte
    /// order, a tag). The two joiners that the letters of several scripts need are kept.
    /// </summary>
    private static bool Hidden(Rune character) => character.Value is not (0x200C or 0x200D)
        && (Rune.IsControl(character) || Undrawn.Any(range => character.Value >= range.First && character.Value <= range.Last)
            || Rune.GetUnicodeCategory(character) is UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator or UnicodeCategory.Format);

    /// <summary>
    /// The root of a path (a drive `X:\`, or a server and its share `\\server\share`) and the names below it, or null: a
    /// path without a drive or without a share, a device (`\\.\`, `\\?\`), a server that is not written as the name of a
    /// machine, a share that is no folder.
    /// </summary>
    private static (string Root, List<string> Segments)? Parts(string path)
    {
        var text = path.Replace('/', Separator);
        var names = text.Split(Separator, StringSplitOptions.RemoveEmptyEntries).ToList();
        if (text.StartsWith(@"\\", StringComparison.Ordinal))
        {
            return names.Count < 2 || !Machine(names[0]) || Reshaped(names[1]) || DeviceShares.Contains(names[1], StringComparer.OrdinalIgnoreCase)
                ? null
                : ($@"\\{names[0]}\{names[1]}", names[2..]);
        }
        return text.Length > 2 && char.IsAsciiLetter(text[0]) && text[1] == ':' && text[2] == Separator ? (text[..3], names[1..]) : null;
    }

    /// <summary>
    /// The name of a machine: the letters and the digits of ASCII, a dot, a hyphen, an underscore, and nothing else. Any
    /// other character has a meaning for Windows or deceives the eye: a semicolon names one of its providers before the
    /// server (and a pipe hides behind the name of a share), an at sign makes the path a WebDAV address on a port of its
    /// choice, a letter of another script drawn as a Latin one names another server than the one read. A server that is
    /// no file server is none either (<see cref="DeviceServers"/>).
    /// </summary>
    private static bool Machine(string name) =>
        !Reshaped(name) && name.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or '_')
        && !DeviceServers.Contains(name, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// A name Windows would read as another: one that climbs or stays (`..`, `.`), one it would cut (a final dot or
    /// space), a stream of a file (a colon), a device (`NUL`, `COM1`, with or without an extension), a name that holds
    /// the mark of an object of Windows (`name.{identifier}`, wherever the mark stands), which the Explorer would show
    /// in the place of the content of the folder. A name that ends with any other space (a no-break space, the space of
    /// the scripts of Asia) is refused with them: Windows keeps it, and nobody sees it there. Inside a name such a space
    /// is drawn as a space, and kept.
    /// </summary>
    private static bool Reshaped(string name) =>
        name is "." or ".." || name[^1] == '.' || char.GetUnicodeCategory(name[^1]) == UnicodeCategory.SpaceSeparator
        || name.Contains(':', StringComparison.Ordinal) || Device(name) || name.Contains(".{", StringComparison.Ordinal);

    /// <summary>The name of a device of Windows: what stands before the first dot, its final spaces left out, whatever the case.</summary>
    private static bool Device(string name)
    {
        var dot = name.IndexOf('.', StringComparison.Ordinal);
        var stem = (dot < 0 ? name : name[..dot]).TrimEnd(' ');
        return Devices.Contains(stem, StringComparer.OrdinalIgnoreCase)
            || (stem.Length == 4 && Ports.Contains(stem[..3], StringComparer.OrdinalIgnoreCase) && PortDigits.Contains(stem[3], StringComparison.Ordinal));
    }

    private static string Join(string root, IReadOnlyList<string> segments) =>
        segments.Count == 0 ? root : (root.EndsWith(Separator) ? root : root + Separator) + string.Join(Separator, segments);
}

/// <summary>
/// The opener of the desktop mode. In this order: the rules of the path (<see cref="FolderPaths"/>), nothing looked at
/// when they refuse it; one click at a time, on a thread of its own that looks then asks the shell, a click that comes
/// while another waits refused at once (<see cref="FolderOutcome.Busy"/>) instead of piling threads up; the look bounded
/// (<see cref="FolderOpeningOptions"/>), its answer dropped once the request has given it up, so that no Explorer opens
/// long after the refusal; then the folder just looked at, the path itself or else its parent and never a folder
/// further up, given to the shell with its final separator, the shell bounded as the look is. A file is never given to
/// the shell. A look or a shell that fails otherwise than by its answer is a folder that does not answer; a lack of
/// memory alone is left to the host, as everywhere in the program.
/// </summary>
public sealed class CheckedFolderOpener(IFolderDisk disk, IFolderShell shell, FolderOpeningOptions options) : IFolderOpener
{
    private int looking;

    /// <summary>Whether a click still waits for the answer of Windows, to its look or to its shell.</summary>
    public bool Looking => Volatile.Read(ref looking) != 0;

    public async Task<FolderOutcome> ShowAsync(string path, CancellationToken cancellationToken)
    {
        var read = FolderPaths.Read(path);
        if (read.Refusal is { } refusal)
            return refusal;
        if (Interlocked.CompareExchange(ref looking, 1, 0) != 0)
            return FolderOutcome.Busy;
        var click = new Click();
        Task<FolderOutcome> work;
        try
        {
            work = Task.Factory.StartNew(() => Work(read, click), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }
        catch
        {
            // No thread for the click: the opener must take the next one.
            Volatile.Write(ref looking, 0);
            throw;
        }
        try
        {
            return await work.WaitAsync(options.LookTimeout, cancellationToken);
        }
        catch (TimeoutException)
        {
            // The look still waits for the server: its answer, when it comes, shows nothing.
            if (click.GiveUp())
                return FolderOutcome.Unreachable;
        }
        catch (OperationCanceledException)
        {
            // The request left: a look that still waits shows nothing; a shell already asked is left to answer.
            click.GiveUp();
            throw;
        }
        // The look answered within its bound and the shell is asked: it is given the same bound to answer.
        try
        {
            return await work.WaitAsync(options.LookTimeout, cancellationToken);
        }
        catch (TimeoutException)
        {
            return FolderOutcome.Unreachable;
        }
    }

    /// <summary>
    /// One click, on its thread: the look, then the shell unless the request has given the look up; the opener takes a
    /// click again once this one ends, the shell answered.
    /// </summary>
    private FolderOutcome Work(FolderPath path, Click click)
    {
        try
        {
            var (outcome, folder) = Find(path);
            if (folder is null)
                return outcome;
            var given = FolderPaths.AsFolder(folder);
            if (given.Length > FolderPaths.MaxLength)
                return FolderOutcome.TooLong;
            return click.Ask() ? shell.Show(given) : FolderOutcome.Unreachable;
        }
        catch (Exception failure) when (failure is not OutOfMemoryException)
        {
            // Neither the look nor the shell answered what they found: nothing is said of the folder but that it did not answer.
            return FolderOutcome.Unreachable;
        }
        finally
        {
            Volatile.Write(ref looking, 0);
        }
    }

    /// <summary>The folder to show, the path itself or else its parent, or why there is none.</summary>
    private (FolderOutcome Outcome, string? Folder) Find(FolderPath path)
    {
        var item = disk.Look(path.Item);
        if (item is FolderLook.NotFolder or FolderLook.Missing && path.Parent is { } parent)
            return Found(disk.Look(parent), parent);
        return Found(item, path.Item);
    }

    private static (FolderOutcome Outcome, string? Folder) Found(FolderLook look, string folder) => look switch
    {
        FolderLook.Folder => (FolderOutcome.Shown, folder),
        FolderLook.Denied => (FolderOutcome.Denied, null),
        FolderLook.Unreachable => (FolderOutcome.Unreachable, null),
        _ => (FolderOutcome.NotFound, null),
    };

    /// <summary>
    /// What became of one click, decided once between its thread and its request: the look still waits, the request has
    /// given the look up (nothing will be shown), or the shell is asked (the request waits for its answer).
    /// </summary>
    private sealed class Click
    {
        private const int Waiting = 0;
        private const int GivenUp = 1;
        private const int Asked = 2;

        private int state;

        /// <summary>The request gives the look up; false when the shell is already asked.</summary>
        public bool GiveUp() => Interlocked.CompareExchange(ref state, GivenUp, Waiting) == Waiting;

        /// <summary>The thread of the click is about to ask the shell; false when the request has given the look up.</summary>
        public bool Ask() => Interlocked.CompareExchange(ref state, Asked, Waiting) == Waiting;
    }
}
