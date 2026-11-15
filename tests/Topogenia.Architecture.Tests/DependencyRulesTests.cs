// DependencyRulesTests.cs
// Dependency rules between the projects of the solution (spec 3.2, D03), checked on the compiled assemblies.

using Assembly = System.Reflection.Assembly;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnitV3;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Topogenia.Architecture.Tests;

public sealed class DependencyRulesTests
{
    private static readonly Assembly Domain = typeof(Topogenia.Domain.Errors.DomainException).Assembly;
    private static readonly Assembly Application = Assembly.Load("Topogenia.Application");
    private static readonly Assembly Persistence = Assembly.Load("Topogenia.Persistence");
    private static readonly Assembly PersistenceSqlServer = Assembly.Load("Topogenia.Persistence.SqlServer");
    private static readonly Assembly PersistenceSqlite = Assembly.Load("Topogenia.Persistence.Sqlite");
    private static readonly Assembly Interchange = Assembly.Load("Topogenia.Interchange");
    private static readonly Assembly Host = Assembly.Load(Topogenia.Application.ProductIdentity.Name);

    private static readonly ArchUnitNET.Domain.Architecture Solution = new ArchLoader()
        .LoadAssemblies(Domain, Application, Persistence, PersistenceSqlServer, PersistenceSqlite, Interchange, Host)
        .Build();

    private static string[] SolutionReferences(Assembly assembly) =>
        assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? "")
            .Where(name => name.StartsWith("Topogenia.", StringComparison.Ordinal) || name == Topogenia.Application.ProductIdentity.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static bool IsBaseClassLibrary(string name) =>
        name is "System.Runtime" or "netstandard" or "mscorlib"
        || (name.StartsWith("System.", StringComparison.Ordinal) && !name.StartsWith("System.Data", StringComparison.Ordinal)
            && !name.StartsWith("System.Net", StringComparison.Ordinal) && !name.StartsWith("System.Diagnostics.Process", StringComparison.Ordinal))
        || name.StartsWith("Microsoft.Win32.", StringComparison.Ordinal);

    [Fact]
    public void Domain_references_no_other_project_and_no_package()
    {
        var references = Domain.GetReferencedAssemblies().Select(reference => reference.Name ?? "").ToArray();
        Assert.All(references, name => Assert.True(IsBaseClassLibrary(name), $"Domain references {name}"));
        Assert.DoesNotContain(references, name => name.StartsWith("System.Data", StringComparison.Ordinal));
    }

    [Fact]
    public void Application_references_only_the_domain()
    {
        Assert.All(SolutionReferences(Application), name => Assert.Equal("Topogenia.Domain", name));
        var references = Application.GetReferencedAssemblies().Select(reference => reference.Name ?? "");
        Assert.All(references, name => Assert.True(IsBaseClassLibrary(name) || name == "Topogenia.Domain", $"Application references {name}"));
    }

    /// <summary>
    /// Forbidden dependency rule for the given types. <c>Types(true)</c> includes the referenced framework types,
    /// without which the rule sees nothing. A forbidden item is a namespace (with its sub-namespaces) or, when it
    /// starts with "type:", a single type full name.
    /// </summary>
    internal static ArchUnitNET.Fluent.IArchRule ForbiddenDependencyRule(string forbidden, ArchUnitNET.Fluent.Syntax.Elements.Types.GivenTypesConjunction subjects)
    {
        var target = forbidden.StartsWith("type:", StringComparison.Ordinal)
            ? Types(true).That().HaveFullName(forbidden["type:".Length..])
            : Types(true).That().ResideInNamespaceMatching(System.Text.RegularExpressions.Regex.Escape(forbidden) + @"(\..*)?");
        return subjects.Should().NotDependOnAny(target)
            .Because("the domain and the application are independent of any technique and do no input or output (D03, spec 3.2, 3.3)")
            .WithoutRequiringPositiveResults();
    }

    /// <summary>Infrastructure, input/output and process-level types that neither the domain nor the application may use.</summary>
    public static TheoryData<string> ForbiddenForCore() =>
    [
        "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "Microsoft.Data", "Microsoft.Extensions", "System.Data",
        "System.IO", "System.Net", "System.Diagnostics.Process", "System.Reflection.Emit",
        "type:System.Console", "type:System.Environment", "type:System.AppDomain", "type:System.Diagnostics.Process",
    ];

    [Theory]
    [MemberData(nameof(ForbiddenForCore))]
    public void Domain_and_application_use_no_infrastructure_nor_io_type(string forbidden) =>
        ForbiddenDependencyRule(forbidden, CoreSources.ArePresent
            ? Types().That().ResideInAssembly(Domain, Application)
            // The obfuscated domain has lost the names by which its generated code is told apart: the application alone.
            : Types().That().ResideInAssembly(Application)).Check(Solution);

    /// <summary>
    /// ARC4-M2: the domain uses only the framework types of an allow list (DomainFrameworkDetectors), read on the resolved
    /// symbols of the compiled domain: names, members, attributes and the type of every expression. Neither System.Xml,
    /// System.IO, System.Net, System.Reflection, System.Runtime.InteropServices nor System.Threading are on it, nor
    /// Environment, AppContext, Console, GC, Activator, Random or the members of System.Type; extern and unsafe code are
    /// refused. The documented exception (SheetJson) is still used (positive control).
    /// </summary>
    [Fact]
    public void The_domain_uses_only_the_framework_types_of_its_allow_list()
    {
        CoreSources.Require();
        var offenders = SemanticDetectors.Offenders(SemanticSources.Domain, DomainFrameworkDetectors.ForeignUses);
        Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders.Distinct(StringComparer.Ordinal)));
        Assert.All(DomainFrameworkDetectors.ExceptedFiles, path =>
            Assert.Contains(SemanticSources.Domain, file => file.DomainPath == path));
    }

    /// <summary>
    /// The same refusals read on the compiled domain assembly, generated code included (the regular expressions of
    /// [GeneratedRegex]): a second net under the allow list for the namespaces and types that the domain never needs.
    /// </summary>
    public static TheoryData<string> ForbiddenForDomain() =>
    [
        "System.Xml", "System.Threading", "System.Runtime.InteropServices.Marshalling", "System.Resources",
        "type:System.AppContext", "type:System.Activator", "type:System.GC", "type:System.Random", "type:System.TimeZoneInfo",
        "type:System.TimeProvider", "type:System.DateTimeOffset", "type:System.Runtime.InteropServices.DllImportAttribute",
        "type:System.Runtime.InteropServices.LibraryImportAttribute", "type:System.Runtime.InteropServices.Marshal",
        "type:System.Reflection.Assembly", "type:System.Reflection.MethodInfo", "type:System.Reflection.PropertyInfo",
    ];

    [Theory]
    [MemberData(nameof(ForbiddenForDomain))]
    public void The_compiled_domain_uses_no_process_reflection_or_interop_type(string forbidden)
    {
        // Read on the assembly built from the sources: the obfuscated one has lost the names of its generated code.
        CoreSources.Require();
        ForbiddenDependencyRule(forbidden, Types().That().ResideInAssembly(Domain)).Check(Solution);
    }

    /// <summary>
    /// ADR 074: the domain is built from its sources (unsigned) where they are, and comes signed with the strong name of
    /// its holder from the package Topogenia.Core where they are not; a rule of the sources is never skipped otherwise.
    /// </summary>
    [Fact]
    public void The_domain_is_built_from_its_sources_or_comes_signed_from_its_package()
    {
        var token = Domain.GetName().GetPublicKeyToken() ?? [];
        Assert.Equal(!CoreSources.ArePresent, token.Length > 0);
    }

    [Theory]
    [InlineData("System.Data")]
    [InlineData("System.IO")]
    [InlineData("type:System.Console")]
    public void The_same_rule_catches_a_probe_that_breaks_it(string forbidden)
    {
        var probe = typeof(Probes.UsesSystemData).Assembly;
        var architecture = new ArchLoader().LoadAssemblies(probe).Build();
        var rule = ForbiddenDependencyRule(forbidden, Types().That().ResideInNamespace(typeof(Probes.UsesSystemData).Namespace!));
        Assert.False(rule.HasNoViolations(architecture));
    }

    [Fact]
    public void Persistence_and_interchange_do_not_reference_the_host()
    {
        foreach (var assembly in new[] { Persistence, PersistenceSqlServer, PersistenceSqlite, Interchange })
            Assert.DoesNotContain(Topogenia.Application.ProductIdentity.Name, SolutionReferences(assembly));
    }

    [Fact]
    public void Only_the_host_knows_aspnet_core()
    {
        foreach (var assembly in new[] { Domain, Application, Persistence, PersistenceSqlServer, PersistenceSqlite, Interchange })
            Assert.DoesNotContain(assembly.GetReferencedAssemblies(), reference =>
                (reference.Name ?? "").StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Topogenia.Domain")]
    [InlineData("Topogenia.Application", "Topogenia.Domain")]
    [InlineData("Topogenia.Persistence", "Topogenia.Application", "Topogenia.Domain")]
    [InlineData("Topogenia.Persistence.SqlServer", "Topogenia.Persistence")]
    [InlineData("Topogenia.Persistence.Sqlite", "Topogenia.Persistence")]
    [InlineData("Topogenia.Interchange", "Topogenia.Application", "Topogenia.Domain")]
    public void Project_references_are_exactly_the_allowed_ones(string project, params string[] allowed)
    {
        if (project == "Topogenia.Domain")
            CoreSources.Require();
        var file = Path.Combine(RepositoryFiles.Root, "src", project, project + ".csproj");
        var references = System.Xml.Linq.XDocument.Load(file).Descendants("ProjectReference")
            .Select(element => Path.GetFileNameWithoutExtension((string?)element.Attribute("Include") ?? ""))
            .Order(StringComparer.Ordinal);
        Assert.Equal(allowed.Order(StringComparer.Ordinal), references);
    }

    [Theory]
    [InlineData("Topogenia.Domain")]
    [InlineData("Topogenia.Application")]
    public void Domain_and_application_reference_no_package(string project)
    {
        if (project == "Topogenia.Domain")
            CoreSources.Require();
        var file = Path.Combine(RepositoryFiles.Root, "src", project, project + ".csproj");
        // ADR 074: the core itself, as a package, and only where the switch takes it instead of its source project.
        Assert.All(System.Xml.Linq.XDocument.Load(file).Descendants("PackageReference"), reference =>
        {
            Assert.Equal("Topogenia.Core", (string?)reference.Attribute("Include"));
            Assert.Equal("'$(UseCorePackage)' == 'true'", (string?)reference.Parent?.Attribute("Condition"));
        });
    }

    /// <summary>Spec 3.1 (S-3.1-09): the tests of the domain are unit tests without a database, the domain their only project.</summary>
    [Fact]
    public void The_domain_tests_reference_the_domain_alone_and_the_test_framework_alone()
    {
        CoreSources.Require();
        var file = System.Xml.Linq.XDocument.Load(Path.Combine(RepositoryFiles.Root, "tests", "Topogenia.Domain.Tests", "Topogenia.Domain.Tests.csproj"));
        Assert.Equal(["Topogenia.Domain"], file.Descendants("ProjectReference")
            .Select(element => Path.GetFileNameWithoutExtension((string?)element.Attribute("Include") ?? "")));
        Assert.Equal(["xunit.v3"], file.Descendants("PackageReference").Select(element => (string?)element.Attribute("Include") ?? ""));
    }

    /// <summary>
    /// A package (or a compiled reference) of an engine: an EF Core provider of SQL Server or SQLite, a driver
    /// (`Microsoft.Data.*`) or the native SQLite library. The common persistence knows the relational EF Core only; each
    /// provider brings its own (D02, ADR 002, ADR 025).
    /// </summary>
    public static bool IsEnginePackage(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return name.StartsWith("Microsoft.Data.", StringComparison.OrdinalIgnoreCase) || name.StartsWith("SQLitePCLRaw", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("Microsoft.EntityFrameworkCore.SqlServer", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("Microsoft.EntityFrameworkCore.Sqlite", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The packages that the common persistence may use, and no other: EF Core, its relational layer, their parts and the
    /// libraries that they bring (D02, ADR 002). A provider, a driver or a library of any engine is refused by default, its
    /// name whatever it is; a new package of EF Core is added here on purpose.
    /// </summary>
    public static bool IsAllowedPersistencePackage(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return PersistencePackages.Contains(name, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>EF Core 10 and its relational layer with what they bring, and the two projects below the persistence.</summary>
    private static readonly string[] PersistencePackages =
    [
        "Microsoft.EntityFrameworkCore", "Microsoft.EntityFrameworkCore.Abstractions", "Microsoft.EntityFrameworkCore.Analyzers",
        "Microsoft.EntityFrameworkCore.Relational", "Microsoft.Extensions.Caching.Abstractions", "Microsoft.Extensions.Caching.Memory",
        "Microsoft.Extensions.Configuration.Abstractions", "Microsoft.Extensions.DependencyInjection",
        "Microsoft.Extensions.DependencyInjection.Abstractions", "Microsoft.Extensions.Logging", "Microsoft.Extensions.Logging.Abstractions",
        "Microsoft.Extensions.Options", "Microsoft.Extensions.Primitives", "Topogenia.Application", "Topogenia.Domain",
        // The domain as its package, in the public repository (ADR 074)
        "Topogenia.Core",
    ];

    /// <summary>An assembly of the shared framework of .NET that runs the tests (System.Data.Common, never a driver).</summary>
    public static bool IsFrameworkAssembly(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return name.Length > 0 && name == Path.GetFileName(name)
            && File.Exists(Path.Combine(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(), name + ".dll"));
    }

    /// <summary>The samples of the control of the architecture (gate P3, round 3): providers and drivers of other engines.</summary>
    [Theory]
    [InlineData("Microsoft.EntityFrameworkCore", true)]
    [InlineData("Microsoft.EntityFrameworkCore.Relational", true)]
    [InlineData("Microsoft.EntityFrameworkCore.Abstractions", true)]
    [InlineData("Microsoft.Extensions.Logging", true)]
    [InlineData("topogenia.application", true)]
    [InlineData("Npgsql.EntityFrameworkCore.PostgreSQL", false)]
    [InlineData("Npgsql", false)]
    [InlineData("Pomelo.EntityFrameworkCore.MySql", false)]
    [InlineData("MySql.EntityFrameworkCore", false)]
    [InlineData("MySqlConnector", false)]
    [InlineData("Oracle.EntityFrameworkCore", false)]
    [InlineData("Oracle.ManagedDataAccess.Core", false)]
    [InlineData("System.Data.SQLite", false)]
    [InlineData("System.Data.SqlClient", false)]
    [InlineData("System.Data.Odbc", false)]
    [InlineData("FirebirdSql.EntityFrameworkCore.Firebird", false)]
    [InlineData("Microsoft.EntityFrameworkCore.InMemory", false)]
    [InlineData("Microsoft.EntityFrameworkCore.Cosmos", false)]
    [InlineData("Microsoft.EntityFrameworkCore.Sqlite.Core", false)]
    [InlineData("Microsoft.Extensions.Caching.SqlServer", false)]
    [InlineData("Dapper", false)]
    [InlineData("topogenia.persistence.sqlserver", false)]
    public void Only_the_packages_of_ef_core_are_allowed_in_the_common_persistence(string name, bool allowed) =>
        Assert.Equal(allowed, IsAllowedPersistencePackage(name) || IsFrameworkAssembly(name));

    [Theory]
    [InlineData("System.Runtime", true)]
    [InlineData("System.Data.Common", true)]
    [InlineData("System.Data.SqlClient", false)]
    [InlineData("System.Data.SQLite", false)]
    [InlineData("Microsoft.Data.SqlClient", false)]
    [InlineData("Npgsql", false)]
    [InlineData("", false)]
    [InlineData("../System.Runtime", false)]
    public void A_framework_assembly_is_one_of_the_shared_framework(string name, bool framework) =>
        Assert.Equal(framework, IsFrameworkAssembly(name));

    [Fact]
    public void The_common_persistence_references_no_package_nor_assembly_of_an_engine()
    {
        var file = Path.Combine(RepositoryFiles.Root, "src", "Topogenia.Persistence", "Topogenia.Persistence.csproj");
        var packages = System.Xml.Linq.XDocument.Load(file).Descendants()
            .Where(element => element.Name.LocalName is "PackageReference" or "Reference")
            .Select(element => (string?)element.Attribute("Include") ?? (string?)element.Attribute("Update") ?? "")
            .ToList();
        Assert.NotEmpty(packages);
        Assert.DoesNotContain(packages, IsEnginePackage);
        Assert.All(packages, package => Assert.True(IsAllowedPersistencePackage(package), $"Topogenia.Persistence references {package}"));
        var locked = LockedPackages(File.ReadAllText(Path.Combine(RepositoryFiles.Root, "src", "Topogenia.Persistence", "packages.lock.json")));
        Assert.Contains("Microsoft.EntityFrameworkCore.Relational", locked);
        Assert.DoesNotContain(locked, IsEnginePackage);
        Assert.All(locked, package => Assert.True(IsAllowedPersistencePackage(package), $"Topogenia.Persistence locks {package}"));
        var assemblies = Persistence.GetReferencedAssemblies().Select(reference => reference.Name ?? "").ToList();
        Assert.DoesNotContain(assemblies, IsEnginePackage);
        Assert.All(assemblies, name => Assert.True(IsAllowedPersistencePackage(name) || IsFrameworkAssembly(name), $"Topogenia.Persistence references the assembly {name}"));
    }

    /// <summary>
    /// The packages of a NuGet lock file: every package resolved for every target and every package it depends on. The
    /// lock sees what the project file does not: a package added by `Directory.Build.props` or brought by another one.
    /// </summary>
    public static IReadOnlyList<string> LockedPackages(string lockJson)
    {
        using var document = System.Text.Json.JsonDocument.Parse(lockJson);
        return [.. document.RootElement.GetProperty("dependencies").EnumerateObject()
            .SelectMany(target => target.Value.EnumerateObject())
            .SelectMany(package => package.Value.TryGetProperty("dependencies", out var dependencies)
                ? dependencies.EnumerateObject().Select(dependency => dependency.Name).Prepend(package.Name).ToList()
                : [package.Name])
            .Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    [Fact]
    public void An_engine_package_added_outside_the_project_file_is_seen_in_the_lock()
    {
        const string direct = """{ "version": 2, "dependencies": { "net10.0": { "Microsoft.Data.Sqlite": { "type": "CentralTransitive" } } } }""";
        const string brought = """{ "version": 2, "dependencies": { "net10.0": { "Some.Tool": { "type": "Direct", "dependencies": { "SQLitePCLRaw.core": "2.1.11" } } } } }""";
        Assert.Contains(LockedPackages(direct), IsEnginePackage);
        Assert.Contains(LockedPackages(brought), IsEnginePackage);
        Assert.DoesNotContain(LockedPackages("""{ "version": 2, "dependencies": { "net10.0": { "Microsoft.EntityFrameworkCore.Relational": { "type": "Direct" } } } }"""), IsEnginePackage);
    }

    [Theory]
    [InlineData("Microsoft.EntityFrameworkCore.SqlServer", true)]
    [InlineData("Microsoft.EntityFrameworkCore.Sqlite", true)]
    [InlineData("Microsoft.EntityFrameworkCore.Sqlite.Core", true)]
    [InlineData("Microsoft.Data.SqlClient", true)]
    [InlineData("Microsoft.Data.Sqlite", true)]
    [InlineData("SQLitePCLRaw.bundle_e_sqlite3", true)]
    [InlineData("Microsoft.EntityFrameworkCore.Relational", false)]
    [InlineData("Microsoft.EntityFrameworkCore", false)]
    public void An_engine_package_is_recognised(string name, bool engine) => Assert.Equal(engine, IsEnginePackage(name));

    [Fact]
    public void Compiled_assemblies_respect_the_same_rules()
    {
        Assert.All(SolutionReferences(Persistence), name => Assert.Contains(name, new[] { "Topogenia.Application", "Topogenia.Domain" }));
        Assert.DoesNotContain("Topogenia.Persistence.Sqlite", SolutionReferences(PersistenceSqlServer));
        Assert.DoesNotContain("Topogenia.Persistence.SqlServer", SolutionReferences(PersistenceSqlite));
        Assert.All(SolutionReferences(Interchange), name => Assert.Contains(name, new[] { "Topogenia.Application", "Topogenia.Domain" }));
    }

    /// <summary>
    /// Spec 3.2: an HTTP entry point holds no rule; it reads the request, calls a handler and translates the
    /// result. Entry points therefore depend on the application only, never on the domain rules, the
    /// persistence or the interchange formats.
    /// </summary>
    [Theory]
    [InlineData("Topogenia.Domain")]
    [InlineData("Topogenia.Persistence")]
    [InlineData("Topogenia.Interchange")]
    public void Http_entry_points_depend_on_handlers_only(string forbiddenNamespace)
    {
        Types().That().ResideInNamespaceMatching(@"Topogenia\.Host\.Endpoints(\..*)?")
            .Should().NotDependOnAny(Types().That().ResideInNamespaceMatching(forbiddenNamespace.Replace(".", @"\.", StringComparison.Ordinal) + @"(\..*)?"))
            .Because("an HTTP entry point contains no rule (spec 3.2)")
            .WithoutRequiringPositiveResults()
            .Check(Solution);
    }
}
