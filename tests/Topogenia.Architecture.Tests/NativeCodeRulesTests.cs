// NativeCodeRulesTests.cs
// The exact list of the native functions of the shipped code, file by file, and the refusal of every other way to native code or to COM, with the samples of their detectors (S-5.4-13, S-5.4-21, ADR 104).

using System.Text.RegularExpressions;

namespace Topogenia.Architecture.Tests;

public sealed partial class NativeCodeRulesTests
{
    private const string ConsoleFile = "src/Topogenia.Host/Startup/ConsoleWindow.cs";

    private const string PackageFile = "src/Topogenia.Host/Desktop/DesktopPackage.cs";

    private const string OwnersFile = "src/Topogenia.Host/Desktop/WindowsConnectionOwners.cs";

    private const string TrayFile = "src/Topogenia.Host/Desktop/DesktopTray.cs";

    private const string IdentityFile = "src/Topogenia.Persistence.Sqlite/Files/FileIdentity.cs";

    private const string SampleFile = "src/Topogenia.Host/Sample.cs";

    private static IEnumerable<string> In(string file, string library, params string[] functions) => functions.Select(function => $"{file}: {library}!{function}");

    /// <summary>
    /// Every native function of the shipped code, as « file: library!function » (<see cref="NativeCodeDetectors.NativeFunctions"/>),
    /// grouped by file. Two of them are given a path, and no file but theirs gives one to native code: the function
    /// that reads the image of the icon, given the executable of the process itself and no other, and the one of the
    /// shell. The identity of the data file is read on a handle already open, never on a path.
    /// </summary>
    private static readonly string[] NativeFunctions =
    [
        // The console of the desktop mode (F-69, ADR 090): whether the process has one of its own, its release, the one
        // opened for the question of the restore with its title and its code pages, the kind of its standard streams;
        // and the box of Windows that tells the person when there is no console.
        .. In(ConsoleFile, "kernel32.dll", "GetConsoleWindow", "GetStdHandle", "GetConsoleProcessList", "GetFileType", "GetConsoleMode", "FreeConsole", "AllocConsole",
            "AttachConsole", "SetConsoleTitleW", "SetConsoleOutputCP", "SetConsoleCP"),
        .. In(ConsoleFile, "user32.dll", "MessageBoxW"),
        // The package of the Microsoft Store the desktop mode runs from, by its family name (ADR 088).
        .. In(PackageFile, "kernel32.dll", "GetCurrentPackageFamilyName"),
        // The owner of a connection of the loopback (ADR 075): the table of the TCP connections of Windows, the process
        // of the client end, the token of that process and its account.
        .. In(OwnersFile, "iphlpapi.dll", "GetExtendedTcpTable"),
        .. In(OwnersFile, "kernel32.dll", "OpenProcess"),
        .. In(OwnersFile, "advapi32.dll", "OpenProcessToken", "GetTokenInformation"),
        // The icon near the clock of a desktop mode without a console (F-69, ADR 090): its hidden window and the
        // messages of Windows, its menu, its image, the notification area of the shell.
        .. In(TrayFile, "user32.dll", "RegisterClassExW", "UnregisterClassW", "CreateWindowExW", "DestroyWindow", "DefWindowProcW", "GetMessageW", "TranslateMessage",
            "DispatchMessageW", "PostMessageW", "PostQuitMessage", "RegisterWindowMessageW", "CreatePopupMenu", "AppendMenuW", "SetMenuDefaultItem",
            "TrackPopupMenuEx", "DestroyMenu", "GetCursorPos", "SetForegroundWindow", "LoadIconW", "DestroyIcon"),
        .. In(TrayFile, "kernel32.dll", "GetModuleHandleW"),
        .. In(TrayFile, "shell32.dll", "Shell_NotifyIconW"),
        // The same icon: its image read from a file, which is the executable of the process itself and no other.
        .. In(TrayFile, "shell32.dll", "ExtractIconExW"),
        // The data file of the desktop mode: the identity of a file on its volume, read on a handle already open.
        .. In(IdentityFile, "kernel32.dll", "GetFileInformationByHandle"),
        // « Open the folder » of a document (ADR 104): the shell asked to show a folder, the one function that is given
        // a path entered in a map, held to what it is given by `SourceRulesTests`.
        .. In(SourceRulesTests.FolderShell, "shell32.dll", "ShellExecuteExW"),
    ];

    /// <summary>A declaration of a native function as the text writes it, the word of the detector aside.</summary>
    [GeneratedRegex(@"\[(DllImport|LibraryImport)\b|\bextern\s+\w|\bextern\b\s*$", RegexOptions.Multiline)]
    private static partial Regex ImportInText();

    /// <summary>
    /// S-5.4-13, S-5.4-21: the shipped code calls Windows by the functions of the list and no other, each in its file. A
    /// second file that declares the function of the shell, or one of the functions of Windows that look at, open or
    /// start what a path names (<see cref="NativeCodeDetectors.TakesPath"/>), fails here: the files of the framework are
    /// held by `SourceRulesTests`, the native ones by this list. Of the whole list two functions take a path: the one of
    /// the shell, in the listed file of the folder, and the one that reads the icon of the executable itself. The
    /// detector is not blind: every file whose text declares an import is in the list (control).
    /// </summary>
    [Fact]
    public void The_native_functions_of_the_shipped_code_are_those_listed_file_by_file()
    {
        var declared = SemanticSources.Production.SelectMany(NativeCodeDetectors.NativeFunctions).Order(StringComparer.Ordinal).ToList();
        Assert.Equal(NativeFunctions.Order(StringComparer.Ordinal), declared);
        Assert.Equal([$"{TrayFile}: shell32.dll!ExtractIconExW", $"{SourceRulesTests.FolderShell}: shell32.dll!ShellExecuteExW"], declared.Where(NativeCodeDetectors.TakesPath));
        var written = RepositoryFiles.ProductionCSharp().Where(file => ImportInText().IsMatch(File.ReadAllText(file))).Select(RepositoryFiles.Relative);
        Assert.Equal(written.Order(StringComparer.Ordinal), declared.Select(function => function.Split(": ")[0]).Distinct(StringComparer.Ordinal));
    }

    /// <summary>The places of the detours without their line: a file edited elsewhere keeps the rule as it is.</summary>
    private static List<string> Detours(IEnumerable<SourceFile> files) =>
        [.. files.SelectMany(NativeCodeDetectors.NativeDetours).Select(place => Regex.Replace(place, @":\d+ ", " ")).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];

    /// <summary>
    /// The list above is the only way of the shipped code to native code: no object of COM found by its name or its
    /// identifier (`Shell.Application`), no class of COM declared, no library loaded nor function taken by its address,
    /// no call left to the run (`dynamic`), no pointer to a function, no unsafe code, no extension of SQLite (a native
    /// library that the driver of the base loads from a path, by its connection or by the functions below it)
    /// (<see cref="NativeCodeDetectors.NativeDetours"/>). One use exists and is named: the procedure of the hidden window
    /// of the icon near the clock, a delegate that Windows calls, never a function of Windows called through a delegate.
    /// This detector names the ways it knows; the ways it does not name are held by what is admitted, in
    /// `FrameworkRulesTests`: the types of the namespace of native code and the members of its memory, the namespaces of
    /// the shipped code, no helper of Visual Basic (an object of COM made by its name), no call by a name written in a
    /// text outside the files listed. A way through a library whose namespace is admitted (the extension of SQLite was
    /// one, found by the third review) is seen only once it is named: there this stays a list of names. For the driver
    /// of SQLite itself, whose functions are native functions called without a declaration, the list is one of what is
    /// admitted: the shipped code calls three of them, file by file, and no other member of that namespace; and the one
    /// that sets an option of a connection is called twice, as written below, with the option that keeps the journal at
    /// the close and no other: another number of the same function turns on what SQLite keeps off by default (the
    /// loading of an extension through the driver, a tokenizer given by its address in a SQL statement).
    /// </summary>
    [Fact]
    public void The_shipped_code_reaches_native_code_and_com_by_its_listed_functions_alone()
    {
        Assert.Equal([$"{TrayFile} UnmanagedFunctionPointerAttribute"], Detours(SemanticSources.Production));
        const string sqlite = "src/Topogenia.Persistence.Sqlite/";
        Assert.Equal([$"{sqlite}Backup/SqliteRestore.cs raw.sqlite3_db_config", $"{sqlite}Connection/SqliteDataFileGuard.cs raw.sqlite3_db_config",
            $"{sqlite}Connection/SqliteDataFileGuard.cs raw.sqlite3_get_autocommit", $"{sqlite}SqliteSchemaCatalog.cs raw.sqlite3_db_readonly"],
            SemanticSources.Production.SelectMany(file => FrameworkUseDetectors.Members(file, Driver).Select(member => $"{file.Path} {member}")).Order(StringComparer.Ordinal));
        Assert.Equal(["SQLitePCL.raw.sqlite3_db_config(source.Handle, NoCheckpointOnClose, 1, out _) [1006, 1]", "raw.sqlite3_db_config(sqlite.Handle, NoCheckpointOnClose, 1, out _) [1006, 1]"],
            SemanticSources.Production.SelectMany(file => NativeCodeDetectors.CallsOf(file, Driver + ".raw", "sqlite3_db_config")).Order(StringComparer.Ordinal));
    }

    /// <summary>The namespace of the driver of SQLite, whose functions are those of the native library of the base.</summary>
    private const string Driver = "SQLitePCL";

    /// <summary>The option given to the driver is read with its value, and the function taken as a value, to be called out of sight, is told from a call.</summary>
    [Theory]
    [InlineData("using SQLitePCL; class A { const int Extensions = 1005; int M(sqlite3 opened) => raw.sqlite3_db_config(opened, Extensions, 1, out _); }",
        "raw.sqlite3_db_config(opened, Extensions, 1, out _) [1005, 1]")]
    [InlineData("class A { int M(SQLitePCL.sqlite3 opened, int option) => SQLitePCL.raw.sqlite3_db_config(opened,  option, 1, out _); }", "SQLitePCL.raw.sqlite3_db_config(opened, option, 1, out _) [1]")]
    [InlineData("using SQLitePCL; class A { delegate int Option(sqlite3 opened, int option, int value, out int result); Option M() => raw.sqlite3_db_config; }", "raw.sqlite3_db_config not called")]
    public void An_option_given_to_the_driver_of_the_base_is_read_with_its_value(string sample, string use) =>
        Assert.Equal([use], NativeCodeDetectors.CallsOf(SemanticSources.Sample(sample, SampleFile), Driver + ".raw", "sqlite3_db_config"));

    [Theory]
    [InlineData("using System.Runtime.InteropServices; static class Native { [DllImport(\"kernel32.dll\", CharSet = CharSet.Unicode)] public static extern uint GetFileAttributesW(string path); }",
        "kernel32.dll!GetFileAttributesW")]
    [InlineData("using System.Runtime.InteropServices; static class Native { [DllImport(\"shell32.dll\", EntryPoint = \"ShellExecuteExW\")] static extern bool Show(ref int request); }",
        "shell32.dll!ShellExecuteExW")]
    [InlineData("using System.Runtime.InteropServices; static partial class Native { [LibraryImport(\"shell32.dll\", EntryPoint = \"ShellExecuteW\")] public static partial nint Open(nint window); }",
        "shell32.dll!ShellExecuteW")]
    [InlineData("using System.Runtime.InteropServices; static partial class Native { [LibraryImport(\"kernel32.dll\")] public static partial uint WinExec(nint command, uint show); }",
        "kernel32.dll!WinExec")]
    [InlineData("using System.Runtime.InteropServices; class A { bool M() { return Beep(440, 100); [DllImport(\"kernel32.dll\")] static extern bool Beep(uint tone, uint time); } }",
        "kernel32.dll!Beep")]
    [InlineData("using Import = System.Runtime.InteropServices.DllImportAttribute; static class Native { [Import(\"SHELL32\")] static extern nint FindExecutableW(string file, string? folder, nint found); }",
        "SHELL32!FindExecutableW")]
    [InlineData("class A { [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.InternalCall)] static extern int Inside(); }", "?!Inside")]
    public void A_native_function_declared_in_any_way_is_read_with_its_library_and_its_entry_point(string sample, string function) =>
        Assert.Equal([$"{SampleFile}: {function}"], NativeCodeDetectors.NativeFunctions(SemanticSources.Sample(sample, SampleFile)));

    [Theory]
    [InlineData("static class Native { public static bool ShellExecuteExW(string folder) => folder.Length > 0; }")]
    [InlineData("static class Native { } // [DllImport(\"shell32.dll\")] static extern bool ShellExecuteExW(ref int request); in a comment")]
    [InlineData("abstract class Shell { public abstract bool Show(string folder); } partial class Parts { partial void Show(string folder); }")]
    public void A_method_written_in_csharp_is_no_native_function(string sample) =>
        Assert.Empty(NativeCodeDetectors.NativeFunctions(SemanticSources.Sample(sample, SampleFile)));

    [Theory]
    [InlineData("kernel32.dll!GetFileAttributesW", true)]
    [InlineData("kernel32.dll!GetFileAttributesExW", true)]
    [InlineData("kernel32.dll!CreateFileW", true)]
    [InlineData("kernel32.dll!FindFirstFileW", true)]
    [InlineData("shell32.dll!ShellExecuteW", true)]
    [InlineData("src/Topogenia.Host/Sample.cs: shell32.dll!ShellExecuteExW", true)]
    [InlineData("shell32.dll!SHOpenFolderAndSelectItems", true)]
    [InlineData("kernel32.dll!WinExec", true)]
    [InlineData("kernel32.dll!CreateProcessW", true)]
    [InlineData("advapi32.dll!CreateProcessAsUserW", true)]
    [InlineData("kernel32.dll!LoadLibraryExW", true)]
    [InlineData("ole32.dll!CoCreateInstance", true)]
    [InlineData("kernel32.dll!GetFileInformationByHandle", false)]
    [InlineData("kernel32.dll!GetFileType", false)]
    [InlineData("kernel32.dll!OpenProcess", false)]
    [InlineData("shell32.dll!Shell_NotifyIconW", false)]
    [InlineData("user32.dll!CreateWindowExW", false)]
    public void A_function_of_windows_that_takes_a_path_is_told_from_the_others(string function, bool takesPath) =>
        Assert.Equal(takesPath, NativeCodeDetectors.TakesPath(function));

    [Theory]
    [InlineData("class A { object M() => System.Type.GetTypeFromProgID(\"Shell.Application\")!; }", "Type.GetTypeFromProgID")]
    [InlineData("using static System.Type; class A { object M(System.Guid id) => GetTypeFromCLSID(id)!; }", "Type.GetTypeFromCLSID")]
    [InlineData("using System.Runtime.InteropServices; class A { delegate int Call(string path); object M(nint address) => Marshal.GetDelegateForFunctionPointer<Call>(address); }",
        "Marshal.GetDelegateForFunctionPointer")]
    [InlineData("class A { object M() => System.Runtime.InteropServices.NativeLibrary.Load(\"shell32.dll\"); }", "NativeLibrary")]
    [InlineData("using System.Runtime.InteropServices; class A { nint M(nint library) => NativeLibrary.GetExport(library, \"ShellExecuteExW\"); }", "NativeLibrary")]
    [InlineData("using Loader = System.Runtime.InteropServices.NativeLibrary; class A { System.Func<string, nint> M() => Loader.Load; }", "NativeLibrary")]
    [InlineData("class A { void M(dynamic shell, string path) => shell.Open(path); }", "dynamic")]
    [InlineData("class A { void M(object shell, string path) => ((dynamic)shell).Open(path); }", "dynamic")]
    [InlineData("unsafe class A { int M(delegate* unmanaged<char*, int> show, char* path) => show(path); }", "function pointer")]
    [InlineData("class A { unsafe int M(int* value) => *value; }", "unsafe")]
    [InlineData("using System.Runtime.InteropServices; [ComImport, Guid(\"00000000-0000-0000-0000-00000000000A\")] class ShellApplication { }", "ComImportAttribute")]
    [InlineData("using System.Runtime.InteropServices; class A { [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate int Show(string folder); }",
        "UnmanagedFunctionPointerAttribute")]
    [InlineData("using Microsoft.Data.Sqlite; class A { void M(SqliteConnection connection, string location) => connection.LoadExtension(location); }", "SqliteConnection.LoadExtension")]
    [InlineData("using Microsoft.Data.Sqlite; class A { void M(SqliteConnection connection) => connection.EnableExtensions(); }", "SqliteConnection.EnableExtensions")]
    [InlineData("using Microsoft.Data.Sqlite; class A { System.Action<string, string?> M(SqliteConnection connection) => connection.LoadExtension; }", "SqliteConnection.LoadExtension")]
    [InlineData("using SQLitePCL; class A { int M(sqlite3 opened, string location) => raw.sqlite3_load_extension(opened, utf8z.FromString(location), default, out _); }",
        "raw.sqlite3_load_extension")]
    [InlineData("using SQLitePCL; class A { int M(sqlite3 opened) => raw.sqlite3_enable_load_extension(opened, 1); }", "raw.sqlite3_enable_load_extension")]
    public void Every_other_way_to_native_code_or_to_com_is_detected(string sample, string detour) =>
        Assert.Contains($"{SampleFile} {detour}", Detours([SemanticSources.Sample(sample, SampleFile)]));

    /// <summary>A call that only the run resolves is seen where no word says it: the type of the expression is read, behind `var` too.</summary>
    [Fact]
    public void A_call_left_to_the_run_is_detected_in_a_file_that_never_writes_the_word()
    {
        var files = SemanticSources.Samples(
            (SampleFile, "class A { void M(string path) { var shell = Shell.Application; shell.Open(path); } }"),
            ("src/Topogenia.Host/Shell.cs", "static class Shell { public static dynamic Application => new object(); }"));
        Assert.Equal([$"{SampleFile} dynamic"], Detours([files[0]]));
    }

    [Theory]
    [InlineData("class A { object M() => System.Type.GetType(\"System.String\")!; }")]
    [InlineData("using System.Runtime.InteropServices; class A { int M() => Marshal.GetLastPInvokeError() + Marshal.SizeOf<int>(); }")]
    [InlineData("class A { string M(string dynamic) => dynamic; }")]
    [InlineData("class A { System.Func<string, int> M() => path => path.Length; }")]
    [InlineData("namespace Sample { static class NativeLibrary { public static int Load(string name) => name.Length; } class A { int M() => NativeLibrary.Load(\"shell32.dll\"); } }")]
    [InlineData("class A { } // Type.GetTypeFromProgID(\"Shell.Application\"), NativeLibrary.Load and dynamic in a comment")]
    [InlineData("using Microsoft.Data.Sqlite; class A { int M(SqliteConnection connection) { connection.Open(); return SQLitePCL.raw.sqlite3_get_autocommit(connection.Handle); } }")]
    public void Managed_code_and_look_alike_names_are_no_way_to_native_code(string sample) =>
        Assert.Empty(Detours([SemanticSources.Sample(sample, SampleFile)]));
}
