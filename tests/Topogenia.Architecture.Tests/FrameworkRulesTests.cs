// FrameworkRulesTests.cs
// The rules of the shipped code written as lists of what is admitted, not of what is refused (S-5.4-13, S-5.4-21, ADR 104): the namespaces it uses, what a file that reads a document may use, the calls by a name written in a text, the types of the processes and of native code, what the files written by a tool use, and the text that a build compiles.

using System.Text.RegularExpressions;

namespace Topogenia.Architecture.Tests;

public sealed class FrameworkRulesTests
{
    /// <summary>The shipped code outside the closed core, which has an allow list of its own (<see cref="DomainFrameworkDetectors"/>).</summary>
    private static List<SourceFile> Shipped => [.. SemanticSources.Production.Where(file => !file.Path.StartsWith(SemanticSources.DomainFolder, StringComparison.Ordinal))];

    private static List<SourceFile> Readers => [.. Shipped.Where(file => ShippedCodeDetectors.DocumentReads(file).Any())];

    /// <summary>A place without its line: a file edited elsewhere keeps the rule as it is.</summary>
    private static string Unlined(string place) => Regex.Replace(place, @":\d+ ", " ");

    /// <summary>
    /// The namespaces of the framework and of the libraries that the shipped code uses, exactly. A detector that names
    /// what opens a path, starts a process or reaches native code sees only what it names; this list sees the rest by
    /// its absence: an API of a namespace that is not here (the enumeration of a folder, an archive read from its file, a
    /// mail with a file attached, the helpers of another language of the framework) fails the rule in any file, whatever
    /// the API. A namespace enters on purpose, once what it opens has been looked at.
    /// </summary>
    internal static readonly string[] AdmittedNamespaces =
    [
        // The models the assistant calls (3.0): the SDK of each connector, in the project of the connectors only.
        "Amazon", "Amazon.BedrockRuntime", "Amazon.BedrockRuntime.Model", "Amazon.Runtime", "Amazon.Runtime.CredentialManagement", "Amazon.Runtime.Credentials",
        "Anthropic", "Anthropic.Core", "Anthropic.Exceptions", "Anthropic.Models.Messages", "Anthropic.Services",
        // The host: ASP.NET Core, its server, its authentication, its protection of data, its contract.
        "Microsoft.AspNetCore.Authentication", "Microsoft.AspNetCore.Authentication.Negotiate", "Microsoft.AspNetCore.Authorization", "Microsoft.AspNetCore.Builder",
        "Microsoft.AspNetCore.Connections", "Microsoft.AspNetCore.DataProtection", "Microsoft.AspNetCore.Hosting", "Microsoft.AspNetCore.Hosting.Server",
        "Microsoft.AspNetCore.Hosting.Server.Features", "Microsoft.AspNetCore.Http", "Microsoft.AspNetCore.Http.Features", "Microsoft.AspNetCore.Http.HttpResults",
        "Microsoft.AspNetCore.Http.Json", "Microsoft.AspNetCore.Http.Metadata", "Microsoft.AspNetCore.HttpsPolicy", "Microsoft.AspNetCore.Mvc",
        "Microsoft.AspNetCore.Mvc.ApiExplorer", "Microsoft.AspNetCore.OpenApi", "Microsoft.AspNetCore.Routing", "Microsoft.AspNetCore.Routing.Patterns",
        "Microsoft.AspNetCore.Server.Kestrel", "Microsoft.AspNetCore.Server.Kestrel.Core", "Microsoft.AspNetCore.StaticFiles", "Microsoft.OpenApi",
        // The persistence: EF Core and the two drivers.
        "Microsoft.Data.SqlClient", "Microsoft.Data.Sqlite", "Microsoft.EntityFrameworkCore", "Microsoft.EntityFrameworkCore.ChangeTracking",
        "Microsoft.EntityFrameworkCore.Diagnostics", "Microsoft.EntityFrameworkCore.Infrastructure", "Microsoft.EntityFrameworkCore.Metadata",
        "Microsoft.EntityFrameworkCore.Metadata.Builders", "Microsoft.EntityFrameworkCore.Metadata.Conventions", "Microsoft.EntityFrameworkCore.Migrations",
        "Microsoft.EntityFrameworkCore.Migrations.Operations", "Microsoft.EntityFrameworkCore.Migrations.Operations.Builders", "Microsoft.EntityFrameworkCore.Query",
        "Microsoft.EntityFrameworkCore.Query.SqlExpressions", "Microsoft.EntityFrameworkCore.SqlServer.Infrastructure.Internal",
        "Microsoft.EntityFrameworkCore.Sqlite.Infrastructure.Internal", "Microsoft.EntityFrameworkCore.Storage", "Microsoft.EntityFrameworkCore.Storage.ValueConversion",
        "SQLitePCL", "System.Data", "System.Data.Common",
        // The extensions of the host: settings, container, logs, options.
        "Microsoft.Extensions.Configuration", "Microsoft.Extensions.DependencyInjection", "Microsoft.Extensions.DependencyInjection.Extensions", "Microsoft.Extensions.Hosting",
        "Microsoft.Extensions.Logging", "Microsoft.Extensions.Options", "Microsoft.Extensions.Primitives",
        // The base of the framework: values, collections, texts, threads.
        "System", "System.Buffers.Binary", "System.Collections", "System.Collections.Concurrent", "System.Collections.Generic", "System.Collections.ObjectModel",
        "System.Globalization", "System.Linq", "System.Linq.Expressions", "System.Runtime.CompilerServices", "System.Runtime.Versioning", "System.Text",
        "System.Text.Encodings.Web", "System.Text.Json", "System.Text.Json.Nodes", "System.Text.Json.Serialization", "System.Text.Json.Serialization.Metadata",
        "System.Text.RegularExpressions", "System.Text.Unicode", "System.Threading", "System.Threading.Channels", "System.Threading.Tasks",
        // What reaches outside the process, each held by a rule of its own: the files (`SourceRulesTests`), the processes
        // and native code (below, `NativeCodeRulesTests`), the network (`Shipped_code_opens_no_outgoing_connection`),
        // the descriptions of types (below), the accounts of Windows and the directory of the server mode, XML read from a stream.
        "System.IO", "System.IO.Compression", "System.Diagnostics", "System.Runtime.InteropServices", "Microsoft.Win32.SafeHandles", "System.Net", "System.Net.Http",
        "System.Net.Http.Headers", "System.Net.Sockets", "System.Reflection", "System.Security", "System.Security.AccessControl", "System.Security.Claims",
        "System.Security.Cryptography", "System.Security.Principal", "System.DirectoryServices.AccountManagement", "System.Xml",
    ];

    /// <summary>
    /// S-5.4-13, S-5.4-21: the shipped code uses the namespaces of the list and no other, every type read (the names
    /// resolved and the types of the expressions), and the list holds no namespace that nothing uses any more. No type of
    /// the helpers of Visual Basic, which read files, start processes and make objects of COM under other names.
    /// </summary>
    [Fact]
    public void The_shipped_code_uses_the_listed_namespaces_of_the_framework_and_of_its_libraries_and_no_other()
    {
        var uses = Shipped.SelectMany(file => FrameworkUseDetectors.Namespaces(file).Select(space => (Space: space, file.Path))).ToList();
        var more = uses.Where(use => !AdmittedNamespaces.Contains(use.Space, StringComparer.Ordinal)).Select(use => $"{use.Space} in {use.Path}").Distinct(StringComparer.Ordinal).ToList();
        Assert.True(more.Count == 0, "A namespace that the list does not admit:" + Environment.NewLine + string.Join(Environment.NewLine, more));
        Assert.Empty(AdmittedNamespaces.Except(uses.Select(use => use.Space), StringComparer.Ordinal));
        Assert.Equal(AdmittedNamespaces.Length, AdmittedNamespaces.Distinct(StringComparer.Ordinal).Count());
        Assert.Empty(SemanticSources.Production.SelectMany(file => NativeCodeDetectors.NamespaceUses(file, "Microsoft.VisualBasic")));
    }

    /// <summary>The files outside the core that read a document or its location (<see cref="ShippedCodeDetectors.DocumentReads"/>), exactly.</summary>
    private static readonly string[] ReaderFiles =
    [
        "src/Topogenia.Application/Assistant/AssistantContext.cs", "src/Topogenia.Application/Documents/DocumentCommands.cs",
        "src/Topogenia.Application/Maps/MapViewBuilder.cs", "src/Topogenia.Application/OperationLog/ChangeSetJson.cs",
        "src/Topogenia.Host/Api/Documents/DocumentApi.cs", "src/Topogenia.Host/Api/Documents/DocumentFolderApi.cs", "src/Topogenia.Host/Endpoints/SheetEndpoints.cs",
        "src/Topogenia.Interchange/Packages/ContentEntries.cs", "src/Topogenia.Interchange/Packages/PackageBuild.Content.cs",
        "src/Topogenia.Interchange/Packages/PackageImporter.cs", "src/Topogenia.Interchange/Pmap/PmapBuild.Sheets.cs", "src/Topogenia.Interchange/Pmap/PmapCarry.cs",
        "src/Topogenia.Interchange/Pmap/PmapFiles.cs", "src/Topogenia.Interchange/Pmap/PmapModel.cs", "src/Topogenia.Interchange/Pmap/PmapOutline.cs",
        "src/Topogenia.Interchange/Pmap/PmapReader.Sheets.cs", "src/Topogenia.Interchange/Pmap/PmapWriter.cs", "src/Topogenia.Persistence/Loading/ProjectScope.cs",
        "src/Topogenia.Persistence/Model/ModelGuards.cs", "src/Topogenia.Persistence/Model/SheetConfigurations.cs",
    ];

    /// <summary>The namespaces every file that reads a document may use: values, collections, texts, the token and the task of a request.</summary>
    private static readonly string[] ReaderCommon =
    [
        "System", "System.Collections", "System.Collections.Generic", "System.Collections.ObjectModel", "System.Globalization", "System.Linq", "System.Text",
        "System.Threading", "System.Threading.Tasks",
    ];

    /// <summary>The namespaces that one of those files uses besides, file by file, exactly: its routes, its options, its tables, its JSON.</summary>
    private static readonly Dictionary<string, string[]> ReaderExtras = new(StringComparer.Ordinal)
    {
        ["src/Topogenia.Application/OperationLog/ChangeSetJson.cs"] =
            ["System.Reflection", "System.Text.Json", "System.Text.Json.Nodes", "System.Text.Json.Serialization", "System.Text.Json.Serialization.Metadata"],
        ["src/Topogenia.Host/Api/Documents/DocumentFolderApi.cs"] = ["Microsoft.Extensions.Options"],
        ["src/Topogenia.Host/Endpoints/SheetEndpoints.cs"] =
            ["Microsoft.AspNetCore.Builder", "Microsoft.AspNetCore.Http", "Microsoft.AspNetCore.Http.HttpResults", "Microsoft.AspNetCore.Mvc", "Microsoft.AspNetCore.Routing"],
        ["src/Topogenia.Interchange/Packages/ContentEntries.cs"] = ["System.Text.Json.Nodes"],
        ["src/Topogenia.Interchange/Packages/PackageBuild.Content.cs"] = ["System.Text.Json.Nodes"],
        ["src/Topogenia.Interchange/Packages/PackageImporter.cs"] = ["System.Text.Json.Nodes"],
        ["src/Topogenia.Interchange/Pmap/PmapReader.Sheets.cs"] = ["System.Text.Json"],
        ["src/Topogenia.Interchange/Pmap/PmapWriter.cs"] = ["System.IO", "System.Text.Encodings.Web", "System.Text.Json"],
        ["src/Topogenia.Persistence/Loading/ProjectScope.cs"] = ["Microsoft.EntityFrameworkCore", "System.Linq.Expressions"],
        ["src/Topogenia.Persistence/Model/ModelGuards.cs"] =
            ["Microsoft.EntityFrameworkCore", "Microsoft.EntityFrameworkCore.Metadata", "Microsoft.EntityFrameworkCore.Storage.ValueConversion", "System.Reflection"],
        ["src/Topogenia.Persistence/Model/SheetConfigurations.cs"] =
            ["Microsoft.EntityFrameworkCore", "Microsoft.EntityFrameworkCore.Metadata.Builders", "System.Linq.Expressions"],
    };

    /// <summary>
    /// In the namespaces admitted there that have doors, the types those files use, exactly. Of `System`: values, texts,
    /// errors and delegates, neither the environment of the process, nor its domain, nor the maker of objects by their
    /// type. Of the files: a stream in memory. Of the descriptions: a member and its name. Of the threads: the token.
    /// </summary>
    private static readonly Dictionary<string, string[]> ReaderTypes = new(StringComparer.Ordinal)
    {
        ["System"] =
        [
            "System.Action", "System.ArgumentException", "System.ArgumentNullException", "System.Boolean", "System.Byte", "System.Char", "System.DateOnly",
            "System.DateTime", "System.DateTimeKind", "System.Decimal", "System.Delegate", "System.Double", "System.Enum", "System.Exception", "System.Func",
            "System.Guid", "System.IFormatProvider", "System.Int32", "System.InvalidOperationException", "System.Nullable", "System.Object", "System.ReadOnlySpan",
            "System.String", "System.StringComparer", "System.StringComparison", "System.Type", "System.ValueTuple", "System.Void",
        ],
        ["System.IO"] = ["System.IO.MemoryStream", "System.IO.Stream"],
        ["System.Reflection"] =
            ["System.Reflection.ICustomAttributeProvider", "System.Reflection.MemberInfo", "System.Reflection.MethodInfo", "System.Reflection.PropertyInfo"],
        ["System.Linq.Expressions"] = ["System.Linq.Expressions.Expression"],
        ["System.Threading"] = ["System.Threading.CancellationToken"],
    };

    /// <summary>What those files do with the description of a type or of a member: read a name, ask whether a property may be written, compare two types.</summary>
    private static readonly string[] ReaderDescriptions = ["MemberInfo.Name", "PropertyInfo.SetMethod", "Type.IsAssignableFrom"];

    /// <summary>
    /// What a file that reads a document uses beyond what is admitted for it: a namespace, a type of a namespace that has
    /// doors, a member of the description of a type, a call by a name written in a text, a native function or another way
    /// to native code, a path opened or looked at.
    /// </summary>
    internal static IEnumerable<string> ReaderOffences(SourceFile file)
    {
        var extras = ReaderExtras.GetValueOrDefault(file.Path, []);
        var spaces = FrameworkUseDetectors.Namespaces(file).Where(space => !ReaderCommon.Contains(space, StringComparer.Ordinal) && !extras.Contains(space, StringComparer.Ordinal));
        var types = ReaderTypes.SelectMany(admitted => FrameworkUseDetectors.TypesIn(file, admitted.Key).Where(type => !admitted.Value.Contains(type, StringComparer.Ordinal)));
        var described = FrameworkUseDetectors.Members(file, "System.Type").Concat(FrameworkUseDetectors.Members(file, "System.Reflection"))
            .Where(member => !ReaderDescriptions.Contains(member, StringComparer.Ordinal));
        return [.. spaces.Select(space => $"{file.Path} namespace {space}"), .. types.Select(type => $"{file.Path} type {type}"),
            .. described.Select(member => $"{file.Path} member {member}"), .. FrameworkUseDetectors.LateBindings(file).Select(late => $"late {late}"),
            .. NativeCodeDetectors.NativeFunctions(file).Concat(NativeCodeDetectors.NativeDetours(file)).Select(native => $"native {native}"),
            .. ShippedCodeDetectors.FileSystemAccesses(file).Select(opened => $"opens {opened}")];
    }

    /// <summary>
    /// S-5.4-13: a file that reads the location of a document is held to a list of what is admitted, as the core is: the
    /// namespaces admitted for all of them and those of its own, the types of the namespaces that have doors, three
    /// members of the descriptions of types; no call by a name written in a text, nothing native, no path opened. One
    /// namespace, one type or one such member more in one of these files fails here, whatever it is: the location cannot
    /// be handed, in the file that reads it, to an API that no detector names. The files are listed: a file more that
    /// reads a document enters on purpose. And each list is exact: nothing admitted that no file uses.
    /// </summary>
    [Fact]
    public void A_file_that_reads_a_document_uses_what_is_admitted_for_it_and_nothing_else()
    {
        var readers = Readers;
        Assert.Equal(ReaderFiles.Order(StringComparer.Ordinal), readers.Select(file => file.Path).Order(StringComparer.Ordinal));
        var offences = readers.SelectMany(ReaderOffences).ToList();
        Assert.True(offences.Count == 0, "A file that reads a document uses more than what is admitted:" + Environment.NewLine + string.Join(Environment.NewLine, offences));
        Assert.Equal(ReaderCommon.Order(StringComparer.Ordinal), readers.SelectMany(FrameworkUseDetectors.Namespaces).Intersect(ReaderCommon, StringComparer.Ordinal).Order(StringComparer.Ordinal));
        Assert.All(ReaderExtras, extra => Assert.Equal(extra.Value.Order(StringComparer.Ordinal),
            FrameworkUseDetectors.Namespaces(readers.Single(file => file.Path == extra.Key)).Except(ReaderCommon, StringComparer.Ordinal).Order(StringComparer.Ordinal)));
        Assert.All(ReaderTypes, admitted => Assert.Equal(admitted.Value.Order(StringComparer.Ordinal),
            readers.SelectMany(file => FrameworkUseDetectors.TypesIn(file, admitted.Key)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)));
        Assert.Equal(ReaderDescriptions, readers.SelectMany(file => FrameworkUseDetectors.Members(file, "System.Type").Concat(FrameworkUseDetectors.Members(file, "System.Reflection")))
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
    }

    private const string Json = "src/Topogenia.Host/Json/";

    /// <summary>
    /// The calls of the shipped code by a name written in a text or held in a value (<see cref="FrameworkUseDetectors.LateBindings"/>),
    /// each one, file by file. None is given a text that comes from a request or from a document: each calls a member of
    /// the solution found by a name that the file itself writes.
    /// </summary>
    private static readonly string[] LateBindings =
    [
        // The handlers of the routes: the classes of `Api/` of the host whose name ends with `Api`, given to the container.
        "src/Topogenia.Host/Composition/HostServices.cs Assembly.GetTypes",
        // A JSON converter of the host made for the type that the serializer asks (an enumeration, a list, an optional value).
        Json + "CodeEnumConverterFactory.cs Activator", Json + "CodeEnumConverterFactory.cs Activator.CreateInstance",
        Json + "NonNullItemsConverterFactory.cs Activator", Json + "NonNullItemsConverterFactory.cs Activator.CreateInstance",
        Json + "Optional.cs Activator", Json + "Optional.cs Activator.CreateInstance",
        // A generic method of the file itself, made for an enumeration and called: its codes, for the schema of the contract.
        Json + "CodeEnumSchemaTransformer.cs MethodBase.Invoke",
        // The copy that a record of the model carries, called to track the record; a generic read of the file itself, made for a type of record.
        "src/Topogenia.Persistence/Writing/ChangeSetWriter.cs MethodBase.Invoke", "src/Topogenia.Persistence/Writing/UnversionedCheck.cs MethodBase.Invoke",
    ];

    /// <summary>
    /// S-5.4-13, S-5.4-21: « one process started » and « no other way to native code nor to COM » are read on the resolved
    /// symbols, which a call by a name written in a text escapes: a type found by its name, a method invoked through its
    /// description, an object made by the maker of objects, an assembly loaded or run, an expression compiled at the run,
    /// code written at the run. Such calls exist in the files of the list and nowhere else, each occurrence counted; and
    /// no file of the list reads a document.
    /// </summary>
    [Fact]
    public void The_shipped_code_calls_by_a_name_written_in_a_text_only_where_listed()
    {
        Assert.Equal(LateBindings.Order(StringComparer.Ordinal), SemanticSources.Production.SelectMany(FrameworkUseDetectors.LateBindings).Select(Unlined).Order(StringComparer.Ordinal));
        Assert.Empty(LateBindings.Select(late => late.Split(' ')[0]).Intersect(ReaderFiles, StringComparer.Ordinal));
    }

    private const string Browser = "src/Topogenia.Host/Startup/StartupService.cs";

    /// <summary>
    /// S-5.4-21: of the namespace of the processes the shipped code uses three types and no other: the process and what
    /// describes one to start, named by the one file that opens the browser, where the process is started and nothing else
    /// is done with it; and a stopwatch. A type of that namespace more, or a second file that names a process in any way
    /// (its type is enough: a method found by its name on it), fails here.
    /// </summary>
    [Fact]
    public void A_process_is_named_by_the_file_that_opens_the_browser_and_by_no_other()
    {
        const string stopwatch = "System.Diagnostics.Stopwatch";
        Assert.Equal(["System.Diagnostics.Process", "System.Diagnostics.ProcessStartInfo", stopwatch],
            Shipped.SelectMany(file => FrameworkUseDetectors.TypesIn(file, "System.Diagnostics")).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
        var naming = Shipped.Where(file => FrameworkUseDetectors.TypesIn(file, "System.Diagnostics").Any(type => type != stopwatch)).ToList();
        Assert.Equal([Browser], naming.Select(file => file.Path));
        Assert.Equal(["Process.Start", "ProcessStartInfo.UseShellExecute"], FrameworkUseDetectors.Members(naming[0], "System.Diagnostics").Order(StringComparer.Ordinal));
    }

    private const string Interop = "System.Runtime.InteropServices";

    /// <summary>
    /// S-5.4-13, S-5.4-21: of the namespace of native code the shipped code uses the types below and no other: what
    /// declares a function and a structure of Windows (`NativeCodeRulesTests` lists the functions), the error of the
    /// directory of the server mode, and the helper of native memory, whose members are listed file by file: the size of
    /// a structure, the error of a call, a block of memory read as an address. A member more (a delegate made from an
    /// address, a structure read at an address) fails here, whatever its name.
    /// </summary>
    [Fact]
    public void The_shipped_code_uses_the_listed_types_of_native_code_and_the_listed_members_of_its_memory()
    {
        Assert.Equal(["COMException", "CallingConvention", "CharSet", "DefaultDllImportSearchPathsAttribute", "DllImportAttribute", "DllImportSearchPath", "LayoutKind", "Marshal",
            "MarshalAsAttribute", "OutAttribute", "StructLayoutAttribute", "UnmanagedFunctionPointerAttribute", "UnmanagedType"],
            Shipped.SelectMany(file => FrameworkUseDetectors.TypesIn(file, Interop)).Distinct(StringComparer.Ordinal).Select(type => type[(Interop.Length + 1)..]).Order(StringComparer.Ordinal));
        var memory = Shipped.SelectMany(file => FrameworkUseDetectors.Members(file, Interop + ".Marshal").Select(member => $"{file.Path} {member}")).Order(StringComparer.Ordinal);
        Assert.Equal(["src/Topogenia.Host/Desktop/DesktopTray.cs Marshal.SizeOf", "src/Topogenia.Host/Desktop/ShellFolderOpener.cs Marshal.GetLastPInvokeError",
            "src/Topogenia.Host/Desktop/ShellFolderOpener.cs Marshal.SizeOf", "src/Topogenia.Host/Desktop/WindowsConnectionOwners.cs Marshal.AllocHGlobal",
            "src/Topogenia.Host/Desktop/WindowsConnectionOwners.cs Marshal.FreeHGlobal", "src/Topogenia.Host/Desktop/WindowsConnectionOwners.cs Marshal.ReadIntPtr"], memory);
    }

    /// <summary>
    /// The settings of the build that the shipped code follows: its projects, what MSBuild reads beside a project when
    /// it is there (`.props`, `.targets`, the file of a user), the profiles of the publication, and the files at the
    /// root that every project reads.
    /// </summary>
    private static List<string> BuildSettings =>
        [.. RepositoryFiles.Enumerate(["src"], ".csproj", ".props", ".targets", ".pubxml", ".user").Concat(RepositoryFiles.Shallow([""], ".props", ".targets"))
            .Select(RepositoryFiles.Relative)];

    /// <summary>
    /// The rules read the C# files of `src` as they are written, with no symbol of any build, and the tests are read the
    /// same way. What a build compiles must therefore be that text and no other. No line kept or left out by a symbol of
    /// the build (`#if`) in any C# file of `src` or of `tests`, written by hand or by a tool, since such a line would be
    /// compiled by the publication, or by the build of the tests, and read by no rule. No file that a build could
    /// compile outside those the rules read: every file of `src`, whatever its folder, but `bin` and `obj` at the root
    /// of a project, is listed without any filter, and each C# file among them is one that <see cref="SemanticSources.Production"/>
    /// or <see cref="SemanticSources.ToolWritten"/> compiles; no page nor component that the web SDK would turn into
    /// C# (`.razor`, `.cshtml`), no file of another language; the same for `tests`. A C# file under a folder named
    /// `obj`, `bin` or `node_modules` below the root of its project is compiled by the build: the rules read it like
    /// any other, and this rule refuses it where it stands, since `.gitignore` keeps such a folder out of the
    /// repository: the machine that builds would compile and ship a file that no one who reads the repository sees. A
    /// C# file in the folder of the pages of the host is not compiled (the web SDK takes `wwwroot` at the root of the
    /// project out of the compilation) but published beside the pages, and that folder is kept out of the repository
    /// too: it is refused here, and by the rule of the files that a tool wrote, which reads it. And no project of the
    /// shipped code, nor the settings they share, nor a profile of the publication, adds a source file, leaves one
    /// out, defines a symbol, reads another file of settings, or moves the output of a project or the sources it
    /// takes by default.
    /// </summary>
    [Fact]
    public void A_build_compiles_the_text_that_the_rules_read_and_no_other()
    {
        var sources = RepositoryFiles.Enumerate(["src", "tests"], ".cs").ToList();
        Assert.Contains(sources, file => RepositoryFiles.IsGenerated(file));
        Assert.Empty(sources.SelectMany(file => FrameworkUseDetectors.BuildConditions(RepositoryFiles.Relative(file), File.ReadAllText(file))));
        var shipped = SemanticSources.Production.Concat(SemanticSources.ToolWritten).Select(file => file.Path).ToHashSet(StringComparer.Ordinal);
        var found = RepositoryFiles.Unfiltered("src").ToList();
        var tests = RepositoryFiles.Unfiltered("tests").ToList();
        Assert.Contains(SourceRulesTests.FolderShell, found);
        // The pages of the host are listed where the interface was built; a repository just cloned has none yet.
        var pages = Path.Combine(RepositoryFiles.Root, "src", "Topogenia.Host", "wwwroot");
        if (Directory.Exists(pages) && Directory.EnumerateFiles(pages, "*", SearchOption.AllDirectories).Any())
            Assert.Contains(found, file => file.StartsWith("src/Topogenia.Host/wwwroot/", StringComparison.Ordinal));
        Assert.Empty(FrameworkUseDetectors.CompiledUnread(found, shipped));
        Assert.Empty(shipped.Except(found, StringComparer.Ordinal));
        Assert.Empty(FrameworkUseDetectors.CompiledUnread(tests, sources.Select(RepositoryFiles.Relative).ToHashSet(StringComparer.Ordinal)));
        var ignored = File.ReadAllText(Path.Combine(RepositoryFiles.Root, ".gitignore"));
        Assert.Empty(FrameworkUseDetectors.UnreadIgnoreLines(ignored));
        Assert.Empty(FrameworkUseDetectors.CompiledKeptOut(found.Concat(tests), ignored));
        var settings = BuildSettings;
        Assert.Contains("Directory.Build.props", settings);
        Assert.Contains("src/Topogenia.Host/Topogenia.Host.csproj", settings);
        Assert.Contains("src/Topogenia.Host/Properties/PublishProfiles/Desktop.pubxml", settings);
        Assert.Empty(settings.SelectMany(file => FrameworkUseDetectors.RefusedBuildSettings(file, File.ReadAllText(Path.Combine(RepositoryFiles.Root, file)))));
    }

    /// <summary>
    /// What a file that a tool wrote uses beyond the lists below: a member of the description of a type or of an
    /// expression, SQL written by hand, a call by a name written in a text, a path opened, anything native.
    /// </summary>
    internal static IEnumerable<string> ToolOffences(SourceFile file) =>
    [
        .. FrameworkUseDetectors.Members(file, "System.Type").Concat(FrameworkUseDetectors.Members(file, "System.Linq.Expressions"))
            .Concat(FrameworkUseDetectors.Members(file, "Microsoft.EntityFrameworkCore.Migrations").Where(member => member == "MigrationBuilder.Sql"))
            .Select(member => $"{file.Path} member {member}"),
        .. FrameworkUseDetectors.LateBindings(file).Select(late => $"late {late}"), .. ShippedCodeDetectors.FileSystemAccesses(file).Select(opened => $"opens {opened}"),
        .. NativeCodeDetectors.NativeFunctions(file).Concat(NativeCodeDetectors.NativeDetours(file)).Select(native => $"native {native}"),
    ];

    /// <summary>
    /// S-5.4-13, S-5.4-21: the files of `src` that a tool wrote are spared the rules of the form, and the other rules do
    /// not read them (<see cref="SemanticSources.Production"/>); the build compiles and ships them all the same. They
    /// are files of the migrations of the two providers and nothing else (a C# file put by hand in the folder of the
    /// pages of the host, which the build does not compile but the publication copies, fails here: it is no
    /// migration), and they use the model of the base and the
    /// values it holds, exactly: the namespaces below and, of `System`, the types below; a type named by `typeof` and
    /// nothing asked of it, no SQL written by hand, no call by a name, no path, nothing native. A line added by hand to
    /// such a file that uses anything else fails here.
    /// </summary>
    [Fact]
    public void The_files_that_a_tool_wrote_are_migrations_and_use_the_model_of_the_base_and_nothing_else()
    {
        var written = SemanticSources.ToolWritten;
        Assert.NotEmpty(written);
        Assert.All(written, file => Assert.Matches(@"^src/Topogenia\.Persistence\.(SqlServer|Sqlite)/Migrations/[^/]+\.cs$", file.Path));
        Assert.Empty(written.Select(file => file.Path).Intersect(SemanticSources.Production.Select(file => file.Path), StringComparer.Ordinal));
        Assert.Equal(["Microsoft.EntityFrameworkCore", "Microsoft.EntityFrameworkCore.Infrastructure", "Microsoft.EntityFrameworkCore.Metadata.Builders",
            "Microsoft.EntityFrameworkCore.Migrations", "Microsoft.EntityFrameworkCore.Migrations.Operations", "Microsoft.EntityFrameworkCore.Migrations.Operations.Builders",
            "System", "System.Collections.Generic", "System.Linq.Expressions"],
            written.SelectMany(FrameworkUseDetectors.Namespaces).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
        Assert.Equal(["System.Action", "System.Boolean", "System.DateOnly", "System.DateTime", "System.Decimal", "System.Double", "System.Func", "System.Guid", "System.Int32",
            "System.Nullable", "System.Object", "System.String", "System.Type", "System.Void"],
            written.SelectMany(file => FrameworkUseDetectors.TypesIn(file, "System")).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
        Assert.Empty(written.SelectMany(ToolOffences));
    }
}
