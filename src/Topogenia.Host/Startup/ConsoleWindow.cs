// ConsoleWindow.cs
// The console of the process on Windows in desktop mode: released when it was made for this process alone, opened for a question only, and the boxes of Windows that tell the person instead (F-69, ADR 090).

using System.Runtime.InteropServices;
using System.Text;

namespace Topogenia.Host.Startup;

/// <summary>
/// What the start tells a person who sees no console: the sentences of the start shown in a box of Windows, and a console
/// opened only to ask a question (the restore of a backup), closed once answered (<see cref="StartupConsole"/>).
/// </summary>
public interface IStartupWindow
{
    /// <summary>Shows sentences in a box of Windows, above the other windows, and waits until the person closes it.</summary>
    void Show(IReadOnlyList<string> lines, bool problem);

    /// <summary>Opens a console, writes the sentences, reads the answer of the person, closes the console; null when nothing is read.</summary>
    string? Ask(IReadOnlyList<string> lines);
}

/// <summary>
/// The desktop mode runs without a console (F-69, ADR 090). From Windows 11 24H2, the manifest of the executable asks for
/// none (`consoleAllocationPolicy` `detached`); the process then takes the console of the terminal that starts it, if
/// any, and runs without one when it starts from the shell, the Start menu or the Store. An older Windows makes a console
/// for the process alone, released at the very start (a short flash). A command of the command line, a console shared
/// with a terminal, a redirected output (a test, a service, the journeys of the interface) or a session without a desktop
/// keep a console: what is written there is read there.
/// </summary>
public sealed class ConsoleWindow : IStartupWindow
{
    private const int StandardOutput = -11;
    private const uint ParentProcess = unchecked((uint)-1);
    private const int CharacterDevice = 0x0002;
    private const uint Utf8 = 65001;
    private const uint BoxOk = 0x0;
    private const uint BoxError = 0x10;
    private const uint BoxInformation = 0x40;
    private const uint BoxForeground = 0x10000;
    private const uint BoxTopmost = 0x40000;

    /// <summary>The title of the boxes and of the console opened for a question.</summary>
    public static string Title => Application.ProductIdentity.Name;

    /// <summary>
    /// True when the process now runs without a console a person sees: none was given to it, or the one made for it alone
    /// has been released. Called first, before anything writes to the console.
    /// </summary>
    public static bool Detach(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (!OperatingSystem.IsWindows() || !Environment.UserInteractive)
            return false;
        var command = HostCommands.Parse(args) is not null;
        if (NativeMethods.GetConsoleWindow() == IntPtr.Zero && !NativeMethods.AttachConsole(ParentProcess) && command)
            // A command started without a terminal (a shortcut) still says its result.
            NativeMethods.AllocConsole();
        if (command)
            return false;
        var output = NativeMethods.GetStdHandle(StandardOutput);
        if (NativeMethods.GetConsoleWindow() == IntPtr.Zero)
            return output == IntPtr.Zero || output == new IntPtr(-1);
        // A console made for this process alone, its output not redirected: released.
        if (NativeMethods.GetConsoleProcessList(new uint[2], 2) != 1 || !IsConsole(output))
            return false;
        return NativeMethods.FreeConsole();
    }

    private static bool IsConsole(IntPtr handle) =>
        (NativeMethods.GetFileType(handle) & CharacterDevice) == CharacterDevice && NativeMethods.GetConsoleMode(handle, out _);

    public void Show(IReadOnlyList<string> lines, bool problem)
    {
        ArgumentNullException.ThrowIfNull(lines);
        if (lines.Count == 0)
            return;
        _ = NativeMethods.MessageBoxW(IntPtr.Zero, string.Join(Environment.NewLine + Environment.NewLine, lines), Title,
            BoxOk | (problem ? BoxError : BoxInformation) | BoxForeground | BoxTopmost);
    }

    public string? Ask(IReadOnlyList<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        if (!NativeMethods.AllocConsole())
            return null;
        try
        {
            NativeMethods.SetConsoleTitleW(Title);
            NativeMethods.SetConsoleOutputCP(Utf8);
            NativeMethods.SetConsoleCP(Utf8);
            using var output = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)) { AutoFlush = true };
            using var input = new StreamReader(Console.OpenStandardInput(), new UTF8Encoding(false));
            foreach (var line in lines)
                output.WriteLine(line);
            return input.ReadLine();
        }
        finally
        {
            NativeMethods.FreeConsole();
        }
    }

    private static class NativeMethods
    {
        [DllImport("kernel32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern IntPtr GetConsoleWindow();

        [DllImport("kernel32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern IntPtr GetStdHandle(int handle);

        [DllImport("kernel32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern uint GetConsoleProcessList([Out] uint[] processes, uint count);

        [DllImport("kernel32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern int GetFileType(IntPtr handle);

        [DllImport("kernel32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetConsoleMode(IntPtr handle, out uint mode);

        [DllImport("kernel32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool FreeConsole();

        [DllImport("kernel32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool AllocConsole();

        [DllImport("kernel32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool AttachConsole(uint processId);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetConsoleTitleW(string title);

        [DllImport("kernel32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetConsoleOutputCP(uint codePage);

        [DllImport("kernel32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetConsoleCP(uint codePage);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern int MessageBoxW(IntPtr owner, string text, string caption, uint type);
    }
}
