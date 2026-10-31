// SemanticSources.cs
// Roslyn compilations of the production sources and of faulty samples, so that the rules read resolved symbols, not text.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Topogenia.Architecture.Tests;

/// <summary>A source file of a compilation: its repository path, its tree and its semantic model.</summary>
internal sealed record SourceFile(string Path, SyntaxTree Tree, SemanticModel Model)
{
    public SyntaxNode Root => Tree.GetRoot(TestContext.Current.CancellationToken);

    /// <summary>Path relative to the domain project, empty outside it.</summary>
    public string DomainPath => Path.StartsWith(SemanticSources.DomainFolder, StringComparison.Ordinal) ? Path[SemanticSources.DomainFolder.Length..] : string.Empty;

    public string Where(SyntaxNode node) => $"{Path}:{node.GetLocation().GetLineSpan().StartLinePosition.Line + 1}";

    /// <summary>The symbol a name or an expression resolves to; the first candidate when the binding is ambiguous (a method group).</summary>
    public ISymbol? SymbolOf(SyntaxNode node)
    {
        var info = Model.GetSymbolInfo(node, TestContext.Current.CancellationToken);
        return info.Symbol ?? info.CandidateSymbols.FirstOrDefault();
    }

    /// <summary>Every name under a node (identifiers, generic names, names of using directives and patterns) with its symbol.</summary>
    public IEnumerable<(SimpleNameSyntax Name, ISymbol Symbol)> ResolvedNames(SyntaxNode? node = null)
    {
        foreach (var name in (node ?? Root).DescendantNodesAndSelf().OfType<SimpleNameSyntax>())
        {
            if (SymbolOf(name) is { } symbol)
                yield return (name, symbol);
        }
    }
}

internal static class SemanticSources
{
    public const string DomainFolder = "src/Topogenia.Domain/";

    /// <summary>The global usings that the implicit usings of the SDK give to every project.</summary>
    private static readonly string[] SdkUsings =
        ["System", "System.Collections.Generic", "System.IO", "System.Linq", "System.Net.Http", "System.Threading", "System.Threading.Tasks"];

    /// <summary>The further global usings of the web SDK (the host).</summary>
    private static readonly string[] WebUsings =
    [
        "System.Net.Http.Json", "Microsoft.AspNetCore.Builder", "Microsoft.AspNetCore.Hosting", "Microsoft.AspNetCore.Http",
        "Microsoft.AspNetCore.Routing", "Microsoft.Extensions.Configuration", "Microsoft.Extensions.DependencyInjection",
        "Microsoft.Extensions.Hosting", "Microsoft.Extensions.Logging",
    ];

    /// <summary>
    /// The only errors accepted in a compilation of the production code: partial members that a source generator of the
    /// build implements ([GeneratedRegex]). Any other error is a name left unresolved, which a rule would then miss.
    /// </summary>
    private static readonly string[] GeneratorErrors = ["CS8795", "CS9248"];

    private static readonly CSharpParseOptions ParseOptions = new(LanguageVersion.Preview);

    /// <summary>The framework assemblies of the test process, without the assemblies of the solution.</summary>
    private static readonly Lazy<IReadOnlyList<MetadataReference>> Platform = new(() =>
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(System.IO.Path.PathSeparator)
            .Where(path => !System.IO.Path.GetFileName(path).StartsWith("Topogenia", StringComparison.Ordinal))
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToList());

    private static readonly Lazy<IReadOnlyList<SourceFile>> ProductionFiles = new(CompileProduction);

    /// <summary>Every C# file of the production code, compiled project by project with the references of the build.</summary>
    public static IReadOnlyList<SourceFile> Production => ProductionFiles.Value;

    /// <summary>The C# files of the domain project.</summary>
    public static IReadOnlyList<SourceFile> Domain => Production.Where(file => file.Path.StartsWith(DomainFolder, StringComparison.Ordinal)).ToList();

    /// <summary>A faulty or correct sample compiled alone against the framework and the domain assembly; unresolved names stay unresolved.</summary>
    public static SourceFile Sample(string csharp, string path = DomainFolder + "Rules/Sample.cs") => Samples((path, csharp))[0];

    /// <summary>Several sample files compiled together (folders of the domain, partial names, using directives).</summary>
    public static IReadOnlyList<SourceFile> Samples(params (string Path, string Text)[] files) =>
        Compile("Sample", files, Platform.Value.Append(Reference(typeof(Topogenia.Domain.Model.IRecord).Assembly.Location)), web: false,
            OutputKind.DynamicallyLinkedLibrary, strict: false);

    private static MetadataReference Reference(string path) => MetadataReference.CreateFromFile(path);

    private static IReadOnlyList<SourceFile> CompileProduction()
    {
        var files = new List<SourceFile>();
        var projects = RepositoryFiles.ProductionCSharp()
            .Select(file => (Path: RepositoryFiles.Relative(file), Text: File.ReadAllText(file)))
            .GroupBy(file => file.Path.Split('/')[1], StringComparer.Ordinal);
        foreach (var project in projects)
        {
            var solution = Directory.EnumerateFiles(AppContext.BaseDirectory, "Topogenia.*.dll")
                .Where(path => System.IO.Path.GetFileNameWithoutExtension(path) is var name
                    && name != project.Key && !name.EndsWith(".Tests", StringComparison.Ordinal))
                .Select(Reference);
            var web = project.Key.EndsWith(".Host", StringComparison.Ordinal);
            var references = project.Key.EndsWith(".Domain", StringComparison.Ordinal) ? Platform.Value : Platform.Value.Concat(solution);
            files.AddRange(Compile(project.Key, project.ToList(), references, web,
                web ? OutputKind.ConsoleApplication : OutputKind.DynamicallyLinkedLibrary, strict: true));
        }
        return files;
    }

    private static IReadOnlyList<SourceFile> Compile(string name, IReadOnlyList<(string Path, string Text)> sources,
        IEnumerable<MetadataReference> references, bool web, OutputKind kind, bool strict)
    {
        var token = TestContext.Current.CancellationToken;
        var usings = string.Join("\n", SdkUsings.Concat(web ? WebUsings : []).Select(name => $"global using global::{name};"));
        var trees = sources.Select(source => CSharpSyntaxTree.ParseText(source.Text, ParseOptions, source.Path, cancellationToken: token)).ToList();
        var compilation = CSharpCompilation.Create(name, trees.Append(CSharpSyntaxTree.ParseText(usings, ParseOptions, "GlobalUsings.g.cs", cancellationToken: token)),
            references, new CSharpCompilationOptions(kind, nullableContextOptions: NullableContextOptions.Enable)
                // A sample sees the internal members of the domain assembly too, as the domain sees its own (Versioning.Update).
                .WithMetadataImportOptions(strict ? MetadataImportOptions.Public : MetadataImportOptions.All));
        if (strict)
        {
            var errors = compilation.GetDiagnostics(token)
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error && !GeneratorErrors.Contains(diagnostic.Id))
                .Select(diagnostic => diagnostic.ToString())
                .ToList();
            if (errors.Count > 0)
                throw new InvalidOperationException($"{name} does not compile for the architecture rules: {string.Join(" | ", errors.Take(10))}");
        }
        return trees.Select(tree => new SourceFile(tree.FilePath, tree, compilation.GetSemanticModel(tree))).ToList();
    }

    /// <summary>The full name of the type of a symbol (the type itself, or the type that declares a member), without type arguments.</summary>
    public static string TypeOf(ISymbol symbol) =>
        (symbol as ITypeSymbol ?? symbol.ContainingType)?.OriginalDefinition.ToDisplayString(FullName) ?? string.Empty;

    private static readonly SymbolDisplayFormat FullName = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.None);

    /// <summary>The type of a symbol and its base types, so that a derived clock or random source is still seen.</summary>
    public static IEnumerable<string> TypeAndBases(ISymbol symbol)
    {
        for (var type = symbol as ITypeSymbol ?? symbol.ContainingType; type is not null; type = type.BaseType)
            yield return TypeOf(type);
    }

    /// <summary>A member and the members it overrides, up to the first declaration.</summary>
    public static IEnumerable<ISymbol> SelfAndOverridden(ISymbol symbol)
    {
        for (ISymbol? current = symbol is IMethodSymbol { ReducedFrom: { } reduced } ? reduced : symbol; current is not null;)
        {
            yield return current;
            current = current switch
            {
                IMethodSymbol method => method.OverriddenMethod,
                IPropertySymbol property => property.OverriddenProperty,
                _ => null,
            };
        }
    }

    /// <summary>A member of a type of the given full name (or of a type derived from it), with one of the given names.</summary>
    public static bool IsMember(ISymbol symbol, string type, params string[] names) =>
        symbol is not ITypeSymbol && SelfAndOverridden(symbol).Any(member => names.Contains(member.Name, StringComparer.Ordinal) && TypeOf(member) == type);

    /// <summary>The type itself, a type derived from it, or any member of them.</summary>
    public static bool IsTypeOrMember(ISymbol symbol, string type) =>
        symbol is not INamespaceSymbol && TypeAndBases(symbol).Contains(type, StringComparer.Ordinal);

    /// <summary>A record of the model with a version: the interface itself or a type that implements it.</summary>
    public static bool IsVersionedRecord(ITypeSymbol? type) =>
        type is not null && (TypeOf(type) == "Topogenia.Domain.Model.IVersionedRecord" || type.AllInterfaces.Any(IsVersionedRecord));

    /// <summary>The domain folder that a symbol belongs to: the folder of its declaration, or the folder of its namespace.</summary>
    public static string? DomainFolderOf(ISymbol symbol)
    {
        if (symbol is INamespaceSymbol space)
            return FolderOfNamespace(space.ToDisplayString());
        var definition = symbol is IMethodSymbol { ReducedFrom: { } reduced } ? reduced : symbol.OriginalDefinition;
        if (definition is ILocalSymbol or IParameterSymbol or IRangeVariableSymbol or ILabelSymbol or ITypeParameterSymbol)
            return null;
        var path = definition.DeclaringSyntaxReferences.FirstOrDefault()?.SyntaxTree.FilePath;
        if (path is not null && path.StartsWith(DomainFolder, StringComparison.Ordinal))
        {
            var parts = path[DomainFolder.Length..].Split('/');
            return parts.Length >= 2 ? parts[0] : null;
        }
        return definition.ContainingNamespace is { } container ? FolderOfNamespace(container.ToDisplayString()) : null;
    }

    private static string? FolderOfNamespace(string name) =>
        name.StartsWith("Topogenia.Domain.", StringComparison.Ordinal) ? name["Topogenia.Domain.".Length..].Split('.')[0] : null;

    /// <summary>The closest member, local function or lambda that holds a node.</summary>
    public static SyntaxNode? FunctionOf(SyntaxNode node) =>
        node.AncestorsAndSelf().FirstOrDefault(ancestor => ancestor is BaseMethodDeclarationSyntax or LocalFunctionStatementSyntax
            or AnonymousFunctionExpressionSyntax or AccessorDeclarationSyntax or PropertyDeclarationSyntax or FieldDeclarationSyntax);
}
