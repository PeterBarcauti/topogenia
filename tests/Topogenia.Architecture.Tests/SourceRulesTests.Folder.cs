// SourceRulesTests.Folder.cs
// The exception of ADR 104 to S-5.4-13, « Open the folder », held on the source text: who uses its port, what its port reads, what its listed file gives to the shell, calls and is word for word, and the surface it offers to the other files, each rule with its sample.

namespace Topogenia.Architecture.Tests;

public sealed partial class SourceRulesTests
{
    private const string Desktop = "Topogenia.Host.Desktop.";

    /// <summary>The port of « Open the folder » (ADR 104): its interfaces, its rules of the path and its opener, without any API of files.</summary>
    private const string FolderPort = "src/Topogenia.Host/Desktop/FolderOpening.cs";

    /// <summary>The listed file behind the port: the look at the folder and the shell asked to show it.</summary>
    internal const string FolderShell = "src/Topogenia.Host/Desktop/ShellFolderOpener.cs";

    /// <summary>The one route that reads a document and uses the port.</summary>
    private const string FolderRoute = "src/Topogenia.Host/Api/Documents/DocumentFolderApi.cs";

    /// <summary>The registration of the desktop mode, where the port gets its parts.</summary>
    private const string FolderRegistration = "src/Topogenia.Host/Desktop/DesktopServices.cs";

    /// <summary>The entry points of the sheet and of the documents, where the route of the folder is declared.</summary>
    private const string SheetRoutes = "src/Topogenia.Host/Endpoints/SheetEndpoints.cs";

    private static SourceFile Shipped(string path) => SemanticSources.Production.Single(file => file.Path == path);

    private static HashSet<string> TypesOf(params SourceFile[] files) => files.SelectMany(ShippedCodeDetectors.DeclaredTypes).ToHashSet(StringComparer.Ordinal);

    private static List<string> FilesUsing(IReadOnlySet<string> types) =>
        [.. SemanticSources.Production.Where(file => ShippedCodeDetectors.UsesOf(file, types).Any()).Select(file => file.Path).Order(StringComparer.Ordinal)];

    /// <summary>
    /// The exception to S-5.4-13, word for word (ADR 104): in desktop mode, on a click of the person, the program asks
    /// Windows to show the folder that holds the document; it never opens a document. The types of the port are used by
    /// four files and no other: the port itself, the listed file that implements it, the registration of the desktop mode
    /// and the route; among them the route alone reads a document. A second file that hands a location to the port, or a
    /// file that reads a document and takes the port, fails here. The port opens nothing itself, and the listed file
    /// touches the disk and the shell by these members only: the attributes of a path read (the one type of the files it
    /// names that is not admitted everywhere, with the attribute that says a folder), and one native function, the one
    /// of the shell; no file opened, read or listed, no process started. What that function is given, and everything
    /// else the listed file calls, is held by the rules below.
    /// </summary>
    [Fact]
    public void Only_the_route_of_the_folder_hands_the_location_of_a_document_to_its_port()
    {
        var port = Shipped(FolderPort);
        Assert.DoesNotContain(FolderPort, FileSystemFiles);
        Assert.Empty(ShippedCodeDetectors.DocumentReads(port));
        var types = TypesOf(port);
        Assert.Contains(Desktop + "IFolderOpener", types);
        var users = SemanticSources.Production.Where(file => ShippedCodeDetectors.UsesOf(file, types).Any()).ToList();
        Assert.Equal(new[] { FolderRoute, FolderRegistration, FolderPort, FolderShell }.Order(StringComparer.Ordinal),
            users.Select(file => file.Path).Order(StringComparer.Ordinal));
        Assert.Equal([FolderRoute], users.Where(file => ShippedCodeDetectors.DocumentReads(file).Any()).Select(file => file.Path));
        var shell = Shipped(FolderShell);
        Assert.Equal(["Directory", "File", "FileAttributes", "GetAttributes"],
            ShippedCodeDetectors.FileSystemAccesses(shell).Select(place => place.Split(' ')[^1]).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
        Assert.Equal([$"{FolderShell}: shell32.dll!ShellExecuteExW"], NativeCodeDetectors.NativeFunctions(shell));
    }

    /// <summary>The rule of the port on samples: every file that uses a type of the port is found, and which of them read a document.</summary>
    [Fact]
    public void A_second_file_that_hands_the_location_of_a_document_to_a_port_is_detected()
    {
        const string hands = "{ void M(Sample.IOpener opener, Topogenia.Domain.Model.Document document) => opener.Show(document.Location!); }";
        var files = SemanticSources.Samples(
            ("src/Topogenia.Host/Port.cs", "namespace Sample; public interface IOpener { void Show(string path); } public sealed class Opener : IOpener { public void Show(string path) { } }"),
            ("src/Topogenia.Host/Route.cs", "class Route " + hands),
            ("src/Topogenia.Host/Second.cs", "class Second " + hands),
            ("src/Topogenia.Host/Registration.cs", "class Registration { object M() => new Sample.Opener(); }"),
            ("src/Topogenia.Host/Reader.cs", "class Reader { string? M(Topogenia.Domain.Model.Document document) => document.Location; }"));
        var types = ShippedCodeDetectors.DeclaredTypes(files[0]).ToHashSet(StringComparer.Ordinal);
        var users = files.Where(file => ShippedCodeDetectors.UsesOf(file, types).Any()).ToList();
        Assert.Equal(["src/Topogenia.Host/Port.cs", "src/Topogenia.Host/Route.cs", "src/Topogenia.Host/Second.cs", "src/Topogenia.Host/Registration.cs"],
            users.Select(file => file.Path));
        Assert.Equal(["src/Topogenia.Host/Route.cs", "src/Topogenia.Host/Second.cs"],
            users.Where(file => ShippedCodeDetectors.DocumentReads(file).Any()).Select(file => file.Path));
    }

    /// <summary>
    /// ADR 104, point 4: the rules of the path read its text alone, before anything is looked at. The port uses no type of
    /// `System.IO` at all, the helpers of paths included (a full path asked to the framework reads the disk, and the
    /// network, for a path that holds a tilde, outside the bound and the single turn of the look), declares no native
    /// function and takes no other way to native code. The listed file behind it does use that namespace (control).
    /// </summary>
    [Fact]
    public void The_port_of_the_folder_reads_the_text_of_a_path_and_uses_no_type_of_the_file_system()
    {
        var port = Shipped(FolderPort);
        Assert.Empty(NativeCodeDetectors.NamespaceUses(port, "System.IO"));
        Assert.Empty(ShippedCodeDetectors.FileSystemAccesses(port));
        Assert.Empty(NativeCodeDetectors.NativeFunctions(port));
        Assert.Empty(NativeCodeDetectors.NativeDetours(port));
        Assert.NotEmpty(NativeCodeDetectors.NamespaceUses(Shipped(FolderShell), "System.IO"));
    }

    [Theory]
    [InlineData("class A { string M(string path) => System.IO.Path.GetFullPath(path); }")]
    [InlineData("using System.IO; class A { bool M(string path) => Path.Exists(path); }")]
    [InlineData("using static System.IO.Path; class A { char M() => DirectorySeparatorChar; }")]
    [InlineData("using Paths = System.IO.Path; class A { string? M(string path) => Paths.GetFileName(path); }")]
    [InlineData("class A { bool M(System.Exception failure) => failure is System.IO.IOException; }")]
    [InlineData("class A { object M(string path) { var found = Probe.Of(path); return found; } }")]
    [InlineData("class A { int M(string path) => Probe.Of(path).Name.Length; }")]
    [InlineData("class A { object M() => new System.IO.Compression.ZipArchive(Probe.Empty); }")]
    public void Every_use_of_a_type_of_the_file_system_by_a_file_is_detected(string sample) =>
        Assert.NotEmpty(NativeCodeDetectors.NamespaceUses(WithProbe(sample), "System.IO"));

    [Theory]
    [InlineData("class A { string M(string path) => path.Replace('/', '\\\\'); }")]
    [InlineData("class A { bool M(string path) => path.Any(char.IsControl) || path.Length > 259; }")]
    [InlineData("class A { int M(string path) => Probe.Length(path); }")]
    [InlineData("class Path { public static string GetFullPath(string text) => text; } class A { string M(string text) => Path.GetFullPath(text); }")]
    [InlineData("class A { } // System.IO.Path.GetFullPath(path) in a comment")]
    public void A_text_read_as_a_text_uses_no_type_of_the_file_system(string sample) =>
        Assert.Empty(NativeCodeDetectors.NamespaceUses(WithProbe(sample), "System.IO"));

    /// <summary>A sample compiled with a helper of another file, whose results are types of the file system that the sample never names.</summary>
    private static SourceFile WithProbe(string sample) => SemanticSources.Samples(
        ("src/Topogenia.Host/Sample.cs", sample),
        ("src/Topogenia.Host/Probe.cs", "static class Probe { public static System.IO.FileInfo Of(string path) => new(path); public static int Length(string path) => path.Length; "
            + "public static System.IO.MemoryStream Empty => new(); }"))[0];

    /// <summary>The structure the listed file gives to the function of the shell (`SHELLEXECUTEINFOW`).</summary>
    private const string ShellRequest = Desktop + "ShellFolderOpener.Native.ExecuteInfo";

    /// <summary>
    /// What the listed file gives to the shell, field by field (ADR 104, point 6): the size of the structure; the verb
    /// by its constant, `explore`; the folder it was given; a window shown as usual; and the class where the verb is
    /// read, which `ClassOf` names for a root alone (a drive or a share: the class of the folders, by its constant)
    /// and leaves unnamed for any other folder, the mask following it (`SEE_MASK_NOASYNC` 0x100 and
    /// `SEE_MASK_FLAG_NO_UI` 0x400 always, `SEE_MASK_CLASSNAME` 0x1 with a class named). A class given to every folder
    /// would hand a file put where the folder was to the verb of the folders; none given to a root would read the verb
    /// a disc declares for it. Neither parameters nor a working folder: the structure does declare them, and nothing
    /// writes nor reads them. The function is called once, with the folder that `Show` received, as the one question
    /// asked on the thread of the shell, where a failure is answered and never left to end the program. The constants
    /// are held by their values; everything else the file calls is listed, the framework included: the attributes of a
    /// path, the thread of the call, the rules and the errors read by the port; a new call of any kind fails here.
    /// These lists say what each name of the file means; they do not say that nothing stands between two of them: a
    /// class named and then given another value, a folder changed before it is given, a question asked twice, a line
    /// kept in a comment while the code beside it says the contrary. The file is therefore also held as it is written,
    /// word for word, comments aside (<see cref="ShellFile"/>): any change of its code fails here and is made on purpose.
    /// </summary>
    [Fact]
    public void The_listed_file_of_the_folder_gives_the_shell_a_verb_a_class_and_the_folder_and_calls_nothing_else()
    {
        var shell = Shipped(FolderShell);
        Assert.Equal(["Size", "Mask", "Window", "Verb", "File", "Parameters", "Directory", "Show", "Instance", "IdList", "Class", "ClassKey", "HotKey", "Icon", "Process"],
            NativeCodeDetectors.FieldsOf(shell, ShellRequest));
        Assert.Equal(["Size = Marshal.SizeOf<Native.ExecuteInfo>()",
            "Mask = named is null ? Native.NoAsync | Native.NoWindowOnError : Native.ClassName | Native.NoAsync | Native.NoWindowOnError",
            "Verb = ShellFolderOpener.Verb (explore)", "File = parameter folder", "Show = Native.ShowNormal (1)", "Class = named"],
            NativeCodeDetectors.FieldsGiven(shell, ShellRequest));
        Assert.Equal(["ShellFolderOpener.Verb = explore", "ShellFolderOpener.Class = Folder", "ShellFolderOpener.Unnamed = 31", "Native.ClassName = 1",
            "Native.NoAsync = 256", "Native.NoWindowOnError = 1024", "Native.ShowNormal = 1"], NativeCodeDetectors.Constants(shell));
        Assert.Equal(ShellFile, NativeCodeDetectors.Declarations(shell));
        Assert.Equal(["OnShellThread(() => Ask(folder))", "Ask(folder)", "Answer(question)", "ClassOf(folder)", "Native.ShellExecuteExW(ref request)"],
            NativeCodeDetectors.OwnCalls(shell));
        Assert.Equal(["System.Enum.HasFlag", "System.IO.File.GetAttributes", "System.OperatingSystem.IsWindows", "System.Runtime.InteropServices.Marshal.GetLastPInvokeError",
            "System.Runtime.InteropServices.Marshal.SizeOf", "System.Threading.Thread..ctor", "System.Threading.Thread.IsBackground", "System.Threading.Thread.Join",
            "System.Threading.Thread.SetApartmentState", "System.Threading.Thread.Start", Desktop + "FolderPaths.LookOf", Desktop + "FolderPaths.OutcomeOf",
            Desktop + "FolderPaths.Root"], NativeCodeDetectors.ForeignMembers(shell));
        Assert.Empty(NativeCodeDetectors.NativeDetours(shell));
    }

    private const string ShellText = "[MarshalAs(UnmanagedType.LPWStr)] public string? ";

    /// <summary>
    /// The listed file as it is written, comments aside (<see cref="NativeCodeDetectors.Declarations"/>): the look; then
    /// the class that asks the shell, its constants and its five methods; the constants of the function of the shell,
    /// the structure it is given and its declaration.
    /// </summary>
    private static readonly string[] ShellFile =
    [
        "public sealed class WindowsFolderDisk : IFolderDisk {",
        "public FolderLook Look(string path) { try { return File.GetAttributes(path).HasFlag(FileAttributes.Directory) ? FolderLook.Folder : FolderLook.NotFolder; } "
            + "catch (OperationCanceledException) { return FolderLook.Unreachable; } "
            + "catch (Exception failure) when (failure is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException) { return FolderPaths.LookOf(failure); } }",
        "}",
        "public sealed class ShellFolderOpener : IFolderShell {",
        "public const string Verb = \"explore\";", "public const string Class = \"Folder\";", "private const int Unnamed = 31;",
        "public FolderOutcome Show(string folder) { if (!OperatingSystem.IsWindows()) return FolderOutcome.Unreachable; var error = OnShellThread(() => Ask(folder)); "
            + "return error == 0 ? FolderOutcome.Shown : FolderPaths.OutcomeOf(error); }",
        "public static string? ClassOf(string folder) => FolderPaths.Root(folder) ? Class : null;",
        "public static int OnShellThread(Func<int> question) { var error = Unnamed; var thread = new Thread(() => error = Answer(question)) { IsBackground = true }; "
            + "if (OperatingSystem.IsWindows()) thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join(); return error; }",
        "private static int Answer(Func<int> question) { try { return question(); } catch (Exception) { return Unnamed; } }",
        "private static int Ask(string folder) { var named = ClassOf(folder); var request = new Native.ExecuteInfo { Size = Marshal.SizeOf<Native.ExecuteInfo>(), "
            + "Mask = named is null ? Native.NoAsync | Native.NoWindowOnError : Native.ClassName | Native.NoAsync | Native.NoWindowOnError, "
            + "Verb = Verb, File = folder, Show = Native.ShowNormal, Class = named, }; if (Native.ShellExecuteExW(ref request)) return 0; "
            + "var error = Marshal.GetLastPInvokeError(); return error == 0 ? Unnamed : error; }",
        "private static class Native {",
        "public const uint ClassName = 0x00000001;", "public const uint NoAsync = 0x00000100;", "public const uint NoWindowOnError = 0x00000400;", "public const int ShowNormal = 1;",
        "[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] public struct ExecuteInfo {",
        "public int Size;", "public uint Mask;", "public IntPtr Window;", ShellText + "Verb;", ShellText + "File;", ShellText + "Parameters;", ShellText + "Directory;",
        "public int Show;", "public IntPtr Instance;", "public IntPtr IdList;", ShellText + "Class;", "public IntPtr ClassKey;", "public uint HotKey;", "public IntPtr Icon;",
        "public IntPtr Process;",
        "}",
        "[DllImport(\"shell32.dll\", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)] [DefaultDllImportSearchPaths(DllImportSearchPath.System32)] "
            + "[return: MarshalAs(UnmanagedType.Bool)] public static extern bool ShellExecuteExW(ref ExecuteInfo info);",
        "}", "}",
    ];

    /// <summary>
    /// The reading word for word on a sample: the headings, the members and the closing braces in the order of the text;
    /// a line in a comment, a comment inside a line and the ends of lines stand for nothing; a line under a symbol of
    /// the build is kept with its directive.
    /// </summary>
    [Fact]
    public void What_a_file_declares_is_read_word_for_word_and_a_line_in_a_comment_stands_for_nothing()
    {
        var file = SemanticSources.Sample("namespace Sample;\n/// <summary>Shown.</summary>\n[System.Serializable] public sealed class Shell : System.IDisposable\n{\n"
            + "    // public static string? ClassOf(string folder) => null;\n    public static string? ClassOf(string folder) =>\n        folder.Length > 3 ? null /* none */ : \"Folder\";\n"
            + "    private struct Info { public int Size; }\n#if MUSEUM\n    public static void More() { }\n#endif\n    public void Dispose() { }\n}\nenum Look { Folder, Missing }",
            "src/Topogenia.Host/Sample.cs");
        Assert.Equal(["[System.Serializable] public sealed class Shell : System.IDisposable {",
            "public static string? ClassOf(string folder) => folder.Length > 3 ? null : \"Folder\";", "private struct Info {", "public int Size;", "}",
            "#if MUSEUM public static void More() { } #endif public void Dispose() { }", "}", "enum Look { Folder, Missing }"], NativeCodeDetectors.Declarations(file));
    }

    /// <summary>The rule above on a sample: parameters, a working folder and a field read are seen, with what each is given; a second call too.</summary>
    [Fact]
    public void A_parameter_or_a_working_folder_given_to_a_native_function_is_detected()
    {
        var file = SemanticSources.Sample("using System.Runtime.InteropServices; static class Shell { public const string Verb = \"explore\"; "
            + "struct Info { public string? Verb; public string? File; public string? Parameters; public string? Directory; } "
            + "[DllImport(\"shell32.dll\")] static extern bool ShellExecuteExW(ref Info info); "
            + "public static bool Show(string folder, string options) { var info = new Info { Verb = Verb, File = folder + \"\\\\\", Parameters = options }; "
            + "info.Directory = System.IO.Path.GetTempPath(); var kept = info.File; return ShellExecuteExW(ref info) && ShellExecuteExW(ref info) && kept is not null; } }",
            "src/Topogenia.Host/Sample.cs");
        Assert.Equal(["Verb", "File", "Parameters", "Directory"], NativeCodeDetectors.FieldsOf(file, "Shell.Info"));
        Assert.Equal(["Verb = Shell.Verb (explore)", "File = folder + \"\\\\\"", "Parameters = parameter options", "Directory = System.IO.Path.GetTempPath()", "File read"],
            NativeCodeDetectors.FieldsGiven(file, "Shell.Info"));
        Assert.Equal(["ShellExecuteExW(ref info)", "ShellExecuteExW(ref info)"], NativeCodeDetectors.OwnCalls(file));
        Assert.Equal(["System.IO.Path.GetTempPath"], NativeCodeDetectors.ForeignMembers(file));
        Assert.Equal(["Shell.Verb = explore"], NativeCodeDetectors.Constants(file));
    }

    /// <summary>
    /// The surface of the exception, beyond the files that name the port (ADR 104, point 8): a member added to an admitted
    /// file, which another file could call without naming a type of the port, fails one of the rules below. First, who
    /// carries the port: the opener carries `IFolderOpener` and nothing else; the listed file declares two classes, one
    /// that carries the look and nothing else, one that carries the shell and nothing else, so that who holds the look
    /// cannot reach the shell by a cast; and no other type of the shipped code carries one of the three, nor extends
    /// one, nor derives from one of the three classes.
    /// </summary>
    [Fact]
    public void The_port_of_the_folder_is_carried_by_its_opener_and_by_its_listed_file_and_by_no_other_type()
    {
        string[] held = [Desktop + "IFolderOpener", Desktop + "IFolderDisk", Desktop + "IFolderShell", Desktop + "CheckedFolderOpener", Desktop + "ShellFolderOpener",
            Desktop + "WindowsFolderDisk"];
        var carrying = SemanticSources.Production.SelectMany(SurfaceDetectors.Carried)
            .Where(entry => entry.Split(" : ") is [var type, var carried] && (held.Contains(type) || carried.Split(", ").Intersect(held).Any()));
        Assert.Equal([$"{Desktop}CheckedFolderOpener : {Desktop}IFolderOpener", $"{Desktop}ShellFolderOpener : {Desktop}IFolderShell",
            $"{Desktop}WindowsFolderDisk : {Desktop}IFolderDisk"], carrying.Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// The file of the route declares its handler and the codes of its refusals. The handler offers one constructor and
    /// one method, which takes two identifiers and the token of the request: the path comes from the base, never from who
    /// calls. No member of the file that another file may reach takes a text; the codes are constants.
    /// </summary>
    [Fact]
    public void The_route_of_the_folder_offers_one_constructor_and_one_method_that_takes_identifiers_alone()
    {
        const string handler = "Topogenia.Host.Api.Documents.DocumentFolderApi";
        const string codes = "Topogenia.Host.Api.Documents.DocumentFolderErrors.const ";
        var route = Shipped(FolderRoute);
        Assert.Equal([handler, "Topogenia.Host.Api.Documents.DocumentFolderErrors"], ShippedCodeDetectors.DeclaredTypes(route).Order(StringComparer.Ordinal));
        var reachable = SurfaceDetectors.Reachable(route).Order(StringComparer.Ordinal).ToList();
        Assert.Equal([$"{handler}..ctor(QueryRunner, IOptions<ProductOptions>, IFolderOpener?)", $"{handler}.ShowAsync(Guid, Guid, CancellationToken)"],
            reachable.Where(member => !member.StartsWith(codes, StringComparison.Ordinal)));
        Assert.Equal(7, reachable.Count(member => member.StartsWith(codes, StringComparison.Ordinal)));
        Assert.Empty(SurfaceDetectors.TextTakers(route));
    }

    /// <summary>
    /// Who names the handler of the route: its own file and the entry points of the sheet, twice (its type and its
    /// method), both inside the one route of the folder, a `MapPost`. No other handler calls it: a read of the sheet that
    /// would show a folder names it and fails here. The composition of the host never names it: it registers the handlers
    /// by the end of their name (control: this one is among them).
    /// </summary>
    [Fact]
    public void The_handler_of_the_folder_is_named_by_its_file_and_by_its_one_route_alone()
    {
        var types = TypesOf(Shipped(FolderRoute));
        Assert.Equal([FolderRoute, SheetRoutes], FilesUsing(types));
        var routes = Shipped(SheetRoutes);
        Assert.Equal(["DocumentFolderApi", "ShowAsync"], ShippedCodeDetectors.UsesOf(routes, types).Select(place => place.Split(' ')[^1]));
        Assert.Equal(["MapPost \"/documents/{documentId:guid}/folder\"", "MapPost \"/documents/{documentId:guid}/folder\""], SurfaceDetectors.RoutesNaming(routes, types));
        Assert.Contains(typeof(Topogenia.Host.Api.Documents.DocumentFolderApi), Topogenia.Host.Composition.HostServices.Handlers());
    }

    /// <summary>
    /// What the registration of the desktop mode does with the port and with the classes behind it: four lines, each a
    /// type given to the container, and nothing else; no delegate, no factory that would take a text and hand it to the
    /// opener. The two classes behind the port are named by that registration and by their own file alone, and no text
    /// of the shipped code writes the name of a type of the exception (a type found by its name at the run).
    /// </summary>
    [Fact]
    public void The_desktop_mode_registers_the_port_of_the_folder_by_four_lines_and_does_nothing_else_with_it()
    {
        var (port, shell) = (Shipped(FolderPort), Shipped(FolderShell));
        Assert.Equal(["services.TryAddSingleton<IFolderDisk, WindowsFolderDisk>();", "services.TryAddSingleton<IFolderShell, ShellFolderOpener>();",
            "services.TryAddSingleton(FolderOpeningOptions.Default);", "services.AddSingleton<IFolderOpener, CheckedFolderOpener>();"],
            SurfaceDetectors.StatementsNaming(Shipped(FolderRegistration), TypesOf(port, shell)));
        Assert.Equal([FolderRegistration, FolderShell], FilesUsing(TypesOf(shell)));
        string[] names = ["IFolderOpener", "IFolderDisk", "IFolderShell", "CheckedFolderOpener", "ShellFolderOpener", "WindowsFolderDisk", "DocumentFolderApi"];
        Assert.Empty(SemanticSources.Production.SelectMany(file => SurfaceDetectors.TextsNaming(file, names)));
    }

    /// <summary>
    /// The members of the four admitted files that another file may reach and that take a text, exactly: the three ports
    /// and their three classes, the rules of the path and the path they read, and the class where the verb is read for
    /// a folder, which answers on the text and reaches nothing. The registration and the route offer none.
    /// </summary>
    [Fact]
    public void The_members_of_the_admitted_files_that_take_a_text_are_those_of_the_port_and_of_its_listed_file()
    {
        Assert.Equal([$"{Desktop}CheckedFolderOpener.ShowAsync(string, CancellationToken)", $"{Desktop}FolderPath..ctor(FolderOutcome?, string, string?)",
            $"{Desktop}FolderPath.Item {{ get; init; }}", $"{Desktop}FolderPath.Parent {{ get; init; }}", $"{Desktop}FolderPaths.AsFolder(string)",
            $"{Desktop}FolderPaths.Read(string)", $"{Desktop}FolderPaths.Root(string)", $"{Desktop}IFolderDisk.Look(string)",
            $"{Desktop}IFolderOpener.ShowAsync(string, CancellationToken)", $"{Desktop}IFolderShell.Show(string)"],
            SurfaceDetectors.TextTakers(Shipped(FolderPort)).Order(StringComparer.Ordinal));
        Assert.Equal([$"{Desktop}ShellFolderOpener.ClassOf(string)", $"{Desktop}ShellFolderOpener.Show(string)", $"{Desktop}WindowsFolderDisk.Look(string)"],
            SurfaceDetectors.TextTakers(Shipped(FolderShell)).Order(StringComparer.Ordinal));
        Assert.Empty(SurfaceDetectors.TextTakers(Shipped(FolderRegistration)));
        Assert.Empty(SurfaceDetectors.TextTakers(Shipped(FolderRoute)));
    }
}
