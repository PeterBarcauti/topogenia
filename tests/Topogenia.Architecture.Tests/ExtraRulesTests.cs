// ExtraRulesTests.cs
// Further structural rules: SQL scripts, report views, HTTP entry points, test case length, test project settings.

using System.Text.RegularExpressions;

namespace Topogenia.Architecture.Tests;

public sealed class ExtraRulesTests
{
    [Fact]
    public void Sql_scripts_live_only_in_the_two_providers()
    {
        var offenders = RepositoryFiles.Enumerate(["src", "tests", "web", "build"], ".sql")
            .Select(RepositoryFiles.Relative)
            .Where(relative => !relative.StartsWith("src/Topogenia.Persistence.SqlServer/", StringComparison.Ordinal)
                && !relative.StartsWith("src/Topogenia.Persistence.Sqlite/", StringComparison.Ordinal))
            .ToList();
        Assert.Empty(offenders);
    }

    [Fact]
    public void Views_exist_only_in_the_bi_schema_of_the_sql_server_provider()
    {
        var offenders = RepositoryFiles.Enumerate(["src"], ".cs", ".sql")
            .Where(file => SourceDetectors.CreatesViewOutsideBi(File.ReadAllText(file)))
            .Select(RepositoryFiles.Relative)
            .ToList();
        Assert.Empty(offenders);
        var sqliteViews = RepositoryFiles.Enumerate(["src/Topogenia.Persistence.Sqlite"], ".cs", ".sql")
            .Where(file => Regex.IsMatch(File.ReadAllText(file), @"\bCREATE\s+VIEW\b", RegexOptions.IgnoreCase))
            .Select(RepositoryFiles.Relative);
        Assert.Empty(sqliteViews);
    }

    private static bool IsEntryPoint(string sample, string path = "src/Topogenia.Host/Composition/Leak.cs") =>
        RouteDetectors.Misplaced([SemanticSources.Sample(EntryPointUsings + sample, path)]).Count > 0;

    private const string EntryPointUsings =
        "using Microsoft.AspNetCore.Builder; using Microsoft.AspNetCore.Http; using Microsoft.AspNetCore.Routing; using Microsoft.AspNetCore.Mvc;\n";

    /// <summary>Each form of the constats of the gate P4 (round 2, A1) and of the round 1, whatever the receiver or the address.</summary>
    [Theory]
    [InlineData("class A { void M(WebApplication application) => application.MapGet(\"/api/leak\", () => \"leak\"); }")]
    [InlineData("class A { void M(IEndpointRouteBuilder steps) => steps.MapPost(\"/x\", () => 1); }")]
    [InlineData("class A { void M(IEndpointRouteBuilder anything) => anything.MapFallbackToFile(\"index.html\"); }")]
    [InlineData("class A { object M(IEndpointRouteBuilder routes) => routes.MapGroup(\"/api\"); }")]
    [InlineData("class A { void M(IEndpointRouteBuilder routes, RequestDelegate refuse) => routes.Map(\"/api/{**rest}\", refuse); }")]
    [InlineData("class A { const string Leak = \"/leak\"; void M(WebApplication application, RequestDelegate answer) => application.Map(Leak, answer); }")]
    [InlineData("class A { void M(WebApplication application, string address, RequestDelegate answer) => application.Map(address, answer); }")]
    [InlineData("class A { void M(WebApplication application, RequestDelegate answer) => application.Map(@\"/leak\", answer); }")]
    [InlineData("class A { void M(WebApplication application, int n, RequestDelegate answer) => application.Map($@\"/leak/{n}\", answer); }")]
    [InlineData("class A { void M(IApplicationBuilder application) => application.MapWhen(context => context.Request.Path == \"/leak\", branch => { }); }")]
    [InlineData("class A { void M(IApplicationBuilder branch) => branch.Run(context => context.Response.WriteAsync(\"leak\")); }")]
    [InlineData("class A { void M(IApplicationBuilder application) => application.UseWhen(context => true, branch => { }); }")]
    [InlineData("class A { void M(IApplicationBuilder application) => application.Map(\"/leak\", branch => { }); }")]
    [InlineData("class A { void M(IEndpointRouteBuilder routes) => EndpointRouteBuilderExtensions.MapGet(routes, \"/leak\", () => 1); }")]
    [InlineData("using Routes = Microsoft.AspNetCore.Builder.EndpointRouteBuilderExtensions; class A { void M(IEndpointRouteBuilder routes) => Routes.MapDelete(routes, \"/leak\", () => 1); }")]
    [InlineData("class A { void M(IEndpointRouteBuilder routes) => routes.MapControllers(); }")]
    [InlineData("class A { void M(IEndpointRouteBuilder routes, EndpointDataSource source) => routes.DataSources.Add(source); }")]
    [InlineData("public sealed class StepsController : ControllerBase { }")]
    [InlineData("[ApiController] public class A { }")]
    [InlineData("class A { [HttpPost] public void M() { } }")]
    [InlineData("class A { [Route(\"/leak\")] public void M() { } }")]
    public void The_entry_point_detector_finds_a_route_a_branch_or_a_controller_whatever_its_form(string sample) => Assert.True(IsEntryPoint(sample));

    [Theory]
    [InlineData("class A { void M(Topogenia.Domain.Workspace.ProjectWorkspace workspace, Guid id) => workspace.Map(id); }")]
    [InlineData("class Workspace { public int Map(string id) => 1; public void Run() { } } class A { int M(Workspace workspace) { workspace.Run(); return workspace.Map(\"/x\"); } }")]
    [InlineData("static class Lookalike { public static void MapGet(this string routes, string address) { } } class A { void M() => \"routes\".MapGet(\"/x\"); }")]
    [InlineData("class A { void M(WebApplication application) => application.UseMiddleware<A>(); }")]
    [InlineData("class A { System.Threading.Tasks.Task M(WebApplication application) => application.RunAsync(); }")]
    [InlineData("class A { void M(WebApplication application) => application.Run(); }")]
    [InlineData("class A { } // application.MapGet(\"/x\", () => 1) in a comment")]
    public void The_entry_point_detector_leaves_the_model_look_alike_names_the_middlewares_and_the_start(string sample) => Assert.False(IsEntryPoint(sample));

    /// <summary>Porte P4 round 3 (A1): the file of the pipeline, or a file named after it, is no allowed place for a route.</summary>
    [Theory]
    [InlineData("src/Topogenia.Host/Composition/HostPipeline.cs")]
    [InlineData("src/Topogenia.Host/Composition/HostPipelineLeak.cs")]
    [InlineData("src/Topogenia.Host/Composition/HostPipeline/Leak.cs")]
    public void A_route_in_the_file_of_the_pipeline_or_in_a_file_named_after_it_is_misplaced(string path)
    {
        Assert.True(IsEntryPoint("class A { const string Leak = \"/leak\"; void M(WebApplication application, RequestDelegate answer) => application.Map(Leak, answer); }", path));
        Assert.True(IsEntryPoint("class A { void M(WebApplication application) => application.MapGet(\"/api/leak\", () => \"leak\"); }", path));
    }

    /// <summary>The hand-over of the pipeline to the routes (a method of the Endpoints namespace) declares none, wherever it is called.</summary>
    [Fact]
    public void A_call_to_a_method_of_the_endpoints_namespace_is_no_misplaced_route()
    {
        const string handOver = "namespace Topogenia.Host.Endpoints { public static class Routes { public static void MapEverything(this IEndpointRouteBuilder routes) { } } }\n"
            + "class A { void M(WebApplication application) => Topogenia.Host.Endpoints.Routes.MapEverything(application); }";
        Assert.False(IsEntryPoint(handOver, "src/Topogenia.Host/Composition/HostPipeline.cs"));
        Assert.True(IsEntryPoint(handOver.Replace("namespace Topogenia.Host.Endpoints", "namespace Topogenia.Host.Composition", StringComparison.Ordinal)
            .Replace("Topogenia.Host.Endpoints.Routes", "Topogenia.Host.Composition.Routes", StringComparison.Ordinal), "src/Topogenia.Host/Composition/HostPipeline.cs"));
    }

    /// <summary>
    /// Spec 3.2, ADR 043: HTTP entry points (routes, branches of the pipeline, controllers) are declared only in the
    /// Endpoints folder of the host, where the dependency rule of
    /// <c>DependencyRulesTests.Http_entry_points_depend_on_handlers_only</c> applies to them; the order of the pipeline
    /// (<c>Composition/HostPipeline</c>) only hands the requests to them by a method of that folder, and is not exempted.
    /// Read on the resolved symbols of every production project.
    /// </summary>
    [Fact]
    public void Http_entry_points_are_declared_only_in_the_endpoints_folder() =>
        Assert.Empty(RouteDetectors.Misplaced(SemanticSources.Production));

    [Fact]
    public void No_typescript_test_case_exceeds_80_lines()
    {
        var offenders = RepositoryFiles.Enumerate(["web/src", "web/e2e", "web/scripts"], ".ts", ".tsx")
            .Where(file => file.Contains(".test.", StringComparison.Ordinal) || RepositoryFiles.Relative(file).StartsWith("web/e2e/", StringComparison.Ordinal))
            .SelectMany(file => SourceDetectors.LongTestCases(File.ReadAllText(file))
                .Select(test => $"{RepositoryFiles.Relative(file)}:{test.Line} ({test.Lines} lines)"))
            .ToList();
        Assert.Empty(offenders);
    }

    [Fact]
    public void A_test_project_with_tests_does_not_ignore_an_empty_run()
    {
        var offenders = Directory.EnumerateDirectories(Path.Combine(RepositoryFiles.Root, "tests"))
            .Where(folder => File.ReadAllText(Directory.EnumerateFiles(folder, "*.csproj").Single()).Contains("--ignore-exit-code 8", StringComparison.Ordinal))
            .Where(folder => RepositoryFiles.Enumerate([RepositoryFiles.Relative(folder)], ".cs")
                .Any(file => Regex.IsMatch(File.ReadAllText(file), @"\[(Fact|Theory)\b")))
            .Select(RepositoryFiles.Relative)
            .ToList();
        Assert.Empty(offenders);
    }

    [Fact]
    public void Configuration_files_name_no_external_address()
    {
        var offenders = RepositoryFiles.Enumerate(["src"], ".json")
            .Where(file => Path.GetFileName(file).StartsWith("appsettings", StringComparison.Ordinal))
            .Where(file => SourceDetectors.ContainsExternalUrl(File.ReadAllText(file)))
            .Select(RepositoryFiles.Relative)
            .ToList();
        Assert.Empty(offenders);
    }

    /// <summary>
    /// The files of the tests that build the host of the product, exactly (<see cref="TestHostDetectors.Hosts"/>): a test
    /// file more that builds one enters this list on purpose, with a shell that records.
    /// </summary>
    private static readonly string[] TestHostFiles =
    [
        // The host in memory of the tests of the API, in desktop mode on SQLite: its recorder is `ApiHost.Folders`.
        "tests/Topogenia.Host.Tests/Infrastructure/ApiHost.cs",
        // The real run of the desktop mode, on Kestrel and the loopback.
        "tests/Topogenia.Host.Tests/Desktop/DesktopRun.cs",
        // The starts of the desktop mode on the settings of a test: the host built bare, the executable run as a person
        // runs it, with a console or without; several are refused before they listen, none is left to chance.
        "tests/Topogenia.Host.Tests/Desktop/DesktopStartupTests.cs", "tests/Topogenia.Host.Tests/Desktop/DesktopWindowedTests.cs",
        // The commands of the command line, which never listen (the server is not started), and the starts around them
        // that do: the data file created as by a first start, a second start on a file held.
        "tests/Topogenia.Host.Tests/Startup/ExchangeCommandsTests.cs", "tests/Topogenia.Host.Tests/Startup/HostCommandsTests.cs",
        // The real runs of the server mode, where the product registers no shell at all: given a recorder all the same,
        // since the settings a test lays over the run may name another mode.
        "tests/Topogenia.Host.Tests/Server/ServerRun.cs", "tests/Topogenia.Host.Tests/Server/ServerVariantRun.cs",
        // The host of the journeys of the browser, and the migration it runs first in server mode: `JourneyFolders`.
        "tests/Topogenia.EndToEnd.Host/EndToEndHost.cs",
    ];

    /// <summary>
    /// ADR 104: no test and no journey opens a real Explorer. Every place of a test file that builds the host of the
    /// product gives it a shell that records in the place of the one of Windows, whatever its mode and whether it comes
    /// to listen or not, on the exact list of the files that build one: a file more, or a place more in a listed file,
    /// that builds a host without a recorder fails here, and so does a host built in a way the rule cannot read. The
    /// host in memory replaces the shell the product registered, as the host of the journeys does through its services.
    /// The class of the product that calls the shell of Windows is named by its own tests only, and no test nor journey
    /// builds it, under any writing that the syntax lets read (<see cref="TestHostDetectors.ShellBuilt"/>): its tests
    /// read its constants and its two functions that reach nothing, and without an object of the class its `Show`
    /// cannot be called. The look of Windows is another class of the product, which does not carry the shell: who holds
    /// the look, in a test as in a host, cannot reach the shell by a cast. No test names the function of the shell nor
    /// the program of the Explorer.
    /// </summary>
    [Fact]
    public void No_test_nor_journey_asks_the_shell_of_windows_to_show_a_folder()
    {
        static string Text(string relative) => File.ReadAllText(Path.Combine(RepositoryFiles.Root, relative));
        var tests = RepositoryFiles.Enumerate(["tests"], ".cs").Select(RepositoryFiles.Relative)
            .Where(file => !file.StartsWith("tests/Topogenia.Architecture.Tests/", StringComparison.Ordinal)).ToList();
        var building = tests.Select(file => (File: file, Hosts: TestHostDetectors.Hosts(Text(file)))).Where(found => found.Hosts.Count > 0).ToList();
        Assert.Equal(TestHostFiles.Order(StringComparer.Ordinal), building.Select(found => found.File));
        var real = building.SelectMany(found => found.Hosts.Where(host => !host.Recorded).Select(host => $"{found.File}:{host.Line} {host.How}")).ToList();
        Assert.True(real.Count == 0, "A host of the tests holds the shell of Windows:" + Environment.NewLine + string.Join(Environment.NewLine, real));
        Assert.Contains("services.RemoveAll<IFolderShell>();\n                    services.AddSingleton<IFolderShell>(folders);",
            Text("tests/Topogenia.Host.Tests/Infrastructure/ApiHost.cs"), StringComparison.Ordinal);
        Assert.Contains("services.RemoveAll<IFolderShell>();\n            services.AddSingleton<IFolderShell>(new JourneyFolders(runFolder));",
            Text("tests/Topogenia.EndToEnd.Host/FictitiousAccount.cs"), StringComparison.Ordinal);
        const string own = "tests/Topogenia.Host.Tests/Desktop/ShellFolderOpenerTests.cs";
        Assert.Equal([own], tests.Where(file => Text(file).Contains("ShellFolderOpener", StringComparison.Ordinal)));
        Assert.DoesNotContain(".Show(", Text(own), StringComparison.Ordinal);
        Assert.DoesNotContain(tests, file => Regex.IsMatch(Text(file), @"\bShellExecute|explorer\.exe", RegexOptions.IgnoreCase));
        var built = tests.SelectMany(file => TestHostDetectors.ShellBuilt(Text(file)).Select(place => $"{file}:{place}")).ToList();
        Assert.True(built.Count == 0, "A test names the class of the shell otherwise than to read its static members:" + Environment.NewLine + string.Join(Environment.NewLine, built));
        Assert.Contains("ShellFolderOpener.ClassOf(", Text(own), StringComparison.Ordinal);
    }

    /// <summary>Each writing by which a test would hold an object of the class of the shell, or the means to make one.</summary>
    [Theory]
    [InlineData("var shell = new ShellFolderOpener();")]
    [InlineData("ShellFolderOpener shell = new();")]
    [InlineData("IFolderShell shell = new Topogenia.Host.Desktop.ShellFolderOpener();")]
    [InlineData("var opener = new CheckedFolderOpener(windows, new ShellFolderOpener(), FolderOpeningOptions.Default);")]
    [InlineData("services.AddSingleton<IFolderShell, ShellFolderOpener>();")]
    [InlineData("var shell = ActivatorUtilities.CreateInstance<ShellFolderOpener>(services);")]
    [InlineData("var shell = Activator.CreateInstance(typeof(ShellFolderOpener));")]
    [InlineData("var shell = Activator.CreateInstance(Type.GetType(\"Topogenia.Host.Desktop.ShellFolderOpener, Topogenia\")!);")]
    [InlineData("IFolderShell Shell(ShellFolderOpener shell) => shell;")]
    [InlineData("var show = typeof(Topogenia.Host.Desktop.ShellFolderOpener).GetMethod(\"Show\");")]
    public void A_test_that_builds_the_class_of_the_shell_is_detected_under_any_writing(string statement) =>
        Assert.Single(TestHostDetectors.ShellBuilt($"using Topogenia.Host.Desktop;\nclass T {{ void M(IServiceCollection services, IFolderDisk windows) {{ {statement} }} }}"));

    [Fact]
    public void An_alias_or_a_field_of_the_class_of_the_shell_is_detected_and_its_static_members_are_read_freely()
    {
        Assert.Single(TestHostDetectors.ShellBuilt("using Shell = Topogenia.Host.Desktop.ShellFolderOpener;\nclass T { }"));
        Assert.Single(TestHostDetectors.ShellBuilt("using Topogenia.Host.Desktop;\nclass T { private readonly ShellFolderOpener shell = new(); }"));
        Assert.Empty(TestHostDetectors.ShellBuilt("using Topogenia.Host.Desktop;\n/// <summary><see cref=\"ShellFolderOpener\"/> is never built.</summary>\nclass ShellFolderOpenerTests { "
            + "private readonly WindowsFolderDisk windows = new(); object?[] M(string folder) => [ShellFolderOpener.Verb, ShellFolderOpener.Class, "
            + "ShellFolderOpener.ClassOf(folder), ShellFolderOpener.OnShellThread(() => 0), Topogenia.Host.Desktop.ShellFolderOpener.Verb]; }"));
        Assert.Single(TestHostDetectors.ShellBuilt("using Topogenia.Host.Desktop;\nclass T { object M() => ShellFolderOpener.Equals; }"));
    }

    private const string TestHostUsings = "using Topogenia.Host.Composition; using Topogenia.Host.Desktop; using Topogenia.Host.Startup;\n";

    private const string Recorder = "builder.Services.AddSingleton<IFolderShell>(new FolderRecorder());";

    /// <summary>The places of a sample that build a host, as « how: recorded or not ».</summary>
    private static IEnumerable<string> TestHosts(string sample) =>
        TestHostDetectors.Hosts(TestHostUsings + sample).Select(host => $"{host.How}: {(host.Recorded ? "recorded" : "real shell")}");

    /// <summary>Each way a test builds the host, without a double of the shell: the host would hold the shell of Windows.</summary>
    [Theory]
    [InlineData("class T { object M(Dictionary<string, string?> settings) => HostApplication.Build([], builder => builder.Configuration.AddInMemoryCollection(settings)); }", "HostApplication.Build")]
    [InlineData("class T { object M() => HostApplication.Build([]); }", "HostApplication.Build")]
    [InlineData("class T { object M(Action<WebApplicationBuilder> configure) => HostApplication.Build([], configure); }", "HostApplication.Build")]
    [InlineData("class T { object M() => Topogenia.Host.Composition.HostApplication.Build([], builder => { }); }", "HostApplication.Build")]
    [InlineData("class T { Task<int> M(TextWriter output) => Program.RunAsync([], output, builder => { builder.Services.AddSingleton<IBrowserLauncher>(new Browser()); }, default); }", "Program.RunAsync")]
    [InlineData("class T { Task<int> M(TextWriter output) => HostCommands.RunAsync([\"--check\"], output, null, default); }", "HostCommands.RunAsync")]
    [InlineData("class T { object M() => new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseEnvironment(\"Production\")); }", "new WebApplicationFactory")]
    [InlineData("class T { object M() => new WebApplicationFactory<Program>(); }", "new WebApplicationFactory")]
    [InlineData("class T { object M() => HostApplication.Build([], builder => Configure(builder)); static void Configure(WebApplicationBuilder builder) => builder.Services.AddSingleton<IFolderDisk>(new Look()); }", "HostApplication.Build")]
    [InlineData("class T { object M() => HostApplication.Build([], builder => builder.Services.AddSingleton<IFolderShell>(new ShellFolderOpener())); }", "HostApplication.Build")]
    [InlineData("class T { object M(IFolderShell shell) => HostApplication.Build([], builder => builder.Services.AddSingleton<IFolderShell>(shell)); }", "HostApplication.Build")]
    public void A_test_that_builds_a_host_without_a_recorder_of_the_shell_is_detected(string sample, string how) =>
        Assert.Equal([$"{how}: real shell"], TestHosts(sample));

    /// <summary>The same places with a double: given in the lambda, by a method of the file that returns the configuration, through a method it calls, or by the services of the journeys.</summary>
    [Theory]
    [InlineData("class T { object M() => HostApplication.Build([], builder => { " + Recorder + " }); }", "HostApplication.Build")]
    [InlineData("class T { object M() => HostApplication.Build([], configure: builder => { " + Recorder + " }); }", "HostApplication.Build")]
    [InlineData("class T { object M() => HostApplication.Build([], Configured()); static Action<WebApplicationBuilder> Configured() => builder => { " + Recorder + " }; }", "HostApplication.Build")]
    [InlineData("class T { Task<int> M(TextWriter output) => Program.RunAsync([], output, builder => Give(builder), default); static void Give(WebApplicationBuilder builder) { " + Recorder + " } }", "Program.RunAsync")]
    [InlineData("class T { Task<int> M(TextWriter output) => HostCommands.RunAsync([\"--check\"], output, builder => { " + Recorder + " }, default); }", "HostCommands.RunAsync")]
    [InlineData("class T { object M() { var folders = new FolderRecorder(); return new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureTestServices(services => "
        + "{ services.RemoveAll<IFolderShell>(); services.AddSingleton<IFolderShell>(folders); })); } }", "new WebApplicationFactory")]
    [InlineData("class T { object M(string run) => HostApplication.Build([], builder => Configure(builder, run)); "
        + "static void Configure(WebApplicationBuilder builder, string run) => builder.Host.UseServiceProviderFactory(new JourneyServicesFactory(null, run)); }", "HostApplication.Build")]
    public void A_test_that_gives_its_host_a_recorder_of_the_shell_is_read_as_such(string sample, string how) =>
        Assert.Equal([$"{how}: recorded"], TestHosts(sample));

    /// <summary>An entry point named in a way the rule cannot read is a host without a recorder; a file that builds none is left alone.</summary>
    [Fact]
    public void A_host_built_in_a_way_the_rule_cannot_read_is_detected_and_a_file_that_builds_none_is_not()
    {
        Assert.Equal(["HostApplication.Build named another way: real shell"],
            TestHosts("class T { Func<string[], Action<WebApplicationBuilder>?, WebApplication> build = HostApplication.Build; }"));
        Assert.Equal(["HostApplication named another way: real shell"], TestHosts("class T { object M() => typeof(HostApplication).GetMethod(\"Build\")!; }"));
        Assert.All(new[]
        {
            "using Starter = Topogenia.Host.Composition.HostApplication; class T { object M() => Starter.Build([], builder => { " + Recorder + " }); }",
            "using static Topogenia.Host.Startup.HostCommands; class T { Task<int> M(TextWriter output) => RunAsync([\"--check\"], output, null, default); }",
            "class T { void M(WebApplicationBuilder builder) { " + Recorder + " builder.AddTopogenia(); } }",
            "sealed class Factory : WebApplicationFactory<Program> { }",
        }, sample => Assert.Contains(TestHosts(sample), host => host.EndsWith("named another way: real shell", StringComparison.Ordinal)));
        Assert.Empty(TestHosts("class T { object? M() => HostCommands.Parse([\"--check\"]); int N => Program.Success; WebApplicationFactory<Program>? factory; }"));
        Assert.Empty(TestHosts("/// <summary>Built like <see cref=\"HostApplication\"/> and <see cref=\"Program.RunAsync\"/>.</summary>\nclass T { }"));
    }
}
