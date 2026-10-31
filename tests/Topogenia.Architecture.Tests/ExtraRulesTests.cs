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
}
