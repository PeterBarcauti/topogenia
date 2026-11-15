// TestHostDetectors.cs
// Detector, on the text of a test file, of each place that builds the host of the product and of whether it gives that host a shell that records in the place of the one of Windows (ADR 104: no test opens a real Explorer).

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Topogenia.Architecture.Tests;

/// <summary>A place of a test file that builds the host: its line, how it builds it, and whether the host gets a shell that records.</summary>
internal sealed record TestHost(int Line, string How, bool Recorded);

/// <summary>
/// The test projects are not compiled by the rules: the text is read by its syntax. A host is built by the entry points
/// of the product that take the configuration of a test (<see cref="Builders"/>) and by the factory of ASP.NET Core
/// (`WebApplicationFactory`). The desktop mode registers the shell of Windows only when none is there: a test gives its
/// own before, in the configuration it hands to the entry point. Whatever the mode and whether the host listens or not,
/// each place must give one: the rule asks no judgement on what a host may come to serve.
/// </summary>
internal static class TestHostDetectors
{
    /// <summary>The entry points of the product that build the host, and the rank of the argument that configures it.</summary>
    private static readonly (string Type, string Method, int Configure)[] Builders =
        [("HostApplication", "Build", 1), ("Program", "RunAsync", 2), ("HostCommands", "RunAsync", 2)];

    /// <summary>The parts of the host that only its own composition calls: a test that calls one builds a host by hand.</summary>
    private static readonly string[] Composition = ["AddTopogenia", "UseTopogenia", "AddDesktop", "AddServer"];

    /// <summary>The doubles of the shell: the recorder of the tests of the host, the one of the journeys.</summary>
    private static readonly string[] Recorders = ["FolderRecorder", "JourneyFolders"];

    private const string Factory = "WebApplicationFactory";

    private const string JourneyServices = "JourneyServicesFactory";

    private const int Depth = 3;

    /// <summary>
    /// Every place of a test file that builds the host. Recorded: the configuration given is a lambda, or a method of the
    /// same file that returns one, that registers a double as the shell (`AddSingleton&lt;IFolderShell&gt;(...)` given a
    /// <see cref="Recorders"/>, or the services of the journeys), itself or through a method of the same file. A place
    /// that names an entry point any other way (no configuration, a method group, an alias, a derived factory, a part of
    /// the composition called by hand) is not recorded: the rule cannot read it.
    /// </summary>
    public static IReadOnlyList<TestHost> Hosts(string csharp)
    {
        var token = TestContext.Current.CancellationToken;
        var root = CSharpSyntaxTree.ParseText(csharp, cancellationToken: token).GetRoot(token);
        var hosts = new List<TestHost>();
        var read = new HashSet<SyntaxNode>();
        foreach (var call in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (call.Expression is not MemberAccessExpressionSyntax access || BuilderOf(access) is not { } builder)
                continue;
            read.Add(access);
            hosts.Add(new TestHost(LineOf(call), $"{builder.Type}.{builder.Method}", Configuration(call, builder.Configure) is { } given && Records(root, given, Depth)));
        }
        foreach (var creation in root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>().Where(creation => Last(creation.Type) == Factory))
        {
            var given = creation.Parent is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "WithWebHostBuilder", Parent: InvocationExpressionSyntax configured }
                ? configured.ArgumentList.Arguments.FirstOrDefault()?.Expression
                : null;
            hosts.Add(new TestHost(LineOf(creation), $"new {Factory}", given is not null && Records(root, given, Depth)));
        }
        hosts.AddRange(Unreadable(root, read).Select(node => new TestHost(LineOf(node), $"{node} named another way", false)));
        return [.. hosts.OrderBy(host => host.Line)];
    }

    private static int LineOf(SyntaxNode node) => node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

    /// <summary>The last identifier of a name or of a member access (`Topogenia.Host.Composition.HostApplication` is `HostApplication`).</summary>
    private static string? Last(SyntaxNode? name) => name switch
    {
        SimpleNameSyntax simple => simple.Identifier.ValueText,
        QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
        AliasQualifiedNameSyntax alias => alias.Name.Identifier.ValueText,
        MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
        _ => null,
    };

    private static (string Type, string Method, int Configure)? BuilderOf(MemberAccessExpressionSyntax access)
    {
        var found = Builders.Where(builder => builder.Method == access.Name.Identifier.ValueText && builder.Type == Last(access.Expression)).ToList();
        return found.Count == 0 ? null : found[0];
    }

    /// <summary>The argument that configures the host: named `configure`, else at its rank; null when it is not given.</summary>
    private static ExpressionSyntax? Configuration(InvocationExpressionSyntax call, int rank)
    {
        var arguments = call.ArgumentList.Arguments;
        var named = arguments.FirstOrDefault(argument => argument.NameColon?.Name.Identifier.ValueText == "configure");
        return named?.Expression ?? (arguments.Count > rank && arguments[rank].NameColon is null ? arguments[rank].Expression : null);
    }

    /// <summary>
    /// The names of an entry point that are no call read above: an alias or a `using static` of a type that builds the
    /// host, the type `HostApplication` named outside a call of `Build`, a builder taken as a method group, a class
    /// derived from the factory, a part of the composition of the host.
    /// </summary>
    private static IEnumerable<SyntaxNode> Unreadable(SyntaxNode root, HashSet<SyntaxNode> read)
    {
        foreach (var node in root.DescendantNodes())
        {
            var unreadable = node switch
            {
                UsingDirectiveSyntax directive => (directive.Alias is not null || directive.StaticKeyword.IsKind(SyntaxKind.StaticKeyword))
                    && Builders.Any(builder => builder.Type == Last(directive.NamespaceOrType)),
                MemberAccessExpressionSyntax access => !read.Contains(access) && BuilderOf(access) is not null,
                IdentifierNameSyntax { Identifier.ValueText: "HostApplication" } name => !name.Ancestors().Any(ancestor => ancestor is UsingDirectiveSyntax)
                    && !(name.Parent is MemberAccessExpressionSyntax owner && BuilderOf(owner) is not null)
                    && !(name.Parent is MemberAccessExpressionSyntax { Parent: MemberAccessExpressionSyntax outer } && BuilderOf(outer) is not null),
                SimpleBaseTypeSyntax parent => Last(parent.Type) == Factory,
                SimpleNameSyntax name => Composition.Contains(name.Identifier.ValueText, StringComparer.Ordinal),
                _ => false,
            };
            if (unreadable)
                yield return node;
        }
    }

    /// <summary>Whether the configuration given to an entry point registers a double as the shell.</summary>
    private static bool Records(SyntaxNode root, ExpressionSyntax given, int depth)
    {
        while (given is ParenthesizedExpressionSyntax inner)
            given = inner.Expression;
        return given switch
        {
            AnonymousFunctionExpressionSyntax lambda => Gives(root, lambda.Body, depth),
            InvocationExpressionSyntax { Expression: IdentifierNameSyntax method } when depth > 0 => MethodsNamed(root, method.Identifier.ValueText) is { Count: > 0 } declared
                && declared.All(body => body.DescendantNodesAndSelf().OfType<AnonymousFunctionExpressionSyntax>().Any(lambda => Gives(root, lambda.Body, depth - 1))),
            _ => false,
        };
    }

    /// <summary>The bodies of the methods and local functions of the file that bear a name.</summary>
    private static List<SyntaxNode> MethodsNamed(SyntaxNode root, string name) =>
        [.. root.DescendantNodes().Select(node => node switch
        {
            MethodDeclarationSyntax method when method.Identifier.ValueText == name => (SyntaxNode?)method.Body ?? method.ExpressionBody,
            LocalFunctionStatementSyntax local when local.Identifier.ValueText == name => (SyntaxNode?)local.Body ?? local.ExpressionBody,
            _ => null,
        }).OfType<SyntaxNode>()];

    /// <summary>
    /// Whether a body registers a double as the shell: `AddSingleton&lt;IFolderShell&gt;(...)` given a new
    /// <see cref="Recorders"/> or a variable of the file made as one, the services of the journeys given to the host
    /// (`UseServiceProviderFactory(new JourneyServicesFactory(...))`), or a call of a method of the same file that does.
    /// </summary>
    private static bool Gives(SyntaxNode root, SyntaxNode body, int depth)
    {
        foreach (var call in body.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>())
        {
            var argument = call.ArgumentList.Arguments.Count == 1 ? call.ArgumentList.Arguments[0].Expression : null;
            var registered = call.Expression switch
            {
                MemberAccessExpressionSyntax { Name: GenericNameSyntax { Identifier.ValueText: "AddSingleton", TypeArgumentList.Arguments: [var service] } } =>
                    Last(service) == "IFolderShell" && argument is not null && IsRecorder(root, argument),
                MemberAccessExpressionSyntax { Name.Identifier.ValueText: "UseServiceProviderFactory" } => argument is ObjectCreationExpressionSyntax made && Last(made.Type) == JourneyServices,
                IdentifierNameSyntax method when depth > 0 => MethodsNamed(root, method.Identifier.ValueText) is { Count: > 0 } declared
                    && declared.All(inner => Gives(root, inner, depth - 1)),
                _ => false,
            };
            if (registered)
                return true;
        }
        return false;
    }

    /// <summary>The class of the product that calls the shell of Windows.</summary>
    private const string Shell = "ShellFolderOpener";

    /// <summary>The members of that class a test may read: two constants and two functions that reach nothing, all static.</summary>
    private static readonly string[] ShellStatics = ["Verb", "Class", "ClassOf", "OnShellThread"];

    /// <summary>
    /// Every place of a test file that names the class of the shell otherwise than to read one of its static members
    /// (<see cref="ShellStatics"/>), as « line: what is written »: the class built (`new ShellFolderOpener()`), the type
    /// of a variable, of a field or of a parameter (which a `new()` then builds), a type given to a generic call (the
    /// container, a maker of objects), `typeof`, an alias, and its name written in a text (a type found by its name).
    /// Without an object of the class, its `Show` cannot be called, and no Explorer opens.
    /// </summary>
    public static IReadOnlyList<string> ShellBuilt(string csharp)
    {
        var token = TestContext.Current.CancellationToken;
        var root = CSharpSyntaxTree.ParseText(csharp, cancellationToken: token).GetRoot(token);
        var named = root.DescendantNodes().OfType<IdentifierNameSyntax>().Where(name => name.Identifier.ValueText == Shell && !ReadsStatic(name))
            .Select(name => (Line: LineOf(name), Written: name.Ancestors().First(node => node is StatementSyntax or MemberDeclarationSyntax or UsingDirectiveSyntax or CompilationUnitSyntax)));
        var texts = root.DescendantTokens().Where(text => text.IsKind(SyntaxKind.StringLiteralToken) || text.IsKind(SyntaxKind.InterpolatedStringTextToken)
                || text.IsKind(SyntaxKind.SingleLineRawStringLiteralToken) || text.IsKind(SyntaxKind.MultiLineRawStringLiteralToken))
            .Where(text => text.ValueText.Contains(Shell, StringComparison.Ordinal)).Select(text => (Line: LineOf(text.Parent!), Written: text.Parent!));
        return [.. named.Concat(texts).OrderBy(place => place.Line).Select(place => $"{place.Line}: {place.Written.ToString().ReplaceLineEndings(" ")}")];
    }

    /// <summary>The class named, alone or after its namespaces, only as what one of its static members is read on.</summary>
    private static bool ReadsStatic(IdentifierNameSyntax name)
    {
        SyntaxNode whole = name;
        while (whole.Parent is MemberAccessExpressionSyntax qualified && qualified.Name == whole)
            whole = qualified;
        return whole.Parent is MemberAccessExpressionSyntax access && access.Expression == whole && ShellStatics.Contains(access.Name.Identifier.ValueText, StringComparer.Ordinal);
    }

    /// <summary>A new double, or a variable of the file that every declaration of its name makes as one.</summary>
    private static bool IsRecorder(SyntaxNode root, ExpressionSyntax value) => value switch
    {
        ObjectCreationExpressionSyntax made => Recorders.Contains(Last(made.Type), StringComparer.Ordinal),
        IdentifierNameSyntax variable => root.DescendantNodes().OfType<VariableDeclaratorSyntax>()
            .Where(declared => declared.Identifier.ValueText == variable.Identifier.ValueText).ToList() is { Count: > 0 } declarations
            && declarations.All(declared => declared.Initializer?.Value is ObjectCreationExpressionSyntax made && Recorders.Contains(Last(made.Type), StringComparer.Ordinal)),
        _ => false,
    };
}
