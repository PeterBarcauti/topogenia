// ShellFolderOpener.cs
// The only file that looks at a folder named by the location of a document and asks the shell of Windows to show it (ADR 104), by two classes that share nothing; it knows no document.

using System.Runtime.InteropServices;

namespace Topogenia.Host.Desktop;

/// <summary>
/// The look of Windows at a path, behind <see cref="IFolderDisk"/>: it is given a path already held to the rules of
/// <see cref="FolderPaths"/> by <see cref="CheckedFolderOpener"/>, and asks Windows for its attributes. A file is never
/// opened, read nor run. Before the attributes, the framework asks Windows for the long name of a path that holds a
/// tilde; when Windows refuses the attributes or the file is in use, it asks the folder that holds the path for this
/// one name: requests to the same server, on the thread of the click and within its bound. This class cannot ask the
/// shell: who holds the look holds nothing else.
/// </summary>
public sealed class WindowsFolderDisk : IFolderDisk
{
    public FolderLook Look(string path)
    {
        try
        {
            return File.GetAttributes(path).HasFlag(FileAttributes.Directory) ? FolderLook.Folder : FolderLook.NotFolder;
        }
        catch (OperationCanceledException)
        {
            // Windows gave the look up (a server that cancels the request): the folder did not answer.
            return FolderLook.Unreachable;
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return FolderPaths.LookOf(failure);
        }
    }
}

/// <summary>
/// The shell of Windows asked to show a folder, behind <see cref="IFolderShell"/>, as a click in the Explorer would: no
/// command line, no program named, no argument. The folder comes with its final separator. The verb
/// (<see cref="Verb"/>) is the one the classes of Windows declare for a folder alone, and where it is read depends on
/// the folder (<see cref="ClassOf"/>):
/// <list type="bullet">
/// <item>for a root, a drive or a share alone, in the class of the folders and no other (<see cref="Class"/>): a verb
/// that a disc declares for its root (`autorun.inf`) is never read, and a root is a folder whatever is put on the
/// disk;</item>
/// <item>for any other folder no class is named: the shell reads the verb in the classes of what the path names, where
/// no plain file has it, so a file put where the folder was after the look is refused instead of being opened. A file
/// that Windows itself shows as a folder (an archive, a library) may have the verb of the folders: the Explorer would
/// then show it as a folder, as it does when a person browses to it.</item>
/// </list>
/// What the shell then starts is the program that shows the folders, the Explorer unless the account put another in
/// its place: this is what the documentation of Windows says, and no test plays the call (ADR 104). A failure of
/// Windows is answered as what it says of the folder (<see cref="FolderPaths.OutcomeOf"/>), never thrown.
/// </summary>
public sealed class ShellFolderOpener : IFolderShell
{
    /// <summary>The verb of the shell that shows a folder in the Explorer.</summary>
    public const string Verb = "explore";

    /// <summary>The class of the registry where the verb is read for a root, and nowhere else: the folders.</summary>
    public const string Class = "Folder";

    /// <summary>What is said of a shell that refuses without naming its error (`ERROR_GEN_FAILURE`): a folder that does not answer.</summary>
    private const int Unnamed = 31;

    /// <summary>Asks the shell and waits for its answer: the caller bounds the wait (<see cref="CheckedFolderOpener"/>).</summary>
    public FolderOutcome Show(string folder)
    {
        if (!OperatingSystem.IsWindows())
            return FolderOutcome.Unreachable;
        var error = OnShellThread(() => Ask(folder));
        return error == 0 ? FolderOutcome.Shown : FolderPaths.OutcomeOf(error);
    }

    /// <summary>
    /// The class of the registry where the verb is read for a folder, written as it is given to the shell: the class of
    /// the folders for a root, none for any other folder.
    /// </summary>
    public static string? ClassOf(string folder) => FolderPaths.Root(folder) ? Class : null;

    /// <summary>
    /// Asks a question on a thread of its own, in a single-threaded apartment as the shell wants, and waits for the
    /// error it answers. A question that fails answers an error without a name: an exception left on a thread of its
    /// own would end the program.
    /// </summary>
    public static int OnShellThread(Func<int> question)
    {
        var error = Unnamed;
        var thread = new Thread(() => error = Answer(question)) { IsBackground = true };
        if (OperatingSystem.IsWindows())
            thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        return error;
    }

    private static int Answer(Func<int> question)
    {
        try
        {
            return question();
        }
        catch (Exception)
        {
            // No exception leaves this thread, whatever it is: left here, it ends the program. A fault of the native code
            // of the shell is no exception, and nothing here can catch it.
            return Unnamed;
        }
    }

    /// <summary>The error of Windows, zero once the shell took the folder. No window of the shell is shown on a refusal.</summary>
    private static int Ask(string folder)
    {
        var named = ClassOf(folder);
        var request = new Native.ExecuteInfo
        {
            Size = Marshal.SizeOf<Native.ExecuteInfo>(),
            Mask = named is null ? Native.NoAsync | Native.NoWindowOnError : Native.ClassName | Native.NoAsync | Native.NoWindowOnError,
            Verb = Verb,
            File = folder,
            Show = Native.ShowNormal,
            Class = named,
        };
        if (Native.ShellExecuteExW(ref request))
            return 0;
        var error = Marshal.GetLastPInvokeError();
        return error == 0 ? Unnamed : error;
    }

    /// <summary>The one function of the shell this file calls (`shellapi.h`), and what it is given.</summary>
    private static class Native
    {
        /// <summary>`SEE_MASK_CLASSNAME`: the verb is read in the class named, not in the classes of what the path names.</summary>
        public const uint ClassName = 0x00000001;

        /// <summary>`SEE_MASK_NOASYNC`: the call ends before the function returns, since its thread ends at once.</summary>
        public const uint NoAsync = 0x00000100;

        /// <summary>`SEE_MASK_FLAG_NO_UI`: no message box of the shell on an error.</summary>
        public const uint NoWindowOnError = 0x00000400;

        /// <summary>`SW_SHOWNORMAL`.</summary>
        public const int ShowNormal = 1;

        /// <summary>`SHELLEXECUTEINFOW`: 112 bytes on a 64-bit Windows.</summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct ExecuteInfo
        {
            public int Size;
            public uint Mask;
            public IntPtr Window;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string? Verb;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string? File;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string? Parameters;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string? Directory;
            public int Show;
            public IntPtr Instance;
            public IntPtr IdList;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string? Class;
            public IntPtr ClassKey;
            public uint HotKey;
            public IntPtr Icon;
            public IntPtr Process;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ShellExecuteExW(ref ExecuteInfo info);
    }
}
