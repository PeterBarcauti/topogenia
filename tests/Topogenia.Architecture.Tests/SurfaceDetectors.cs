// SurfaceDetectors.cs
// Detectors of what a file of the shipped code offers to the others: the contracts its types carry, the members another file may reach, those that take a text, and the statements and routes that name given types (the exception of ADR 104 to S-5.4-13).

using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Topogenia.Architecture.Tests;

internal static partial class SurfaceDetectors
{
    /// <summary>Short names of the types, `string` for `String`, `T?` for a value or a reference that may be null.</summary>
    private static readonly SymbolDisplayFormat Short = SymbolDisplayFormat.MinimallyQualifiedFormat;

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    private static IEnumerable<INamedTypeSymbol> TypesOf(SourceFile file) =>
        file.Root.DescendantNodes().Where(node => node is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax)
            .Select(node => file.Model.GetDeclaredSymbol(node, TestContext.Current.CancellationToken))
            .OfType<INamedTypeSymbol>();

    /// <summary>
    /// The types a file declares that carry something, as « Type : Base, Contract, Contract »: the base type when it is
    /// one of the solution or of a library (not `object`), then every interface, inherited ones included, sorted.
    /// </summary>
    public static IEnumerable<string> Carried(SourceFile file)
    {
        foreach (var type in TypesOf(file).Where(type => type.TypeKind is TypeKind.Class or TypeKind.Struct or TypeKind.Interface))
        {
            IEnumerable<string> parent = type.BaseType is { SpecialType: SpecialType.None } own ? [SemanticSources.TypeOf(own)] : [];
            var carried = parent.Concat(type.AllInterfaces.Select(contract => SemanticSources.TypeOf(contract)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)).ToList();
            if (carried.Count > 0)
                yield return $"{SemanticSources.TypeOf(type)} : {string.Join(", ", carried)}";
        }
    }

    /// <summary>
    /// The members a file declares that another file may reach, as « Type.Member »: neither private nor inside a private
    /// type, the members that the compiler writes by itself left out (the equality of a record). A method and a
    /// constructor (`.ctor`) come with the types of their parameters, a property with its accessors, a field with `const`
    /// or `readonly`, a delegate with `delegate`. The members of an enumeration are no members to call: left out.
    /// </summary>
    public static IEnumerable<string> Reachable(SourceFile file) => Members(file).Select(Written);

    /// <summary>
    /// Among <see cref="Reachable"/>, the members that take a text from who calls them: a method, a constructor or a
    /// delegate with a parameter that carries one (<see cref="CarriesText"/>, a parameter that only gives a value back
    /// left out), a property that may be given one, a field that may be written with one.
    /// </summary>
    public static IEnumerable<string> TextTakers(SourceFile file) => Members(file).Where(TakesText).Select(Written);

    private static IEnumerable<ISymbol> Members(SourceFile file)
    {
        foreach (var type in TypesOf(file).Where(IsReached))
        {
            if (type.TypeKind == TypeKind.Delegate)
                yield return type;
            if (type.TypeKind is TypeKind.Delegate or TypeKind.Enum)
                continue;
            foreach (var member in type.GetMembers())
            {
                if (member is INamedTypeSymbol or IMethodSymbol { AssociatedSymbol: not null } || member.IsImplicitlyDeclared
                    || member.DeclaredAccessibility == Accessibility.Private || !member.DeclaringSyntaxReferences.Any(reference => reference.SyntaxTree == file.Tree))
                    continue;
                yield return member;
            }
        }
    }

    private static bool IsReached(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.ContainingType)
        {
            if (current.DeclaredAccessibility == Accessibility.Private)
                return false;
        }
        return true;
    }

    private static string Written(ISymbol member)
    {
        var owner = SemanticSources.TypeOf(member);
        return member switch
        {
            INamedTypeSymbol { DelegateInvokeMethod: { } invoke } => $"delegate {owner}({Parameters(invoke)})",
            IMethodSymbol method => $"{owner}.{method.Name}({Parameters(method)})",
            IPropertySymbol property => $"{owner}.{property.Name} {{ {Accessors(property)} }}",
            IFieldSymbol field => $"{owner}.{(field.IsConst ? "const " : field.IsReadOnly ? "readonly " : "")}{field.Name}",
            _ => $"{owner}.{member.Name}",
        };
    }

    private static string Parameters(IMethodSymbol method) =>
        string.Join(", ", method.Parameters.Select(parameter => (parameter.RefKind == RefKind.None ? "" : parameter.RefKind.ToString().ToLowerInvariant() + " ") + parameter.Type.ToDisplayString(Short)));

    private static string Accessors(IPropertySymbol property) =>
        string.Join(" ", new[] { property.GetMethod is null ? null : "get;", property.SetMethod is null ? null : property.SetMethod.IsInitOnly ? "init;" : "set;" }.OfType<string>());

    private static bool TakesText(ISymbol member) => member switch
    {
        INamedTypeSymbol { DelegateInvokeMethod: { } invoke } => invoke.Parameters.Any(Gives),
        IMethodSymbol method => method.Parameters.Any(Gives),
        IPropertySymbol property => property.Parameters.Any(Gives)
            || (property.SetMethod is { DeclaredAccessibility: not Accessibility.Private } && CarriesText(property.Type)),
        IFieldSymbol field => !field.IsConst && !field.IsReadOnly && CarriesText(field.Type),
        _ => false,
    };

    private static bool Gives(IParameterSymbol parameter) => parameter.RefKind != RefKind.Out && CarriesText(parameter.Type);

    /// <summary>
    /// A type by which a text may come: a string, a character, anything (`object`, `dynamic`, a type parameter), a
    /// builder of strings, an address, and what is made of one of them: an array, a pointer, a generic type by its
    /// arguments (a list of strings, a span of characters), a delegate by its parameters (a function that takes a text).
    /// </summary>
    public static bool CarriesText(ITypeSymbol type) => type switch
    {
        { SpecialType: SpecialType.System_String or SpecialType.System_Char or SpecialType.System_Object } => true,
        IDynamicTypeSymbol or ITypeParameterSymbol => true,
        IArrayTypeSymbol array => CarriesText(array.ElementType),
        IPointerTypeSymbol pointer => CarriesText(pointer.PointedAtType),
        INamedTypeSymbol named => SemanticSources.TypeOf(named) is "System.Text.StringBuilder" or "System.Uri"
            || named.TypeArguments.Any(CarriesText) || (named.DelegateInvokeMethod?.Parameters.Any(Gives) ?? false),
        _ => false,
    };

    /// <summary>The texts of a file (a string of any kind, the text of an interpolated one) that write one of the names, as « path:line name ».</summary>
    public static IEnumerable<string> TextsNaming(SourceFile file, IReadOnlyCollection<string> names) =>
        from token in file.Root.DescendantTokens()
        where token.IsKind(SyntaxKind.StringLiteralToken) || token.IsKind(SyntaxKind.InterpolatedStringTextToken) || token.IsKind(SyntaxKind.Utf8StringLiteralToken)
            || token.IsKind(SyntaxKind.SingleLineRawStringLiteralToken) || token.IsKind(SyntaxKind.MultiLineRawStringLiteralToken)
            || token.IsKind(SyntaxKind.Utf8SingleLineRawStringLiteralToken) || token.IsKind(SyntaxKind.Utf8MultiLineRawStringLiteralToken)
        from name in names
        where token.ValueText.Contains(name, StringComparison.Ordinal)
        select $"{file.Path}:{token.GetLocation().GetLineSpan().StartLinePosition.Line + 1} {name}";

    /// <summary>The names of a file that resolve to one of the <paramref name="types"/> or to one of their members.</summary>
    private static IEnumerable<SimpleNameSyntax> NamesOf(SourceFile file, IReadOnlySet<string> types) =>
        file.ResolvedNames().Where(pair => pair.Symbol is not INamespaceSymbol && types.Contains(SemanticSources.TypeOf(pair.Symbol))).Select(pair => pair.Name);

    /// <summary>
    /// The statements of a file that name one of the <paramref name="types"/> or one of their members, as they are
    /// written (spaces and line breaks read as one space), each once, in the order of the text. Outside a statement: the
    /// parameter, the base type, the expression body or the member that names it.
    /// </summary>
    public static IReadOnlyList<string> StatementsNaming(SourceFile file, IReadOnlySet<string> types) =>
        [.. NamesOf(file, types)
            .Select(name => name.Ancestors().First(node => node is (StatementSyntax and not BlockSyntax) or ParameterSyntax or BaseTypeSyntax
                or ArrowExpressionClauseSyntax or MemberDeclarationSyntax or UsingDirectiveSyntax or CompilationUnitSyntax))
            .Select(node => Spaces().Replace(node.ToString(), " "))
            .Distinct(StringComparer.Ordinal)];

    /// <summary>
    /// For each name of a file that resolves to one of the <paramref name="types"/> or to one of their members, the
    /// route that holds it, as « MapPost "/address" » (the method of the route and its first argument as written), or
    /// « outside a route »: a handler named by two routes, or by code that is no route, shows here.
    /// </summary>
    public static IReadOnlyList<string> RoutesNaming(SourceFile file, IReadOnlySet<string> types) =>
        [.. NamesOf(file, types).Select(name => name.Ancestors().OfType<InvocationExpressionSyntax>().Select(call => RouteOf(file, call)).OfType<string>().FirstOrDefault()
            ?? "outside a route")];

    private static string? RouteOf(SourceFile file, InvocationExpressionSyntax call) =>
        file.SymbolOf(call) is IMethodSymbol method && (method.ReducedFrom ?? method) is { IsExtensionMethod: true } route
            && route.Name.StartsWith("Map", StringComparison.Ordinal)
            ? $"{route.Name} {call.ArgumentList.Arguments.FirstOrDefault()}"
            : null;
}
