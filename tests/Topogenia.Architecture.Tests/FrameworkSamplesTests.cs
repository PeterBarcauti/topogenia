// FrameworkSamplesTests.cs
// The samples of the rules written as lists of what is admitted: the twenty-five ways round the first lists that the second review of « Open the folder » compiled, each seen by its rule, what a file that reads a document may not use, a type of the files that no name writes, the lines that only some builds compile, the files that a build compiles and no rule reads, the settings that change what it compiles, and what a file written by a tool may not use (S-5.4-13, S-5.4-21).

namespace Topogenia.Architecture.Tests;

public sealed class FrameworkSamplesTests
{
    private const string Sample = "src/Topogenia.Host/Sample.cs";

    private const string Usings = "using Microsoft.AspNetCore.Hosting; using Microsoft.AspNetCore.Http.Features; using Microsoft.Extensions.Configuration; "
        + "using Microsoft.Extensions.FileSystemGlobbing; using System.Security.Cryptography.X509Certificates;\n";

    /// <summary>A class that reads the location of a document, put in the same file as a sample: the file is then one that reads a document.</summary>
    private const string Reads = " class Reads { string? Of(Topogenia.Domain.Model.Document document) => document.Location; }";

    private static SourceFile Compiled(string sample) => SemanticSources.Sample(Usings + sample, Sample);

    /// <summary>The rules that see a file: its paths, its processes, native code, a call by a name written in a text, a namespace outside the list, the helpers of Visual Basic.</summary>
    private static List<string> SeenBy(SourceFile file)
    {
        var rules = new (string Rule, bool Seen)[]
        {
            ("files", ShippedCodeDetectors.FileSystemAccesses(file).Any()), ("process", ShippedCodeDetectors.ProcessStarts(file).Any()),
            ("native", NativeCodeDetectors.NativeDetours(file).Any()), ("late", FrameworkUseDetectors.LateBindings(file).Any()),
            ("namespace", FrameworkUseDetectors.Namespaces(file).Except(FrameworkRulesTests.AdmittedNamespaces, StringComparer.Ordinal).Any()),
            ("basic", NativeCodeDetectors.NamespaceUses(file, "Microsoft.VisualBasic").Any()),
        };
        return [.. rules.Where(rule => rule.Seen).Select(rule => rule.Rule)];
    }

    /// <summary>
    /// The twenty-five APIs of the framework that no detector saw (second review, defect 1), each given the location of a
    /// document: to look at, list or open a folder (6), to read a file (12), to send one as the reply (1), to load or run
    /// one (2), to start a process (1), to reach an object of COM of the shell (1), to call by a name written in a text
    /// (2). Each is seen by the rules named: where it stands in the shipped code, at least one of them fails.
    /// </summary>
    [Theory]
    [InlineData("class A { object M(string location) => new System.IO.Enumeration.FileSystemEnumerable<string>(location, (ref System.IO.Enumeration.FileSystemEntry entry) => entry.ToFullPath()); }", "files namespace")]
    [InlineData("class A { void M(string location) => System.Environment.CurrentDirectory = location; }", "files")]
    [InlineData("class A { bool M(string location) => Microsoft.VisualBasic.FileIO.FileSystem.FileExists(location); }", "files namespace basic")]
    [InlineData("class A { object M(string location) => new Microsoft.Extensions.FileProviders.Internal.PhysicalDirectoryContents(location); }", "files namespace")]
    [InlineData("class A { object M(Matcher matcher, string location) => matcher.GetResultsInFullPath(location); }", "files namespace")]
    [InlineData("class A { object M(string location) => new System.Security.AccessControl.FileSecurity(location, System.Security.AccessControl.AccessControlSections.Access); }", "files")]
    [InlineData("class A { string M(string location) => Microsoft.VisualBasic.FileIO.FileSystem.ReadAllText(location); }", "files namespace basic")]
    [InlineData("class A { object M(string location) => System.Reflection.AssemblyName.GetAssemblyName(location); }", "files")]
    [InlineData("class A { void M(string location, string folder) => System.Formats.Tar.TarFile.ExtractToDirectory(location, folder, false); }", "files namespace")]
    [InlineData("class A { object M(string location) => new System.Net.Mail.Attachment(location); }", "files namespace")]
    [InlineData("class A { void M(System.Data.DataTable table, string location) => table.ReadXml(location); }", "files")]
    [InlineData("class A { object M(string location) => new System.Resources.ResourceReader(location); }", "files namespace")]
    [InlineData("class A { object M(string location) => X509Certificate2.GetCertContentType(location); }", "files namespace")]
    [InlineData("class A { void M(X509Certificate2Collection certificates, string location) => certificates.Import(location); }", "files namespace")]
    [InlineData("class A { object M(string location) => new System.Runtime.Loader.AssemblyDependencyResolver(location); }", "files late namespace")]
    [InlineData("class A { object? M(System.Xml.Schema.XmlSchemaSet schemas, string location) => schemas.Add(null, location); }", "files namespace")]
    [InlineData("class A { void M(IConfigurationBuilder builder, string location) => builder.AddIniFile(location); }", "files")]
    [InlineData("class A { void M(Microsoft.AspNetCore.Server.Kestrel.Core.ListenOptions listen, string location) => listen.UseHttps(location); }", "files")]
    [InlineData("class A { Task M(IHttpResponseBodyFeature reply, string location) => reply.SendFileAsync(location, 0, null); }", "files")]
    [InlineData("class A { int M(string location) => System.AppDomain.CurrentDomain.ExecuteAssembly(location); }", "files late")]
    [InlineData("class A { object? M(string location) => System.Activator.CreateInstanceFrom(location, \"Museum.Tour\"); }", "files late namespace")]
    [InlineData("class A { int M(string location) => Microsoft.VisualBasic.Interaction.Shell(\"explorer.exe \\\"\" + location + \"\\\"\"); }", "process namespace basic")]
    [InlineData("class A { object? M(string location) { var shell = Microsoft.VisualBasic.Interaction.CreateObject(\"Shell.Application\"); "
        + "return shell.GetType().InvokeMember(\"Open\", System.Reflection.BindingFlags.InvokeMethod, null, shell, [location]); } }", "native late namespace basic")]
    [InlineData("class A { object? M(string location) => System.Type.GetType(\"System.Diagnostics.Process, System.Diagnostics.Process\")!.GetMethod(\"Start\", [typeof(string)])!.Invoke(null, [location]); }", "late")]
    [InlineData("class A { object? M(string location) => System.Type.GetType(\"System.IO.File\")!.GetMethod(\"ReadAllBytes\")!.Invoke(null, [location]); }", "late")]
    public void Each_way_round_the_lists_that_the_second_review_found_is_seen_by_its_rules(string sample, string rules)
    {
        Assert.Equal(rules.Split(' '), SeenBy(Compiled(sample)));
        var reader = Compiled(sample + Reads);
        Assert.NotEmpty(ShippedCodeDetectors.DocumentReads(reader));
        Assert.NotEmpty(FrameworkRulesTests.ReaderOffences(reader));
    }

    /// <summary>
    /// What the lists of a file that reads a document refuse beyond those: any namespace that is not admitted for it,
    /// though the shipped code uses it elsewhere; the environment and the domain of the process, the maker of objects;
    /// the description of a type used for more than a name; a type of the files that is no stream in memory.
    /// </summary>
    [Theory]
    [InlineData("class A { object M() => new System.Net.Http.HttpRequestMessage(); }", "namespace System.Net.Http")]
    [InlineData("class A { object M(string text) => System.Text.RegularExpressions.Regex.Match(text, \"a\"); }", "namespace System.Text.RegularExpressions")]
    [InlineData("class A { object M(string text) => System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)); }", "namespace System.Security.Cryptography")]
    [InlineData("class A { string? M(string name) => System.Environment.GetEnvironmentVariable(name); }", "type System.Environment")]
    [InlineData("class A { object M() => System.AppDomain.CurrentDomain; }", "type System.AppDomain")]
    [InlineData("class A { object? M(System.Type type) => System.Activator.CreateInstance(type); }", "type System.Activator")]
    [InlineData("class A { object M() => System.GC.GetTotalMemory(false); }", "type System.GC")]
    [InlineData("class A { object? M(System.Type type, string name) => type.GetMethod(name); }", "member Type.GetMethod")]
    [InlineData("class A { object? M(System.Reflection.PropertyInfo property, object target) => property.GetValue(target); }", "member PropertyInfo.GetValue")]
    [InlineData("class A { object M(System.IO.Stream stream) => new System.IO.BinaryReader(stream); }", "type System.IO.BinaryReader")]
    [InlineData("class A { object M() => new System.Threading.Thread(() => { }); }", "type System.Threading.Thread")]
    public void What_a_file_that_reads_a_document_may_not_use_is_detected(string sample, string offence) =>
        Assert.Contains($"{Sample} {offence}", FrameworkRulesTests.ReaderOffences(Compiled(sample + Reads)));

    [Theory]
    [InlineData("class A { string M(Topogenia.Domain.Model.Document document) => document.Name.Trim().ToUpperInvariant(); }")]
    [InlineData("class A { System.Collections.Generic.List<string> M(System.Collections.Generic.IEnumerable<string?> locations) => locations.OfType<string>().Order(System.StringComparer.Ordinal).ToList(); }")]
    [InlineData("class A { async Task<int> M(Task<string> location, CancellationToken token) => (await location.WaitAsync(token)).Length; }")]
    [InlineData("class A { string M(System.Guid id, System.DateTime at) => id.ToString(\"N\") + at.ToString(\"O\", System.Globalization.CultureInfo.InvariantCulture); }")]
    [InlineData("class A { } // System.Environment.CurrentDirectory and Type.GetType(\"System.IO.File\") in a comment")]
    public void Values_texts_collections_and_tasks_are_admitted_in_a_file_that_reads_a_document(string sample) =>
        Assert.Empty(FrameworkRulesTests.ReaderOffences(Compiled(sample + Reads)));

    /// <summary>The members of the first lists stay seen, and what reads the text alone or a stream already open stays free.</summary>
    [Theory]
    [InlineData("class A { string M() => System.IO.Path.GetTempFileName(); }", true)]
    [InlineData("class A { object M(System.IO.Stream stream) => new System.IO.BinaryReader(stream); }", true)]
    [InlineData("class A { object M(string location) => System.IO.Directory.EnumerateFiles(location); }", true)]
    [InlineData("class A { object M(string location) => new System.IO.IsolatedStorage.IsolatedStorageFileStream(location, System.IO.FileMode.Open); }", true)]
    [InlineData("class A { void M(IConfigurationBuilder builder, string location) => builder.AddXmlFile(location); }", true)]
    [InlineData("class A { void M(IConfigurationBuilder builder) => builder.AddUserSecrets(\"museum\"); }", true)]
    [InlineData("class A { object M(System.IO.Stream stream) => new System.IO.Compression.ZipArchive(stream); }", false)]
    [InlineData("class A { int M(System.IO.FileStream stream) => stream.ReadByte(); }", true)]
    [InlineData("class A { int M(System.IO.MemoryStream stream) => stream.ReadByte(); }", false)]
    [InlineData("class A { string M(System.IO.TextReader reader) => reader.ReadToEnd(); }", false)]
    [InlineData("class A { void M(IConfigurationBuilder builder, System.IO.Stream stream) => builder.AddJsonStream(stream); }", false)]
    [InlineData("class A { string M() => System.Environment.CurrentDirectory; }", false)]
    // The third review: an object of the files made by a `new(...)` whose type the library called gives, no name written.
    [InlineData("using Microsoft.AspNetCore.DataProtection; class A { void M(IDataProtectionBuilder keys, string location) => keys.PersistKeysToFileSystem(new(location)); }", true)]
    [InlineData("class A { object M(string location) => Microsoft.AspNetCore.DataProtection.DataProtectionProvider.Create(keyDirectory: new(location)); }", true)]
    [InlineData("class A { long M() { System.IO.MemoryStream stream = new(); return stream.Length; } }", false)]
    public void A_type_of_the_files_is_an_opening_unless_it_is_admitted(string sample, bool opens) =>
        Assert.Equal(opens, ShippedCodeDetectors.FileSystemAccesses(Compiled(sample)).Any());

    /// <summary>A helper of another file, whose parameters and results are types of the files that the sample never names.</summary>
    private const string Keep = "static class Keep { public static string All(System.IO.StreamReader reader) => reader.ReadToEnd(); public static System.IO.FileInfo Of(string path) => new(path); "
        + "public static System.IO.MemoryStream Empty => new(); public static int Length(string path) => path.Length; }";

    /// <summary>
    /// A type of the files that a file never names is read on its expressions: an object made for a method of another
    /// file that takes one (third review, defect 2), a value kept behind `var`, the element of an array, the argument
    /// of a generic type. The file that opens is seen, not only the one that names the type; what is admitted stays so.
    /// </summary>
    [Theory]
    [InlineData("class A { string M(string location) => Keep.All(new(location)); }", "StreamReader")]
    [InlineData("class A { object M(string location) { var found = Keep.Of(location); return found; } }", "FileInfo")]
    [InlineData("class A { int M(string location) => new[] { Keep.Of(location) }.Length; }", "FileInfo")]
    [InlineData("class A { System.Func<string, object> M() => Keep.Of; }", "FileInfo")]
    [InlineData("class A { long M() => Keep.Empty.Length; }", null)]
    [InlineData("class A { int M(string location) => Keep.Length(location); }", null)]
    public void A_type_of_the_files_that_no_name_writes_is_an_opening_in_the_file_that_holds_it(string sample, string? type)
    {
        var files = SemanticSources.Samples((Sample, sample), ("src/Topogenia.Host/Keep.cs", Keep));
        string[] expected = type is null ? [] : [type];
        Assert.Equal(expected, ShippedCodeDetectors.FileSystemAccesses(files[0]).Select(place => place.Split(' ')[^1]).Distinct(StringComparer.Ordinal));
        Assert.NotEmpty(ShippedCodeDetectors.FileSystemAccesses(files[1]));
    }

    /// <summary>A call by a name written in a text, however it is written; a description that is only read is none.</summary>
    [Theory]
    [InlineData("class A { object? M(string name) => System.Type.GetType(name); }", "Type.GetType")]
    [InlineData("using Kinds = System.Type; class A { System.Func<string, System.Type?> M() => Kinds.GetType; }", "Type.GetType")]
    [InlineData("class A { object? M(System.Reflection.MethodInfo method) => method.Invoke(null, null); }", "MethodBase.Invoke")]
    [InlineData("class A { object M(System.Reflection.ConstructorInfo maker) => maker.Invoke(null); }", "ConstructorInfo.Invoke")]
    [InlineData("class A { object? M(System.Delegate call) => call.DynamicInvoke(); }", "Delegate.DynamicInvoke")]
    [InlineData("class A { object M(System.Reflection.MethodInfo method) => method.CreateDelegate<System.Action>(); }", "MethodInfo.CreateDelegate")]
    [InlineData("class A { object M(string name) => System.Reflection.Assembly.Load(name); }", "Assembly.Load")]
    [InlineData("class A { object? M(System.Reflection.Assembly assembly, string name) => assembly.GetType(name); }", "Assembly.GetType")]
    [InlineData("class A { object M(System.Linq.Expressions.Expression<System.Func<int>> made) => made.Compile(); }", "Expression.Compile")]
    [InlineData("class A { object M() => new System.Reflection.Emit.DynamicMethod(\"tour\", typeof(void), []); }", "DynamicMethod")]
    [InlineData("class A { object M() => System.Runtime.Loader.AssemblyLoadContext.Default; }", "AssemblyLoadContext")]
    public void A_call_by_a_name_written_in_a_text_is_detected(string sample, string late) =>
        Assert.Contains($"{Sample} {late}", FrameworkUseDetectors.LateBindings(Compiled(sample)).Select(place => System.Text.RegularExpressions.Regex.Replace(place, @":\d+ ", " ")));

    [Theory]
    [InlineData("class A { string M(object value) => value.GetType().Name; }")]
    [InlineData("class A { string? M() => typeof(A).Assembly.GetName().Name; }")]
    [InlineData("class A { object? M() => typeof(A).GetMethod(\"M\"); }")]
    [InlineData("class A { object M() => typeof(System.Collections.Generic.List<>).MakeGenericType(typeof(int)); }")]
    [InlineData("class A { System.Func<int, int> M() => value => value + 1; }")]
    public void A_description_that_is_only_read_is_no_call_by_a_name(string sample) =>
        Assert.Empty(FrameworkUseDetectors.LateBindings(Compiled(sample)));

    /// <summary>A line under a symbol of the build, with its directives: the rules would not read it, and the build that defines the symbol compiles it.</summary>
    [Fact]
    public void A_line_that_only_some_builds_compile_is_detected_with_its_directives()
    {
        Assert.Equal([$"{Sample}:2 #if RELEASE", $"{Sample}:3 void M(string location) => System.IO.File.ReadAllBytes(location);", $"{Sample}:4 #endif"],
            FrameworkUseDetectors.BuildConditions(Sample, "class A {\n#if RELEASE\n    void M(string location) => System.IO.File.ReadAllBytes(location);\n#endif\n}"));
        Assert.Equal([$"{Sample}:2 #if DEBUG", $"{Sample}:3 void M() {{ }}", $"{Sample}:4 #else", $"{Sample}:6 #endif"],
            FrameworkUseDetectors.BuildConditions(Sample, "class A {\n#if DEBUG\n    void M() { }\n#else\n    void M(string location) => System.IO.File.Delete(location);\n#endif\n}"));
        Assert.Equal([$"{Sample}:1 #define MUSEUM", $"{Sample}:2 #undef MUSEUM"], FrameworkUseDetectors.BuildConditions(Sample, "#define MUSEUM\n#undef MUSEUM\nclass A { }"));
    }

    // The word of the directive is split so that this file does not match the rule on the warnings silenced.
    [Theory]
    [InlineData("#nullable disable\n#pra" + "gma warning disable 612, 618\nclass A { }\n#pra" + "gma warning restore 612, 618")]
    [InlineData("class A {\n    #region Members\n    // #if RELEASE\n    string M() => \"#if RELEASE\";\n    #endregion\n}")]
    public void A_directive_that_changes_no_line_compiled_is_no_condition_of_the_build(string sample) =>
        Assert.Empty(FrameworkUseDetectors.BuildConditions(Sample, sample));

    private const string HostProject = "src/Topogenia.Host/";

    private const string ExchangeProject = "src/Topogenia.Interchange/";

    /// <summary>
    /// The rule of the build on a tree of example (third review, defect 1). A build compiles a C# file wherever it
    /// stands in a project but under `bin` and `obj` at its root: under a folder of the same name deeper, under a
    /// folder of a tool, with its extension in capitals; and the web SDK compiles a page. Each is detected when no
    /// rule reads it. The output of a project, and what no build compiles, are left alone.
    /// </summary>
    [Fact]
    public void A_file_that_a_build_compiles_and_that_no_rule_reads_is_detected_wherever_it_stands()
    {
        string[] read = [HostProject + "Api/Documents/DocumentApi.cs", ExchangeProject + "Pmap/PmapFiles.cs", "src/Topogenia.Persistence.Sqlite/Migrations/Initial.Designer.cs"];
        string[] unread =
        [
            HostProject + "Api/obj/Visit.cs", HostProject + "Api/Documents/Visit.razor", ExchangeProject + "Pmap/node_modules/Visit.cs", ExchangeProject + "Pmap/bin/Visit.cs",
            HostProject + "Api/TestResults/Visit.cs", HostProject + "Api/.cache/Visit.cs", HostProject + "Pages/Visit.cshtml", HostProject + "Api/Visit.CS", HostProject + "Api/Visit.vb", HostProject + "Api/Visit.fs",
            HostProject + "wwwroot/Visit.cs", "src/obj/Visit.cs",
        ];
        string[] ignored =
        [
            HostProject + "obj/Debug/net10.0/Museum.AssemblyInfo.cs", HostProject + "bin/Debug/net10.0/Visit.cs", ExchangeProject + "obj/Visit.cs", HostProject + "wwwroot/assets/index.js",
            HostProject + "appsettings.json", HostProject + "Topogenia.Host.csproj", HostProject + "Api/Documents/Visit.cs.txt",
        ];
        Assert.Equal(unread, FrameworkUseDetectors.CompiledUnread([.. read, .. unread, .. ignored], read.ToHashSet(StringComparer.Ordinal)));
        Assert.Empty(FrameworkUseDetectors.CompiledUnread([.. read, .. unread, .. ignored], read.Concat(unread).ToHashSet(StringComparer.Ordinal)));
    }

    /// <summary>
    /// What the rules leave out of what they list. Under the folders of the projects, `bin` and `obj` at the root of a
    /// project and nothing else: a folder of that name deeper, or a folder of a tool, is read. Elsewhere (the
    /// interface, the documents), the folders of the tools at any depth, as before.
    /// </summary>
    [Theory]
    [InlineData("src/Topogenia.Host/obj/Debug/net10.0/Museum.AssemblyInfo.cs", true)]
    [InlineData("src/Topogenia.Host/bin/Visit.cs", true)]
    [InlineData("tests/Topogenia.Host.Tests/obj/Visit.cs", true)]
    [InlineData("src/Topogenia.Host/Api/obj/Visit.cs", false)]
    [InlineData("src/Topogenia.Interchange/Pmap/bin/Visit.cs", false)]
    [InlineData("src/Topogenia.Interchange/Pmap/node_modules/Visit.cs", false)]
    [InlineData("src/Topogenia.Host/Api/TestResults/Visit.cs", false)]
    [InlineData("src/Topogenia.Host/.vs/Visit.cs", false)]
    [InlineData("tests/Topogenia.Host.Tests/Security/obj/Visit.cs", false)]
    [InlineData("src/Topogenia.Host/obj", false)]
    [InlineData("src/obj/Visit.cs", false)]
    [InlineData("web/node_modules/museum/index.ts", true)]
    [InlineData("web/src/features/bin/visit.ts", true)]
    [InlineData("web/src/features/map/visit.ts", false)]
    [InlineData("docs/recette/visit.md", false)]
    public void Only_the_output_of_a_project_is_left_out_under_the_folders_that_a_build_compiles(string path, bool leftOut) =>
        Assert.Equal(leftOut, RepositoryFiles.IsLeftOut(path));

    /// <summary>The lines of an ignore file as the repository writes them: names of folders, an extension, a path from the root.</summary>
    private const string Ignore = "# Build output and tool caches\nbin/\nobj/\n.vs/\n*.user\nTestResults/\npublish/\nnode_modules/\nsrc/Topogenia.Host/wwwroot/\nweb/tsconfig.tsbuildinfo\n";

    /// <summary>
    /// A file that a build compiles in a folder that the repository does not keep: a reader of the repository never
    /// sees it, and the machine that builds ships it. Whatever the case of the folder, in the tests too. The output of
    /// a project, a file that no build compiles and a name that only looks like one are left alone.
    /// </summary>
    [Theory]
    [InlineData("src/Topogenia.Host/Api/obj/Visit.cs", "obj/")]
    [InlineData("src/Topogenia.Interchange/Pmap/bin/Visit.cs", "bin/")]
    [InlineData("src/Topogenia.Interchange/Pmap/node_modules/Visit.cs", "node_modules/")]
    [InlineData("src/Topogenia.Host/Api/OBJ/Visit.cs", "obj/")]
    [InlineData("src/Topogenia.Host/Api/publish/Visit.cs", "publish/")]
    [InlineData("src/Topogenia.Host/Api/.vs/Visit.razor", ".vs/")]
    [InlineData("src/Topogenia.Host/wwwroot/Visit.cs", "src/Topogenia.Host/wwwroot/")]
    [InlineData("tests/Topogenia.Host.Tests/Security/TestResults/Visit.cs", "TestResults/")]
    [InlineData("src/Topogenia.Host/Api/Documents/DocumentApi.cs", null)]
    [InlineData("src/Topogenia.Host/obj/Debug/net10.0/Museum.AssemblyInfo.cs", null)]
    [InlineData("src/Topogenia.Host/Api/obj/visit.json", null)]
    [InlineData("src/Topogenia.Host/Api/objects/Visit.cs", null)]
    [InlineData("src/Topogenia.Host/Api/bin.cs", null)]
    public void A_file_that_a_build_compiles_and_that_the_repository_does_not_keep_is_detected(string path, string? line)
    {
        string[] expected = line is null ? [] : [$"{path} ({line})"];
        Assert.Equal(expected, FrameworkUseDetectors.CompiledKeptOut([path], Ignore));
    }

    [Fact]
    public void A_line_of_the_ignore_file_that_the_rule_cannot_read_is_named()
    {
        Assert.Empty(FrameworkUseDetectors.UnreadIgnoreLines(Ignore));
        Assert.Equal(["!src/Topogenia.Host/Api/obj/", "**/obj/", "*museum/", "visit?.cs", "[Oo]bj/"],
            FrameworkUseDetectors.UnreadIgnoreLines("obj/\n!src/Topogenia.Host/Api/obj/\n**/obj/\n*museum/\nvisit?.cs\n[Oo]bj/\n*.log\n"));
    }

    private const string Settings = "src/Topogenia.Host/Topogenia.Host.csproj";

    /// <summary>A setting that adds a source, defines a symbol, reads another file of settings, or moves the output of a project or its default sources: by its element, in a target too, whatever its case.</summary>
    [Theory]
    [InlineData("<Project><ItemGroup><Compile Include=\"../Visit.cs\" /></ItemGroup></Project>", "Compile")]
    [InlineData("<Project><ItemGroup><compile Include=\"../Visit.cs\" /></ItemGroup></Project>", "compile")]
    [InlineData("<Project><Target Name=\"Visit\" BeforeTargets=\"CoreCompile\"><ItemGroup><Compile Include=\"obj/Visit.cs\" /></ItemGroup></Target></Project>", "Compile")]
    [InlineData("<Project><PropertyGroup><DefineConstants>MUSEUM</DefineConstants></PropertyGroup></Project>", "DefineConstants")]
    [InlineData("<Project><Import Project=\"Visit.props\" /></Project>", "Import")]
    [InlineData("<Project><PropertyGroup><BaseIntermediateOutputPath>../../artifacts/obj/</BaseIntermediateOutputPath></PropertyGroup></Project>", "BaseIntermediateOutputPath")]
    [InlineData("<Project><PropertyGroup><BaseOutputPath>../../artifacts/bin/</BaseOutputPath></PropertyGroup></Project>", "BaseOutputPath")]
    [InlineData("<Project><PropertyGroup><DefaultItemExcludes /></PropertyGroup></Project>", "DefaultItemExcludes")]
    [InlineData("<Project><PropertyGroup><DefaultLanguageSourceExtension>.txt</DefaultLanguageSourceExtension></PropertyGroup></Project>", "DefaultLanguageSourceExtension")]
    [InlineData("<Project><PropertyGroup><EnableDefaultItems>false</EnableDefaultItems></PropertyGroup></Project>", "EnableDefaultItems")]
    public void A_setting_that_changes_what_a_build_compiles_is_detected(string settings, string element) =>
        Assert.Equal([$"{Settings} {element}"], FrameworkUseDetectors.RefusedBuildSettings(Settings, settings));

    [Fact]
    public void The_settings_of_a_project_that_change_no_source_are_admitted() =>
        Assert.Empty(FrameworkUseDetectors.RefusedBuildSettings(Settings, "<!-- <Compile Include=\"Visit.cs\" /> --><Project Sdk=\"Microsoft.NET.Sdk.Web\"><PropertyGroup>"
            + "<AssemblyName>Museum</AssemblyName><PublishDir>../../artifacts/desktop/</PublishDir></PropertyGroup><ItemGroup><ProjectReference Include=\"../Visit/Visit.csproj\" />"
            + "<EmbeddedResource Include=\"Visit.json\" /><Content Update=\"web.config\" CopyToPublishDirectory=\"Never\" /></ItemGroup></Project>"));

    private const string Migrations = "class A { void M(Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder migrationBuilder) => ";

    /// <summary>What a file that a tool wrote may not use: SQL written by hand, however the call is spaced; a type asked for more than its name; a path; a type found by its name.</summary>
    [Theory]
    [InlineData(Migrations + "migrationBuilder .Sql(\"DELETE FROM Documents\"); }", "member MigrationBuilder.Sql")]
    [InlineData("class A { object M(object model) => model.GetType().Assembly; }", "member Type.Assembly")]
    [InlineData("class A { byte[] M(string location) => System.IO.File.ReadAllBytes(location); }", "opens ")]
    [InlineData("class A { object? M(string name) => System.Type.GetType(name); }", "late ")]
    public void What_a_file_that_a_tool_wrote_may_not_use_is_detected(string sample, string offence) =>
        Assert.Contains(FrameworkRulesTests.ToolOffences(Compiled(sample)), found => found.Contains(offence, StringComparison.Ordinal));

    [Theory]
    [InlineData("class A { System.Type M() => typeof(System.Guid); }")]
    [InlineData(Migrations + "migrationBuilder.DropTable(\"Documents\"); }")]
    public void A_table_changed_by_the_model_and_a_type_named_are_admitted_in_a_file_that_a_tool_wrote(string sample) =>
        Assert.Empty(FrameworkRulesTests.ToolOffences(Compiled(sample)));
}
