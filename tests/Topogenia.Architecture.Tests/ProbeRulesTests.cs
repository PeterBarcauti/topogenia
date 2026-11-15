// ProbeRulesTests.cs
// The test probes of the providers stay for the tests: no type of the application uses them, and only the persistence tests see them (ADR 037).

using System.Runtime.CompilerServices;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnitV3;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using Assembly = System.Reflection.Assembly;

namespace Topogenia.Architecture.Tests;

/// <summary>
/// The SQL that the persistence tests need lives in the providers (spec 3.2), in their probes: `SqlServerAccessProbe`
/// (users without login, impersonation, effective rights, rights and objects changed by hand), `SqlServerOptionsProbe`
/// (options of the database removed by hand, a record of a migration refused), `SqliteSettingsProbe` (settings read back
/// from SQLite). They change or read a database as an administrator would, so the application must never reach them: a
/// probe is an internal static class of a provider, in a file named after it; a provider opens its internals to the
/// persistence tests only, with one `InternalsVisibleTo` at most, written in the file of one of its probes; and no type
/// outside the probes depends on one.
/// </summary>
public sealed class ProbeRulesTests
{
    private const string Provider = "src/Topogenia.Persistence.SqlServer/";

    /// <summary>A type of a probe, or one that the compiler generates inside it (a state machine, a closure; `+` or `/` before it).</summary>
    private static readonly System.Text.RegularExpressions.Regex ProbeType = new(@"Probe([+/].*)?$", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    private static readonly string[] Providers = [Provider, "src/Topogenia.Persistence.Sqlite/"];

    private static readonly string[] ProviderAssemblies = ["Topogenia.Persistence.SqlServer", "Topogenia.Persistence.Sqlite"];

    /// <summary>The friend of the providers: a fixed technical name, not the product name (ADR 013).</summary>
    private const string Tests = Topogenia.Persistence.Schema.TechnicalNames.PersistenceTests;

    private static readonly Assembly[] Sources =
    [
        typeof(Topogenia.Domain.Errors.DomainException).Assembly, Assembly.Load("Topogenia.Application"), Assembly.Load("Topogenia.Persistence"),
        Assembly.Load("Topogenia.Persistence.SqlServer"), Assembly.Load("Topogenia.Persistence.Sqlite"), Assembly.Load("Topogenia.Interchange"),
        Assembly.Load(Topogenia.Application.ProductIdentity.Name),
    ];

    private static readonly ArchUnitNET.Domain.Architecture Solution = new ArchLoader().LoadAssemblies(Sources).Build();

    /// <summary>A test probe: a file `*Probe.cs` of a provider, outside its migrations.</summary>
    public static bool IsTestProbe(string relative)
    {
        ArgumentNullException.ThrowIfNull(relative);
        return Providers.Any(provider => relative.StartsWith(provider, StringComparison.Ordinal))
            && relative.EndsWith("Probe.cs", StringComparison.Ordinal) && !relative.Contains("/Migrations/", StringComparison.Ordinal);
    }

    /// <summary>The types that are not probes may not depend on a probe; the subjects are the types of <paramref name="scope"/>.</summary>
    internal static IArchRule NoUseOfProbes(ArchUnitNET.Fluent.Syntax.Elements.Types.GivenTypesConjunction scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        return scope.And().FollowCustomPredicate(type => !ProbeType.IsMatch(type.FullName), "are not probes")
            .Should().NotDependOnAny(Types(true).That().FollowCustomPredicate(type => ProbeType.IsMatch(type.FullName), "are probes"))
            .Because("a probe changes or reads a database as an administrator would, for the tests only (ADR 037)")
            .WithoutRequiringPositiveResults();
    }

    /// <summary>
    /// The <c>InternalsVisibleTo</c> that are not allowed: one from an assembly other than a provider, one to an assembly
    /// other than the persistence tests, or a second one of the same provider.
    /// </summary>
    public static IReadOnlyList<string> UnexpectedFriends(IEnumerable<(string Assembly, string Friend)> friends)
    {
        ArgumentNullException.ThrowIfNull(friends);
        var list = friends.ToList();
        return [.. list.Where((friend, index) => !ProviderAssemblies.Contains(friend.Assembly, StringComparer.Ordinal) || friend.Friend != Tests
                || list.Take(index).Any(earlier => earlier.Assembly == friend.Assembly))
            .Select(friend => $"{friend.Assembly} -> {friend.Friend}")];
    }

    [Fact]
    public void The_probes_are_internal_static_classes_of_the_providers_named_after_their_file()
    {
        var files = RepositoryFiles.Enumerate(["src"], ".cs").Select(RepositoryFiles.Relative)
            .Where(file => file.EndsWith("Probe.cs", StringComparison.Ordinal)).ToList();
        Assert.All(files, file => Assert.True(IsTestProbe(file), $"{file} is a probe outside the providers"));
        var probes = Sources.SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.Name.EndsWith("Probe", StringComparison.Ordinal) && !type.IsNested)
            .ToList();
        Assert.Contains(probes, type => type.Name == "SqlServerAccessProbe");
        Assert.Equal(files.Select(Path.GetFileNameWithoutExtension).Order(StringComparer.Ordinal), probes.Select(type => type.Name).Order(StringComparer.Ordinal));
        Assert.All(probes, type =>
        {
            Assert.Contains(type.Assembly.GetName().Name, ProviderAssemblies);
            Assert.False(type.IsPublic, $"{type.Name} is public");
            Assert.True(type.IsAbstract && type.IsSealed, $"{type.Name} is not a static class");
        });
    }

    /// <summary>
    /// A probe file holds its probe and nothing else: one type declared at the level of the file (a class, a record, a
    /// struct, an interface, an enum or a delegate, `file` types included), named after the file. The exemptions of a
    /// probe go to its file (<see cref="SourceRulesTests"/>) while the restriction of use goes to its type name
    /// (<see cref="NoUseOfProbes"/>): a second type in the file would have the first without the second.
    /// </summary>
    public static bool HoldsOnlyItsProbe(string fileName, string csharp)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        var root = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(csharp, cancellationToken: TestContext.Current.CancellationToken)
            .GetRoot(TestContext.Current.CancellationToken);
        var members = root.DescendantNodes(node => node is CompilationUnitSyntax or BaseNamespaceDeclarationSyntax)
            .OfType<MemberDeclarationSyntax>()
            .Where(member => member is not BaseNamespaceDeclarationSyntax)
            .ToList();
        var names = members.Select(member => member switch
        {
            BaseTypeDeclarationSyntax type => type.Identifier.ValueText,
            DelegateDeclarationSyntax type => type.Identifier.ValueText,
            _ => "",
        }).Distinct(StringComparer.Ordinal).ToList();
        return names.Count == 1 && names[0] == Path.GetFileNameWithoutExtension(fileName);
    }

    [Fact]
    public void A_probe_file_holds_only_the_probe_named_after_it()
    {
        var files = RepositoryFiles.Enumerate(["src"], ".cs").Where(file => IsTestProbe(RepositoryFiles.Relative(file))).ToList();
        Assert.NotEmpty(files);
        Assert.All(files, file => Assert.True(HoldsOnlyItsProbe(Path.GetFileName(file), File.ReadAllText(file)), $"{RepositoryFiles.Relative(file)} holds another type than its probe"));
    }

    /// <summary>The samples of the control of the architecture (gate P3, round 3): a second type beside the probe, or a type named otherwise.</summary>
    [Theory]
    [InlineData("internal static class SqlServerAccessProbe { } internal static class ReportSetup { }", false)]
    [InlineData("namespace N; internal static class SqlServerAccessProbe { } internal sealed record ReportSetup(string Sql);", false)]
    [InlineData("namespace N { internal static class SqlServerAccessProbe { } } namespace M { internal enum Mode { A } }", false)]
    [InlineData("namespace N; internal static class SqlServerAccessProbe { } file static class Helper { }", false)]
    [InlineData("namespace N; internal delegate void Setup(); internal static class SqlServerAccessProbe { }", false)]
    [InlineData("namespace N; internal static partial class SqlServerSchemaCatalog { }", false)]
    [InlineData("namespace N; internal static class SqlServerAccessProbe { private sealed record Grant(string Name); }", true)]
    [InlineData("namespace N; internal static partial class SqlServerAccessProbe { }", true)]
    public void A_probe_file_with_another_type_is_caught(string csharp, bool allowed) =>
        Assert.Equal(allowed, HoldsOnlyItsProbe("SqlServerAccessProbe.cs", csharp));

    [Fact]
    public void No_type_of_the_application_depends_on_a_probe() =>
        NoUseOfProbes(Types().That().ResideInAssembly(Sources[0], Sources[1..])).Check(Solution);

    [Fact]
    public void The_same_rule_catches_a_type_that_uses_a_probe()
    {
        var samples = typeof(Probes.SampleProbeUser);
        var architecture = new ArchLoader().LoadAssemblies(samples.Assembly).Build();
        Assert.False(NoUseOfProbes(Types().That().ResideInNamespace(samples.Namespace!)).HasNoViolations(architecture));
        Assert.True(NoUseOfProbes(Types().That().ResideInNamespace(samples.Namespace!).And().DoNotHaveName(samples.Name)).HasNoViolations(architecture));
    }

    [Fact]
    public void Src_opens_its_internals_only_from_a_probe_of_each_provider_to_the_persistence_tests()
    {
        var friends = Sources.SelectMany(assembly => assembly.GetCustomAttributes(typeof(InternalsVisibleToAttribute), false)
            .Cast<InternalsVisibleToAttribute>()
            .Select(attribute => (assembly.GetName().Name ?? "", attribute.AssemblyName)))
            .ToList();
        Assert.Empty(UnexpectedFriends(friends));
        Assert.Contains(friends, friend => friend.Item1 == "Topogenia.Persistence.SqlServer");
        var declarations = RepositoryFiles.Enumerate(["src"], ".cs", ".csproj", ".props", ".targets")
            .Concat(RepositoryFiles.Shallow([""], ".props", ".targets"))
            .Where(file => File.ReadAllText(file).Contains("InternalsVisibleTo", StringComparison.Ordinal))
            .Select(RepositoryFiles.Relative)
            .ToList();
        Assert.Equal(friends.Count, declarations.Count);
        Assert.All(declarations, file => Assert.True(IsTestProbe(file), $"{file} opens internals outside a probe"));
    }

    [Fact]
    public void A_friend_other_than_the_persistence_tests_or_a_second_one_is_caught()
    {
        var allowed = ("Topogenia.Persistence.SqlServer", Tests);
        Assert.Empty(UnexpectedFriends([allowed, ("Topogenia.Persistence.Sqlite", Tests)]));
        Assert.Equal(["Topogenia.Persistence -> " + Tests], UnexpectedFriends([allowed, ("Topogenia.Persistence", Tests)]));
        Assert.Single(UnexpectedFriends([("Topogenia.Persistence.SqlServer", "Topogenia.Host.Tests")]));
        Assert.Single(UnexpectedFriends([allowed, allowed]));
        Assert.True(IsTestProbe(Provider + "SqlServerAccessProbe.cs"));
        Assert.True(IsTestProbe("src/Topogenia.Persistence.Sqlite/Connection/SqliteSettingsProbe.cs"));
        Assert.False(IsTestProbe(Provider + "Migrations/SqlServerAccessProbe.cs"));
        Assert.False(IsTestProbe("src/Topogenia.Persistence/Schema/SchemaProbe.cs"));
    }
}
