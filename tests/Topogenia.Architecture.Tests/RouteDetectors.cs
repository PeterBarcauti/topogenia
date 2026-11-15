// RouteDetectors.cs
// Detector on resolved symbols of an HTTP entry point (route, branch of the pipeline, controller) and the only places allowed to declare one (spec 3.2, ADR 043).

using Microsoft.CodeAnalysis;
using static Topogenia.Architecture.Tests.SemanticSources;

namespace Topogenia.Architecture.Tests;

internal static class RouteDetectors
{
    /// <summary>The folder of the routes, the only place allowed to declare one (porte P4 round 3: no file of the pipeline is exempted).</summary>
    public static readonly string[] AllowedPlaces = ["src/Topogenia.Host/Endpoints/"];

    /// <summary>
    /// The namespace of the Endpoints folder: a call to one of its methods (the pipeline calls <c>MapApi</c>) hands the
    /// requests over to routes declared and checked there, and declares none itself.
    /// </summary>
    private const string EndpointsNamespace = "Topogenia.Host.Endpoints";

    /// <summary>The builders of ASP.NET Core on which a route or a branch of the pipeline is declared.</summary>
    private static readonly string[] Builders =
        ["Microsoft.AspNetCore.Routing.IEndpointRouteBuilder", "Microsoft.AspNetCore.Builder.IApplicationBuilder", "Microsoft.AspNetCore.Routing.IRouteBuilder"];

    /// <summary>The methods of a builder that answer a request or branch the pipeline, beside every `Map*` method.</summary>
    private static readonly string[] Branches = ["Run", "UseWhen", "UseEndpoints", "UseRouter"];

    /// <summary>The controllers of MVC: their base type and the attributes that make a class or a method an entry point.</summary>
    private static readonly string[] ControllerTypes =
        ["Microsoft.AspNetCore.Mvc.ControllerBase", "Microsoft.AspNetCore.Mvc.ApiControllerAttribute", "Microsoft.AspNetCore.Mvc.RouteAttribute",
         "Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute"];

    /// <summary>
    /// Spec 3.2: every name of a file that resolves to an HTTP entry point, whatever the name of the receiver, the form of
    /// the address (literal, verbatim, interpolated, constant or variable) or the form of the call (extension, static
    /// call, method group): an extension method of a builder of ASP.NET Core named <c>Map*</c> (routes, groups, fallbacks,
    /// controllers, hubs, <c>Map</c> and <c>MapWhen</c> branches) or one of <see cref="Branches"/>; the data sources of the
    /// routes; a controller, its base type or one of its routing attributes. A method of the model that happens to be
    /// named <c>Map</c> (<c>workspace.Map(id)</c>) has no builder as receiver and is no entry point, nor is a call to a
    /// method of <see cref="EndpointsNamespace"/>. A middleware added by <c>Use</c> or <c>UseMiddleware</c> is no entry
    /// point either, even when it answers one address (the launch of the desktop mode, <c>GET /launch</c>, spec 5.2): it
    /// is a step of the pipeline, ordered in <c>Composition/HostPipeline</c> (ADR 043, revision of the round 3).
    /// </summary>
    public static IEnumerable<string> EntryPoints(SourceFile file) =>
        file.ResolvedNames()
            .Where(pair => IsRouteOrBranch(pair.Symbol) || IsMember(pair.Symbol, Builders[0], "DataSources")
                || ControllerTypes.Any(type => IsTypeOrMember(pair.Symbol, type)))
            .Select(pair => $"{file.Where(pair.Name)} {pair.Symbol.Name}");

    /// <summary>The entry points of the files outside <see cref="AllowedPlaces"/>.</summary>
    public static IReadOnlyList<string> Misplaced(IEnumerable<SourceFile> files) => SemanticDetectors.Offenders(files, EntryPoints, AllowedPlaces);

    private static bool IsRouteOrBranch(ISymbol symbol) =>
        symbol is IMethodSymbol method
        && (method.ReducedFrom ?? method) is { IsExtensionMethod: true, Parameters: [var receiver, ..] } definition
        && (definition.Name.StartsWith("Map", StringComparison.Ordinal) || Branches.Contains(definition.Name, StringComparer.Ordinal))
        && definition.ContainingNamespace.ToDisplayString() != EndpointsNamespace
        && IsBuilder(receiver.Type);

    private static bool IsBuilder(ITypeSymbol type) =>
        type is ITypeParameterSymbol parameter
            ? parameter.ConstraintTypes.Any(IsBuilder)
            : Builders.Contains(TypeOf(type), StringComparer.Ordinal) || type.AllInterfaces.Any(contract => Builders.Contains(TypeOf(contract), StringComparer.Ordinal));
}
