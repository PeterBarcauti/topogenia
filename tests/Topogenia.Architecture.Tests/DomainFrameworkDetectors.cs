// DomainFrameworkDetectors.cs
// Allow list of the framework types that the domain may use, read on resolved symbols: no input/output, reflection, interop, thread, clock or chance.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Topogenia.Architecture.Tests.SemanticSources;

namespace Topogenia.Architecture.Tests;

/// <summary>
/// ARC4-M2, ARC4 minor 5: the domain does no input or output and reads no process state (spec 3.2, 3.3, D03). Rather than
/// a list of forbidden namespaces, which misses whatever it does not name (System.Xml, P/Invoke, reflection, AppContext),
/// the domain may use only the framework types listed here; everything else is refused. Every resolved name (type,
/// member, attribute, extension method) and the type of every expression (so that `var` hides nothing) are checked.
/// </summary>
internal static class DomainFrameworkDetectors
{
    /// <summary>Namespaces of pure computation allowed in full (exactly these namespaces, not their sub-namespaces).</summary>
    private static readonly string[] AllowedNamespaces =
    [
        "System.Collections", "System.Collections.Generic", "System.Collections.ObjectModel", "System.Collections.Immutable",
        "System.Collections.Frozen", "System.Linq", "System.Text", "System.Text.RegularExpressions", "System.Globalization",
        "System.Diagnostics.CodeAnalysis", "System.Runtime.CompilerServices", "System.Numerics",
    ];

    /// <summary>
    /// The types of the namespace System that the domain may use: values, texts, collections helpers, delegates and
    /// exceptions. Absent on purpose: Environment, AppContext, AppDomain, Console, GC, Activator, Random, DateTimeOffset,
    /// TimeProvider, TimeZoneInfo, IntPtr, Buffer, Convert, Lazy and every other type. The clock and identifier members
    /// of DateTime and Guid (Now, UtcNow, Today, NewGuid, CreateVersion7) are kept to the operation context by
    /// <see cref="SemanticDetectors.ClockOrIdentifierFactories"/>.
    /// </summary>
    private static readonly HashSet<string> SystemTypes = new(StringComparer.Ordinal)
    {
        "System.Object", "System.String", "System.Char", "System.Boolean", "System.Byte", "System.SByte", "System.Int16", "System.UInt16",
        "System.Int32", "System.UInt32", "System.Int64", "System.UInt64", "System.Int128", "System.UInt128", "System.Single",
        "System.Double", "System.Decimal", "System.Half", "System.Void", "System.Enum", "System.ValueType", "System.Nullable",
        "System.Array", "System.ValueTuple", "System.Tuple", "System.DateTime", "System.DateTimeKind", "System.DayOfWeek",
        "System.DateOnly", "System.TimeOnly", "System.TimeSpan", "System.Guid", "System.Math", "System.MathF", "System.MidpointRounding",
        "System.StringComparer", "System.StringComparison", "System.StringSplitOptions", "System.Index", "System.Range", "System.Span",
        "System.ReadOnlySpan", "System.Memory", "System.ReadOnlyMemory", "System.Action", "System.Func", "System.Predicate",
        "System.Comparison", "System.Converter", "System.IComparable", "System.IEquatable", "System.IFormattable", "System.IFormatProvider",
        "System.IDisposable", "System.FormattableString", "System.Exception", "System.ArgumentException", "System.ArgumentNullException",
        "System.ArgumentOutOfRangeException", "System.InvalidOperationException", "System.NotSupportedException", "System.FormatException",
        "System.OverflowException", "System.Attribute", "System.AttributeUsageAttribute", "System.AttributeTargets", "System.FlagsAttribute",
        "System.ObsoleteAttribute", "System.Uri", "System.UriKind", "System.Type", "System.HashCode",
    };

    /// <summary>Types of an allowed namespace that still reach memory, threads or private members.</summary>
    private static readonly HashSet<string> ForbiddenTypes = new(StringComparer.Ordinal)
    {
        "System.Linq.ParallelEnumerable", "System.Linq.ParallelQuery", "System.Runtime.CompilerServices.Unsafe",
        "System.Runtime.CompilerServices.RuntimeHelpers", "System.Runtime.CompilerServices.ModuleInitializerAttribute",
        "System.Runtime.CompilerServices.UnsafeAccessorAttribute", "System.Collections.Concurrent",
    };

    /// <summary>
    /// The documented exception, file by file: the types (or members, written Type.Member) that one file only may use.
    /// Sheets/SheetJson.cs reads and writes the JSON text of a sheet in memory (annex B 2.12), without the reflection
    /// serializer. The lists of error codes that the message test of spec 3.6 checks are read by the tests: the domain
    /// reads no type.
    /// </summary>
    private static readonly Dictionary<string, HashSet<string>> FileExceptions = new(StringComparer.Ordinal)
    {
        ["Sheets/SheetJson.cs"] = new(StringComparer.Ordinal)
        {
            "System.Text.Json.JsonDocument", "System.Text.Json.JsonDocumentOptions", "System.Text.Json.JsonElement",
            "System.Text.Json.JsonProperty", "System.Text.Json.JsonValueKind", "System.Text.Json.JsonSerializerOptions",
            "System.Text.Json.Nodes.JsonNode", "System.Text.Json.Nodes.JsonObject", "System.Text.Json.Nodes.JsonArray",
            "System.Text.Json.Nodes.JsonValue", "System.Text.Json.Nodes.JsonNodeOptions", "System.Text.Encodings.Web.JavaScriptEncoder",
            // The refusal of a text that is not JSON, caught by TryThaw for the content of a version brought by a package.
            "System.Text.Json.JsonException",
        },
    };

    /// <summary>The files that hold a documented exception, so that a test can check that each is still used.</summary>
    public static IEnumerable<string> ExceptedFiles => FileExceptions.Keys;

    /// <summary>The places where the domain uses a framework type or member outside the allow list, as "path:line name".</summary>
    public static IEnumerable<string> ForeignUses(SourceFile file)
    {
        var token = TestContext.Current.CancellationToken;
        var own = file.Model.Compilation.Assembly;
        foreach (var (name, symbol) in file.ResolvedNames())
        {
            if (symbol is INamespaceSymbol or ILocalSymbol or IParameterSymbol or IRangeVariableSymbol or ILabelSymbol or ITypeParameterSymbol or IDiscardSymbol)
                continue;
            if (Refusal(file, name, symbol, own) is { } refusal)
                yield return $"{file.Where(name)} {refusal}";
        }
        foreach (var expression in file.Root.DescendantNodes().OfType<ExpressionSyntax>())
        {
            var type = file.Model.GetTypeInfo(expression, token).Type;
            if (type is not null && TypeRefusal(file, type, own) is { } refusal)
                yield return $"{file.Where(expression)} {refusal}";
        }
        foreach (var node in file.Root.DescendantNodes())
        {
            var native = node switch
            {
                MemberDeclarationSyntax member when member.Modifiers.Any(SyntaxKind.ExternKeyword) => "extern",
                MemberDeclarationSyntax member when member.Modifiers.Any(SyntaxKind.UnsafeKeyword) => "unsafe",
                LocalFunctionStatementSyntax local when local.Modifiers.Any(SyntaxKind.ExternKeyword) || local.Modifiers.Any(SyntaxKind.UnsafeKeyword) => "extern",
                UnsafeStatementSyntax or FunctionPointerTypeSyntax or PointerTypeSyntax or ExternAliasDirectiveSyntax => "unsafe",
                _ => null,
            };
            if (native is not null)
                yield return $"{file.Where(node)} {native}";
        }
    }

    private static string? Refusal(SourceFile file, SimpleNameSyntax name, ISymbol symbol, IAssemblySymbol own)
    {
        if (TypeAndBases(symbol).Contains("System.HashCode", StringComparer.Ordinal) && !InGetHashCode(name))
            return "System.HashCode";
        if (symbol is ITypeSymbol type)
            return TypeRefusal(file, type, own);
        var container = symbol is IMethodSymbol { ReducedFrom: { } reduced } ? reduced.ContainingType : symbol.ContainingType;
        if (container is null)
            return null;
        if (TypeRefusal(file, container, own) is { } refused)
            return refused;
        var member = $"{TypeOf(container)}.{symbol.Name}";
        if (Excepted(file, member))
            return null;
        if (TypeOf(container) == "System.Type")
            return member;
        if (symbol.Name == "GetHashCode" && !InGetHashCode(name))
            return member;
        return null;
    }

    /// <summary>A type outside the allow list, or one of its type arguments or element types; null when allowed.</summary>
    private static string? TypeRefusal(SourceFile file, ITypeSymbol type, IAssemblySymbol own)
    {
        switch (type)
        {
            case IErrorTypeSymbol or ITypeParameterSymbol:
                return null;
            case IDynamicTypeSymbol or IPointerTypeSymbol or IFunctionPointerTypeSymbol:
                return type.ToDisplayString();
            case IArrayTypeSymbol array:
                return TypeRefusal(file, array.ElementType, own);
            case INamedTypeSymbol named:
                foreach (var argument in named.TypeArguments)
                {
                    if (TypeRefusal(file, argument, own) is { } refused)
                        return refused;
                }
                if (named.IsAnonymousType || named.IsTupleType)
                    return null;
                return NamedAllowed(file, named, own) ? null : TypeOf(named);
            default:
                return type.ToDisplayString();
        }
    }

    private static bool NamedAllowed(SourceFile file, INamedTypeSymbol type, IAssemblySymbol own)
    {
        var outer = type;
        while (outer.ContainingType is { } container)
            outer = container;
        var space = outer.ContainingNamespace?.ToDisplayString() ?? string.Empty;
        if (SymbolEqualityComparer.Default.Equals(outer.ContainingAssembly, own) || space == "Topogenia" || space.StartsWith("Topogenia.", StringComparison.Ordinal))
            return true;
        var name = TypeOf(outer);
        if (ForbiddenTypes.Contains(name) || ForbiddenTypes.Contains(space))
            return false;
        return space == "System" ? SystemTypes.Contains(name) : AllowedNamespaces.Contains(space, StringComparer.Ordinal) || Excepted(file, name);
    }

    /// <summary>
    /// A per-process randomised hash (HashCode, GetHashCode) is allowed only in an override of GetHashCode, where it keeps
    /// equality and hashing consistent and never reaches a result.
    /// </summary>
    private static bool InGetHashCode(SyntaxNode node) =>
        node.Ancestors().OfType<MethodDeclarationSyntax>().Any(method => method.Identifier.ValueText == "GetHashCode"
            && method.Modifiers.Any(SyntaxKind.OverrideKeyword) && method.ParameterList.Parameters.Count == 0);

    private static bool Excepted(SourceFile file, string name) =>
        FileExceptions.TryGetValue(file.DomainPath, out var allowed) && allowed.Contains(name);
}
