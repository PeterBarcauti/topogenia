// FrameworkUseDetectors.cs
// Detectors behind the rules written as lists of what is admitted: the types and the namespaces that a file of the shipped code uses outside the solution, its calls by a name written in a text, the text that only some builds compile, the files that a build compiles and no rule reads, and the settings that change what it compiles (S-5.4-13, S-5.4-21, ADR 104).

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Topogenia.Architecture.Tests;

/// <summary>
/// A list of what is refused sees only what it names, and the framework is larger than any such list. These detectors
/// read everything a file uses, so that a rule compares it to what is admitted: one type, one namespace or one call
/// more than the list fails the rule, whatever it is.
/// </summary>
internal static class FrameworkUseDetectors
{
    /// <summary>The root of the namespaces of the solution: what the solution declares is held by the other rules.</summary>
    private const string Solution = "Topogenia";

    /// <summary>
    /// Every type a file uses, each once: the type of each resolved name (the type itself, the type that declares a
    /// member, the type that declares an extension method) and the type of each expression, converted or not (so that
    /// `var` hides nothing), with their type arguments, the elements of an array and the types they are nested in.
    /// </summary>
    public static IReadOnlyList<INamedTypeSymbol> Types(SourceFile file) => Read.GetValue(file.Tree, _ => TypesOf(file));

    /// <summary>The types of each file, read once: several rules ask for them.</summary>
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<SyntaxTree, IReadOnlyList<INamedTypeSymbol>> Read = [];

    private static IReadOnlyList<INamedTypeSymbol> TypesOf(SourceFile file)
    {
        var token = TestContext.Current.CancellationToken;
        var found = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var (_, symbol) in file.ResolvedNames())
        {
            var type = symbol switch
            {
                INamespaceSymbol or ILocalSymbol or IParameterSymbol or IRangeVariableSymbol or ILabelSymbol or ITypeParameterSymbol or IDiscardSymbol => null,
                IMethodSymbol { ReducedFrom: { } reduced } => reduced.ContainingType,
                ITypeSymbol named => named,
                _ => symbol.ContainingType,
            };
            found.UnionWith(Parts(type));
        }
        foreach (var expression in file.Root.DescendantNodes().OfType<ExpressionSyntax>())
        {
            var info = file.Model.GetTypeInfo(expression, token);
            found.UnionWith(Parts(info.Type));
            found.UnionWith(Parts(info.ConvertedType));
        }
        return [.. found];
    }

    private static IEnumerable<INamedTypeSymbol> Parts(ITypeSymbol? type) => type switch
    {
        null or IErrorTypeSymbol => [],
        IArrayTypeSymbol array => Parts(array.ElementType),
        IPointerTypeSymbol pointer => Parts(pointer.PointedAtType),
        INamedTypeSymbol named => named.TypeArguments.SelectMany(Parts).Concat(Parts(named.ContainingType)).Prepend(named.OriginalDefinition),
        _ => [],
    };

    /// <summary>The namespace of a type outside the solution, null for a type of the solution, of the global namespace or written by the compiler.</summary>
    private static string? SpaceOf(INamedTypeSymbol type)
    {
        if (type.IsAnonymousType || type.ContainingNamespace is not { IsGlobalNamespace: false } space)
            return null;
        var name = space.ToDisplayString();
        return name == Solution || name.StartsWith(Solution + ".", StringComparison.Ordinal) ? null : name;
    }

    /// <summary>The namespaces of the framework and of the libraries that a file uses, each once (<see cref="Types"/>).</summary>
    public static IEnumerable<string> Namespaces(SourceFile file) =>
        Types(file).Select(SpaceOf).OfType<string>().Distinct(StringComparer.Ordinal);

    /// <summary>The types of one namespace, exactly, that a file uses, by their full name, each once; a tuple is `System.ValueTuple`.</summary>
    public static IEnumerable<string> TypesIn(SourceFile file, string space) =>
        Types(file).Where(type => SpaceOf(type) == space).Select(type => type.IsTupleType ? "System.ValueTuple" : SemanticSources.TypeOf(type))
            .Distinct(StringComparer.Ordinal);

    /// <summary>
    /// The members a file uses of one type, or of every type of one namespace, as « Type.Member », each once: what a
    /// file does with a type that is admitted but has doors (the description of a type, of a method, the memory of a
    /// native call).
    /// </summary>
    public static IEnumerable<string> Members(SourceFile file, string typeOrNamespace) =>
        file.ResolvedNames().Select(pair => pair.Symbol is IMethodSymbol { ReducedFrom: { } reduced } ? reduced : pair.Symbol)
            .Where(symbol => symbol is IMethodSymbol or IPropertySymbol or IFieldSymbol or IEventSymbol && symbol.ContainingType is { } owner
                && (SemanticSources.TypeOf(owner) == typeOrNamespace || owner.ContainingNamespace?.ToDisplayString() == typeOrNamespace))
            .Select(symbol => $"{symbol.ContainingType.Name}.{symbol.Name}").Distinct(StringComparer.Ordinal);

    /// <summary>
    /// The members that call, load or make something found by a name written in a text or held in a value, which no rule
    /// on the resolved symbols can follow: a method or a constructor invoked through its description, a member invoked
    /// by its name, a delegate made from a description, an assembly loaded or run, a type made by the name of its
    /// assembly, an expression compiled at the run, and the types of an assembly listed or found by their name.
    /// </summary>
    private static readonly (string Type, string[] Members)[] LateMembers =
    [
        ("System.Type", ["InvokeMember"]),
        ("System.Reflection.MethodBase", ["Invoke"]), ("System.Reflection.ConstructorInfo", ["Invoke"]), ("System.Reflection.MethodInfo", ["CreateDelegate"]),
        ("System.Reflection.PropertyInfo", ["GetValue", "SetValue"]),
        ("System.Delegate", ["DynamicInvoke", "CreateDelegate"]),
        ("System.Reflection.Assembly", ["Load", "LoadFrom", "LoadFile", "UnsafeLoadFrom", "LoadWithPartialName", "ReflectionOnlyLoad", "ReflectionOnlyLoadFrom",
            "LoadModule", "CreateInstance", "GetType", "GetTypes", "GetExportedTypes", "DefinedTypes", "ExportedTypes"]),
        ("System.AppDomain", ["ExecuteAssembly", "ExecuteAssemblyByName", "Load", "CreateInstance", "CreateInstanceAndUnwrap", "CreateInstanceFrom",
            "CreateInstanceFromAndUnwrap"]),
        ("System.Linq.Expressions.LambdaExpression", ["Compile"]), ("System.Linq.Expressions.Expression", ["Compile"]),
    ];

    /// <summary>The types that are such a call whole: the maker of objects by their type or by the file of their assembly, the loaders of assemblies.</summary>
    private static readonly string[] LateTypes =
        ["System.Activator", "System.Runtime.Loader.AssemblyLoadContext", "System.Runtime.Loader.AssemblyDependencyResolver"];

    /// <summary>The namespace that writes code at the run.</summary>
    private const string Emitted = "System.Reflection.Emit";

    /// <summary>
    /// Every call of a file by a name written in a text (<see cref="LateMembers"/>, <see cref="LateTypes"/>, a type found
    /// by its name with the static `Type.GetType`, code written at the run), as « path:line Type.Member ».
    /// </summary>
    public static IEnumerable<string> LateBindings(SourceFile file)
    {
        foreach (var (name, symbol) in file.ResolvedNames())
        {
            if (symbol is INamespaceSymbol or ILocalSymbol or IParameterSymbol or IRangeVariableSymbol or ILabelSymbol or ITypeParameterSymbol or IDiscardSymbol)
                continue;
            var late = LateMembers.Any(entry => SemanticSources.IsMember(symbol, entry.Type, entry.Members))
                || LateTypes.Any(type => SemanticSources.IsTypeOrMember(symbol, type))
                || (symbol is IMethodSymbol { IsStatic: true, Name: "GetType" } && SemanticSources.TypeOf(symbol) == "System.Type")
                || (symbol as ITypeSymbol ?? symbol.ContainingType)?.ContainingNamespace?.ToDisplayString() == Emitted;
            if (late)
                yield return $"{file.Where(name)} {(symbol is ITypeSymbol type ? type.Name : $"{symbol.ContainingType.Name}.{symbol.Name}")}";
        }
    }

    /// <summary>
    /// The text of a C# file that only some builds compile: a directive that keeps or leaves out lines by a symbol of
    /// the build, or that defines one, and the lines such a directive left out. The rules read the sources with no
    /// symbol of any build: what stands under one is compiled by a build that defines it and read by no rule. As
    /// « path:line text », the first line of the text.
    /// </summary>
    public static IEnumerable<string> BuildConditions(string path, string csharp)
    {
        var token = TestContext.Current.CancellationToken;
        return CSharpSyntaxTree.ParseText(csharp, cancellationToken: token).GetRoot(token).DescendantTrivia()
            .Where(trivia => trivia.Kind() is SyntaxKind.IfDirectiveTrivia or SyntaxKind.ElifDirectiveTrivia or SyntaxKind.ElseDirectiveTrivia or SyntaxKind.EndIfDirectiveTrivia
                or SyntaxKind.DefineDirectiveTrivia or SyntaxKind.UndefDirectiveTrivia or SyntaxKind.DisabledTextTrivia or SyntaxKind.BadDirectiveTrivia)
            .Select(trivia => $"{path}:{trivia.GetLocation().GetLineSpan().StartLinePosition.Line + 1} {trivia.ToString().Trim().Split('\n')[0].Trim()}");
    }

    /// <summary>
    /// The kinds of file that a build of .NET may compile into an assembly, by their extension, whatever its case (a
    /// build on Windows matches `*.cs` so): C#, the pages and components that the web SDK turns into C# (`.razor`,
    /// `.cshtml`), and the two other languages of the framework.
    /// </summary>
    private static readonly string[] Compilable = [".cs", ".razor", ".cshtml", ".vb", ".fs"];

    /// <summary>
    /// The files, among paths relative to the root, that a build could compile and that the rules do not read: a file
    /// of a kind of <see cref="Compilable"/> outside the output of a project (`bin` and `obj` at its root) that is not
    /// among <paramref name="read"/>. A C# file in a folder named `obj` below the root of its project, in a folder
    /// that the web SDK alone leaves out, or a page that the web SDK compiles, is one: whatever stands there is
    /// compiled, shipped and seen by no rule.
    /// </summary>
    public static IEnumerable<string> CompiledUnread(IEnumerable<string> paths, IReadOnlySet<string> read) =>
        paths.Where(IsCompiled).Where(path => !read.Contains(path));

    /// <summary>A file of a kind of <see cref="Compilable"/> outside the output of a project, by its path relative to the root.</summary>
    private static bool IsCompiled(string path) =>
        Compilable.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase) && !RepositoryFiles.IsProjectOutput(path);

    /// <summary>The lines of a `.gitignore` that say something: neither empty nor a comment.</summary>
    private static IEnumerable<string> IgnoreLines(string gitignore) =>
        gitignore.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0 && !line.StartsWith('#'));

    /// <summary>
    /// The lines of a `.gitignore` that the rule cannot read. It reads a name or a path, with a slash at its end for a
    /// folder, and an extension (`*.log`); any other writing (another wildcard, a line that takes a path back in) is
    /// named here, so that the rule fails instead of guessing.
    /// </summary>
    public static IEnumerable<string> UnreadIgnoreLines(string gitignore) =>
        IgnoreLines(gitignore).Where(line => line.TrimStart('*').Any(sign => "*?[]!\\".Contains(sign, StringComparison.Ordinal))
            || (line.StartsWith('*') && !line.StartsWith("*.", StringComparison.Ordinal)));

    /// <summary>Whether a line of a `.gitignore` keeps a path out: an extension, a path from the root (a slash inside), or a name at any depth, of a folder when a slash ends it.</summary>
    private static bool KeepsOut(string line, string path)
    {
        var body = line.Trim('/');
        if (body.StartsWith("*.", StringComparison.Ordinal))
            return path.EndsWith(body[1..], StringComparison.OrdinalIgnoreCase);
        if (body.Contains('/', StringComparison.Ordinal))
            return path.StartsWith(body + "/", StringComparison.OrdinalIgnoreCase) || path.Equals(body, StringComparison.OrdinalIgnoreCase);
        var parts = path.Split('/');
        return (line.EndsWith('/') ? parts[..^1] : parts).Contains(body, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The files, among paths relative to the root, that a build could compile and that `.gitignore` keeps out of the
    /// repository, as `path (line)`: a C# file under a folder named `obj`, `bin` or `node_modules` below the root
    /// of its project is compiled and shipped by the machine that builds, and no one who reads the repository sees it.
    /// The names are compared whatever their case, as git does on Windows.
    /// </summary>
    public static IEnumerable<string> CompiledKeptOut(IEnumerable<string> paths, string gitignore)
    {
        var lines = IgnoreLines(gitignore).ToList();
        return paths.Where(IsCompiled).SelectMany(path => lines.Where(line => KeepsOut(line, path)).Take(1).Select(line => $"{path} ({line})"));
    }

    /// <summary>
    /// What a file of settings of the build may not hold, by the name of the element, whatever its case (MSBuild reads
    /// the names of items and of properties so): a source file added or left out, a symbol defined, another file of
    /// settings read; and what moves the output of a project or the sources it takes by default, on which the rules
    /// rely to leave `bin` and `obj` out and to read everything else. This is a list of names: a property passed on
    /// the command line, or a task of a package, is not read here.
    /// </summary>
    private static readonly string[] RefusedSettings =
    [
        "Compile", "DefineConstants", "EnableDefaultCompileItems", "Import", "EnableDefaultItems", "DefaultItemExcludes", "DefaultExcludesInProjectFolder",
        "DefaultLanguageSourceExtension", "BaseOutputPath", "BaseIntermediateOutputPath",
    ];

    /// <summary>The elements of <see cref="RefusedSettings"/> that a file of settings of the build holds, at any depth (in a target too), as `path Element`.</summary>
    public static IEnumerable<string> RefusedBuildSettings(string path, string xml) =>
        System.Xml.Linq.XDocument.Parse(xml).Descendants().Where(element => RefusedSettings.Contains(element.Name.LocalName, StringComparer.OrdinalIgnoreCase))
            .Select(element => $"{path} {element.Name.LocalName}");
}
