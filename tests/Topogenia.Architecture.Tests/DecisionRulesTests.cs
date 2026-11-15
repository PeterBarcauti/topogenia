// DecisionRulesTests.cs
// Rules that keep the architecture decisions D07, D08, D12 and D13 held in every source, with their faulty samples.

using System.Text.RegularExpressions;
using Topogenia.Persistence.Schema;
using Topogenia.Persistence.SqlServer;

namespace Topogenia.Architecture.Tests;

public sealed class DecisionRulesTests
{
    [Fact]
    public void Identifiers_are_never_random_guids()
    {
        Assert.Empty(SemanticDetectors.Offenders(SemanticSources.Production, SemanticDetectors.RandomGuids));
    }

    /// <summary>
    /// D07, D08 in the domain: a rule takes its time and its identifiers from the operation context only, so the
    /// identifier factory, the clock and every source of time or chance (Guid.NewGuid, CreateVersion7, DateTime.Now,
    /// UtcNow, Today, DateTimeOffset, Stopwatch, Random, Environment.TickCount, TimeProvider) appear in one file, read on
    /// the resolved symbols (using static directives, aliases and method groups included). The context holds the
    /// factory (positive control).
    /// </summary>
    [Fact]
    public void Only_the_operation_context_reads_the_clock_or_creates_identifiers_in_the_domain()
    {
        CoreSources.Require();
        const string context = SemanticSources.DomainFolder + "Workspace/OperationContext.cs";
        Assert.Empty(SemanticDetectors.Offenders(SemanticSources.Domain, SemanticDetectors.ClockOrIdentifierFactories, context));
        Assert.NotEmpty(SemanticSources.Domain.Where(file => file.Path == context).SelectMany(SemanticDetectors.ClockOrIdentifierFactories));
    }

    [Fact]
    public void Dates_are_never_built_without_their_utc_kind()
    {
        var offenders = RepositoryFiles.ProductionCSharp()
            .Where(file => SourceDetectors.BuildsNonUtcDate(File.ReadAllText(file)))
            .Select(RepositoryFiles.Relative)
            .ToList();
        Assert.Empty(offenders);
    }

    [Fact]
    public void Code_comments_are_english()
    {
        var offenders = RepositoryFiles.AllCSharp()
            .Where(file => SourceDetectors.HasFrenchComment(File.ReadAllText(file)))
            .Select(RepositoryFiles.Relative)
            .ToList();
        Assert.Empty(offenders);
    }

    /// <summary>
    /// D13 in build scripts, project files and CI: the name appears only as a project identifier (Topogenia.X); the texts
    /// and settings of build/ that ship in the ZIP write it as a token that build/package.mjs replaces.
    /// </summary>
    [Fact]
    public void Build_and_project_files_derive_the_product_name()
    {
        var name = Topogenia.Application.ProductIdentity.Name;
        var offenders = RepositoryFiles.Enumerate(["build", "src", "tests", ".github"], ".csproj", ".props", ".proj", ".ps1", ".cmd", ".bat", ".yml", ".mjs")
            .Concat(RepositoryFiles.Enumerate(["build"], ".txt", ".json").Where(file => !file.EndsWith("packages.lock.json", StringComparison.Ordinal)))
            .Concat(RepositoryFiles.Shallow([""], ".props"))
            .Where(file => Regex.IsMatch(File.ReadAllText(file), Regex.Escape(name) + @"(?!\.[A-Z])", RegexOptions.IgnoreCase))
            .Select(RepositoryFiles.Relative)
            .ToList();
        Assert.Empty(offenders);
    }

    /// <summary>
    /// ADR 013: the technical names are the root of the namespaces, the name of the project of the persistence tests and
    /// the schema and the roles that the migrations froze; the displayed product name may change without them.
    /// </summary>
    [Fact]
    public void The_technical_names_are_those_of_the_solution_and_of_the_migrations()
    {
        Assert.Equal(TechnicalNames.Root, typeof(TechnicalNames).Namespace!.Split('.')[0]);
        var project = Path.Combine(RepositoryFiles.Root, "tests", TechnicalNames.PersistenceTests, TechnicalNames.PersistenceTests + ".csproj");
        Assert.True(File.Exists(project), project);
        Assert.DoesNotContain(System.Xml.Linq.XDocument.Load(project).Descendants(), element => element.Name.LocalName == "AssemblyName");
        var migrations = string.Join('\n', RepositoryFiles.Enumerate(["src/Topogenia.Persistence.SqlServer/Migrations"], ".cs")
            .Where(file => !file.EndsWith(".Designer.cs", StringComparison.Ordinal)).Select(File.ReadAllText));
        Assert.Contains(TechnicalNames.Schema, Regex.Matches(migrations, @"EnsureSchema\(\s*name:\s*""([^""]+)""").Select(match => match.Groups[1].Value));
        Assert.Equal([SqlServerAccess.ApplicationRole, SqlServerAccess.ReportRole],
            Regex.Matches(migrations, @"CREATE ROLE (\w+)").Select(match => match.Groups[1].Value).Distinct().Order(StringComparer.Ordinal));
        Assert.Equal(TechnicalNames.Schema, SqlServerEngine.Schema);
        Assert.Equal(TechnicalNames.Schema + "_app", SqlServerAccess.ApplicationRole);
        Assert.Equal(TechnicalNames.Schema + "_bi", SqlServerAccess.ReportRole);
    }

    [Theory]
    [InlineData("class A { System.Guid g = System.Guid.NewGuid(); }", true)]
    [InlineData("class A { System.Guid g = Guid.NewGuid(); }", true)]
    [InlineData("class A { System.Guid g = System.Guid.CreateVersion7(); }", false)]
    public void The_random_guid_detector_works(string sample, bool expected) =>
        Assert.Equal(expected, SourceDetectors.CreatesRandomGuid(sample));

    [Theory]
    [InlineData("class A { object d = new System.DateTime(2026, 1, 2); }", true)]
    [InlineData("class A { object d = x.ToLocalTime(); }", true)]
    [InlineData("class A { object k = System.DateTimeKind.Local; }", true)]
    [InlineData("class A { object z = TimeZoneInfo.Local; }", true)]
    [InlineData("class A { object d = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc); }", false)]
    public void The_non_utc_date_detector_works(string sample, bool expected) =>
        Assert.Equal(expected, SourceDetectors.BuildsNonUtcDate(sample));

    [Theory]
    [InlineData("class A { } // Vérifie que la case est libre", true)]
    [InlineData("class A { } // la case est libre dans le couloir", true)]
    [InlineData("/// <summary>Checks that the cell is free.</summary>\nclass A { }", false)]
    [InlineData("class A { } // Gets the cell of the step", false)]
    public void The_french_comment_detector_works(string sample, bool expected) =>
        Assert.Equal(expected, SourceDetectors.HasFrenchComment(sample));
}
