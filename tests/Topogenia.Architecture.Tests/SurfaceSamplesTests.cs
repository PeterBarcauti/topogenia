// SurfaceSamplesTests.cs
// The samples of the detectors of the surface of a file: a second contract carried, a member that takes a text added to an admitted file, a handler named by a second route, a factory registered beside a port (the exception of ADR 104 to S-5.4-13).

namespace Topogenia.Architecture.Tests;

public sealed class SurfaceSamplesTests
{
    private const string Port = "src/Topogenia.Host/Port.cs";

    private const string PortText = "namespace Sample; public interface IOpener { Task ShowAsync(string path); } "
        + "public sealed class Opener : IOpener { public Task ShowAsync(string path) => Task.CompletedTask; }";

    /// <summary>A class that carries a second contract, an interface that extends the port and a class behind it, a class derived from the opener: all seen.</summary>
    [Fact]
    public void A_second_contract_on_a_class_of_a_port_and_another_type_that_carries_the_port_are_detected()
    {
        var files = SemanticSources.Samples(
            (Port, "namespace Sample; public interface IOpener { void Show(string path); } public interface ILook { void Open(string path); } "
                + "public class Opener : IOpener, ILook { public void Show(string path) { } public void Open(string path) { } } public sealed record Found(string Path);"),
            ("src/Topogenia.Host/Other.cs", "namespace Sample; public interface IWider : IOpener { } public sealed class Other : IWider { public void Show(string path) { } } "
                + "public sealed class Derived : Opener { } public sealed class Apart { public void Show(string path) { } }"));
        Assert.Equal(["Sample.Opener : Sample.ILook, Sample.IOpener", "Sample.Found : System.IEquatable"], SurfaceDetectors.Carried(files[0]));
        Assert.Equal(["Sample.IWider : Sample.IOpener", "Sample.Other : Sample.IOpener, Sample.IWider", "Sample.Derived : Sample.Opener, Sample.ILook, Sample.IOpener"],
            SurfaceDetectors.Carried(files[1]));
    }

    /// <summary>
    /// A handler with a second method, which takes a path, and other ways to be given a text (a function, a property, a
    /// list): each is a member another file may reach, and each is told from the method that takes identifiers alone, from
    /// a text only given back and from what is private.
    /// </summary>
    [Fact]
    public void A_member_that_takes_a_text_added_to_a_file_is_detected_and_a_private_one_is_not()
    {
        var file = SemanticSources.Sample("namespace Sample; public sealed class Api(System.Func<string, Task> open) { public const string Code = \"CODE\"; "
            + "public string? Folder { get; set; } public string Name => \"name\"; "
            + "public Task ShowAsync(Guid step, Guid document, CancellationToken token) => open(Folder ?? Name); public Task ShowPathAsync(string path) => open(path); "
            + "internal void Take(System.Collections.Generic.IEnumerable<string> paths) { } public bool Read(out string path) { path = Name; return true; } "
            + "private Task Hidden(string path) => open(path); private sealed class Inner { public void Take(string path) { } } } "
            + "public delegate Task Shows(string path); public enum Kind { Folder, File }", "src/Topogenia.Host/Api.cs");
        Assert.Equal(["Sample.Api..ctor(Func<string, Task>)", "Sample.Api.Folder { get; set; }", "Sample.Api.Name { get; }", "Sample.Api.Read(out string)",
            "Sample.Api.ShowAsync(Guid, Guid, CancellationToken)", "Sample.Api.ShowPathAsync(string)", "Sample.Api.Take(IEnumerable<string>)", "Sample.Api.const Code",
            "delegate Sample.Shows(string)"], SurfaceDetectors.Reachable(file).Order(StringComparer.Ordinal));
        Assert.Equal(["Sample.Api..ctor(Func<string, Task>)", "Sample.Api.Folder { get; set; }", "Sample.Api.ShowPathAsync(string)", "Sample.Api.Take(IEnumerable<string>)",
            "delegate Sample.Shows(string)"], SurfaceDetectors.TextTakers(file).Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("string", true)]
    [InlineData("string?", true)]
    [InlineData("char[]", true)]
    [InlineData("object", true)]
    [InlineData("System.ReadOnlySpan<char>", true)]
    [InlineData("System.Collections.Generic.IReadOnlyList<string>", true)]
    [InlineData("System.Func<string, System.Threading.Tasks.Task>", true)]
    [InlineData("System.Action<System.Func<string, bool>>", true)]
    [InlineData("System.Text.StringBuilder", true)]
    [InlineData("System.Uri", true)]
    [InlineData("System.Guid", false)]
    [InlineData("int", false)]
    [InlineData("System.Threading.CancellationToken", false)]
    [InlineData("System.Func<System.Guid, string>", true)]
    [InlineData("System.Func<System.Guid, int>", false)]
    [InlineData("System.TimeSpan?", false)]
    public void A_type_by_which_a_text_may_come_is_told_from_the_others(string type, bool carries)
    {
        var file = SemanticSources.Sample($"class A {{ public void M({type} value) {{ }} }}", "src/Topogenia.Host/Sample.cs");
        Assert.Equal(carries, SurfaceDetectors.TextTakers(file).Any());
    }

    /// <summary>A handler named by a second route, and by a handler that is no route at all: both show, with the route of each name.</summary>
    [Fact]
    public void A_handler_named_by_a_second_route_or_outside_a_route_is_detected()
    {
        const string usings = "using Microsoft.AspNetCore.Builder; using Microsoft.AspNetCore.Http; using Microsoft.AspNetCore.Routing;\n";
        var files = SemanticSources.Samples(
            ("src/Topogenia.Host/Api/FolderApi.cs", "namespace Sample; public sealed class FolderApi { public Task ShowAsync(Guid step, Guid document) => Task.CompletedTask; }"),
            ("src/Topogenia.Host/Endpoints/Routes.cs", usings + "static class Routes { static void Map(IEndpointRouteBuilder routes) { "
                + "routes.MapPost(\"/folder\", (Guid id, Guid document, Sample.FolderApi api) => api.ShowAsync(id, document)); "
                + "routes.MapGet(\"/sheet\", (Guid id, Sample.FolderApi api) => api.ShowAsync(id, id)); } }"),
            ("src/Topogenia.Host/Api/SheetApi.cs", "class SheetApi(Sample.FolderApi folders) { Task M(Guid id) => folders.ShowAsync(id, id); }"),
            ("src/Topogenia.Host/Api/Other.cs", "class Other { Task M(Guid id) => Task.CompletedTask; }"));
        var types = ShippedCodeDetectors.DeclaredTypes(files[0]).ToHashSet(StringComparer.Ordinal);
        Assert.Equal(["src/Topogenia.Host/Endpoints/Routes.cs", "src/Topogenia.Host/Api/SheetApi.cs"],
            files.Where(file => ShippedCodeDetectors.UsesOf(file, types).Any()).Select(file => file.Path));
        Assert.Equal(["MapPost \"/folder\"", "MapPost \"/folder\"", "MapGet \"/sheet\"", "MapGet \"/sheet\""], SurfaceDetectors.RoutesNaming(files[1], types));
        Assert.Equal(["outside a route", "outside a route"], SurfaceDetectors.RoutesNaming(files[2], types));
    }

    /// <summary>A factory that takes a text and hands it to the port, registered beside it: a statement more that names the port.</summary>
    [Fact]
    public void A_delegate_or_a_factory_registered_beside_a_port_is_detected()
    {
        const string registered = "services.AddSingleton<Sample.IOpener, Sample.Opener>();";
        const string factory = "services.AddSingleton<System.Func<string, Task>>(provider => path => provider.GetRequiredService<Sample.IOpener>().ShowAsync(path));";
        var files = SemanticSources.Samples((Port, PortText),
            ("src/Topogenia.Host/Registration.cs", "using Microsoft.Extensions.DependencyInjection; static class Registration { static void Add(IServiceCollection services) { "
                + $"{registered} services.AddSingleton<System.TimeProvider>(System.TimeProvider.System);\n    {factory} }} }}"),
            ("src/Topogenia.Host/Texts.cs", "class Texts { object? M() => System.Type.GetType(\"Sample.IOpener\"); string N => $\"no Opener {1}\"; }"));
        var types = ShippedCodeDetectors.DeclaredTypes(files[0]).ToHashSet(StringComparer.Ordinal);
        Assert.Equal([registered, factory], SurfaceDetectors.StatementsNaming(files[1], types));
        Assert.Equal(["src/Topogenia.Host/Texts.cs:1 IOpener"], SurfaceDetectors.TextsNaming(files[2], ["IOpener", "IShell"]));
        Assert.Empty(SurfaceDetectors.TextsNaming(files[1], ["IOpener", "IShell"]));
    }
}
