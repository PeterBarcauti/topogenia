// QualityRulesTests.cs
// Rules that keep the quality settings from being bypassed: single version, no warning suppression, no lint bypass, English names.

using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Topogenia.Architecture.Tests;

public sealed partial class QualityRulesTests
{
    private static string Read(string relative) => File.ReadAllText(Path.Combine(RepositoryFiles.Root, relative));

    private static readonly string[] SettingsOwnedByTheCommonProps =
        ["Version", "TargetFramework", "TargetFrameworks", "Nullable", "TreatWarningsAsErrors", "WarningsNotAsErrors", "NoWarn",
         "AnalysisLevel", "AnalysisMode", "EnforceCodeStyleInBuild", "LangVersion"];

    [Fact]
    public void The_version_number_is_written_once_and_the_interface_follows_it()
    {
        var version = XDocument.Parse(Read("Directory.Build.props")).Descendants("Version").Single().Value;
        Assert.Matches(@"^\d+\.\d+\.\d+$", version);
        using var package = JsonDocument.Parse(Read("web/package.json"));
        Assert.Equal(version, package.RootElement.GetProperty("version").GetString());
    }

    /// <summary>Spec 3.1, NF10: no project overrides the common settings (version, target, nullable, warnings, analysers).</summary>
    [Fact]
    public void No_project_overrides_the_common_settings()
    {
        var offenders = RepositoryFiles.Enumerate(["src", "tests"], ".csproj")
            .SelectMany(file => XDocument.Parse(File.ReadAllText(file)).Descendants()
                .Where(element => SettingsOwnedByTheCommonProps.Contains(element.Name.LocalName))
                .Select(element => $"{RepositoryFiles.Relative(file)}: {element.Name.LocalName}"))
            .ToList();
        Assert.Empty(offenders);
    }

    [Fact]
    public void Analysers_run_at_the_latest_level_and_the_only_global_suppression_is_documented()
    {
        var props = XDocument.Parse(Read("Directory.Build.props"));
        Assert.Equal("latest", props.Descendants("AnalysisLevel").Single().Value);
        Assert.Equal("$(NoWarn);CS1591", props.Descendants("NoWarn").Single().Value);
        Assert.Empty(props.Descendants("WarningsNotAsErrors"));
    }

    // The words are split so that this file does not match its own rule.
    [GeneratedRegex(@"#pra" + @"gma\s+warning\s+disable|\[(assembly:\s*)?Suppress" + @"Message\b|Suppress" + @"MessageAttribute")]
    private static partial Regex WarningSuppression();

    [Fact]
    public void No_warning_is_silenced_in_the_code()
    {
        var offenders = RepositoryFiles.AllCSharp()
            .Where(file => WarningSuppression().IsMatch(File.ReadAllText(file)))
            .Select(RepositoryFiles.Relative)
            .ToList();
        Assert.Empty(offenders);
    }

    /// <summary>The only rules turned off in .editorconfig are the documented ones (ADR 017).</summary>
    [Fact]
    public void Editorconfig_turns_off_only_documented_rules()
    {
        var silenced = Regex.Matches(Read(".editorconfig"), @"dotnet_diagnostic\.(\w+)\.severity\s*=\s*(none|silent|suggestion)")
            .Select(match => match.Groups[1].Value)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(["CA1707", "CA2007"], silenced);
        foreach (var rule in new[] { "IDE0005", "IDE0051", "IDE0052", "IDE0060", "IDE0130", "CA1812", "CA1823" })
            Assert.Contains($"dotnet_diagnostic.{rule}.severity = warning", Read(".editorconfig"), StringComparison.Ordinal);
    }

    [GeneratedRegex(@"eslint-disable|@ts-ignore|@ts-nocheck|@ts-expect-error")]
    private static partial Regex LintBypass();

    [Fact]
    public void No_lint_or_type_check_is_bypassed_in_the_interface()
    {
        var offenders = RepositoryFiles.Enumerate(["web/src", "web/e2e", "web/scripts"], ".ts", ".tsx", ".mjs")
            .Concat(RepositoryFiles.Shallow(["web"], ".ts", ".mjs"))
            .Where(file => LintBypass().IsMatch(File.ReadAllText(file)))
            .Select(RepositoryFiles.Relative)
            .ToList();
        Assert.Empty(offenders);
    }

    [Fact]
    public void The_lint_configuration_keeps_the_size_limits()
    {
        var config = Read("web/eslint.config.mjs");
        Assert.Matches(@"'max-lines':\s*\['error',\s*\{\s*max:\s*600\s*\}\]", config);
        Assert.Matches(@"'max-lines-per-function':\s*\['error',\s*\{\s*max:\s*80\s*\}\]", config);
        Assert.Matches(@"'max-lines':\s*\['error',\s*\{\s*max:\s*250\s*\}\]", config);
        Assert.DoesNotMatch(@"'max-lines(-per-function)?':\s*'off'", config);
        var limits = Regex.Matches(config, @"'max-lines(-per-function)?':\s*\['error',\s*\{\s*max:\s*(\d+)").Select(match => int.Parse(match.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture));
        Assert.All(limits, limit => Assert.Contains(limit, new[] { 80, 250, 600 }));
    }

    [GeneratedRegex(@"(Auteur|Author|Date)\s*:", RegexOptions.IgnoreCase)]
    private static partial Regex AuthorOrDate();

    /// <summary>Spec 0.2: the two header lines carry the name and the description, never an author nor a date.</summary>
    [Fact]
    public void Headers_carry_no_author_nor_date()
    {
        var offenders = RepositoryFiles.Enumerate(["src", "tests", "web/src", "web/e2e", "web/scripts", "build", "docs", "samples", ".github"],
                ".cs", ".csproj", ".props", ".proj", ".ts", ".tsx", ".css", ".mjs", ".html", ".md", ".ps1", ".cmd", ".bat", ".yml", ".yaml", ".sql")
            .Concat(RepositoryFiles.Shallow(["", "web"], ".md", ".props", ".mjs", ".ts", ".html", ".yml"))
            .Where(file => File.ReadLines(file).Take(3).Any(line => AuthorOrDate().IsMatch(line)))
            .Select(RepositoryFiles.Relative)
            .ToList();
        Assert.Empty(offenders);
    }

    /// <summary>French words of the 1.x code (glossary of spec 3.3) that must not reappear in identifiers (D12).</summary>
    private static readonly string[] FrenchWords =
    [
        "Projet", "Carte", "Couloir", "Etape", "Fleche", "Lien", "Fiche", "Deroule", "Irritant", "Remontee", "Reliquat", "Cadre",
        "Animateur", "Contributeur", "Lecteur", "Corbeille", "Membre", "Droits", "Colonne", "Rangee", "Libelle", "Traduction",
        "Langue", "Utilisateur", "Sauvegarde", "Disposition", "Rattacher", "Regrouper", "Recoudre", "Detailler", "Magasin", "Historique",
        "Annuler", "Retablir", "Couper", "Coller", "Supprimer", "Ajouter", "Modifier", "Enregistrer", "Valider", "Ouvrir", "Fermer",
        "Fichier", "Deplacer", "Renommer", "Creer", "Brouillon", "Acteur", "Cartes", "Etapes", "Couloirs", "Liens", "Fiches",
    ];

    private static IEnumerable<string> Words(string identifier) =>
        Regex.Split(identifier, @"(?<=[a-z0-9])(?=[A-Z])|_").Where(word => word.Length > 0);

    internal static bool HasFrenchWord(string identifier) =>
        Words(identifier).Any(word => FrenchWords.Contains(word, StringComparer.OrdinalIgnoreCase));

    [Fact]
    public void Csharp_identifiers_are_english()
    {
        var offenders = RepositoryFiles.AllCSharp()
            .SelectMany(file => CSharpSyntaxTree.ParseText(File.ReadAllText(file), cancellationToken: TestContext.Current.CancellationToken)
                .GetRoot(TestContext.Current.CancellationToken).DescendantTokens()
                .Where(token => token.IsKind(SyntaxKind.IdentifierToken) && HasFrenchWord(token.ValueText))
                .Select(token => $"{RepositoryFiles.Relative(file)}: {token.ValueText}"))
            .Distinct()
            .ToList();
        Assert.Empty(offenders);
    }

    [GeneratedRegex(@"""(?:\\.|[^""\\])*""|'(?:\\.|[^'\\])*'|`(?:\\.|[^`\\])*`|//[^\n]*|/\*[\s\S]*?\*/")]
    private static partial Regex TypeScriptStringsAndComments();

    [GeneratedRegex(@"\b[A-Za-z_$][\w$]*\b")]
    private static partial Regex TypeScriptWord();

    [Fact]
    public void Typescript_identifiers_are_english()
    {
        var offenders = RepositoryFiles.Enumerate(["web/src", "web/e2e", "web/scripts"], ".ts", ".tsx", ".mjs")
            .SelectMany(file => TypeScriptWord().Matches(TypeScriptStringsAndComments().Replace(File.ReadAllText(file), " "))
                .Select(match => match.Value)
                .Where(HasFrenchWord)
                .Select(word => $"{RepositoryFiles.Relative(file)}: {word}"))
            .Distinct()
            .ToList();
        Assert.Empty(offenders);
    }

    [Theory]
    [InlineData("ServiceStructure", false)]
    [InlineData("AjouterCouloir", true)]
    [InlineData("etape_id", true)]
    [InlineData("laneHeaders", false)]
    [InlineData("magasinCarte", true)]
    public void The_french_word_detector_works(string identifier, bool french) => Assert.Equal(french, HasFrenchWord(identifier));

    /// <summary>
    /// D13, ADR 013: the settings delivered with the application name no section after the product, whose name the code
    /// gives (`ProductOptions.Section`): renaming the product never leaves a section unread (the defaults live in the code).
    /// </summary>
    [Fact]
    public void The_delivered_settings_carry_no_section_named_after_the_product()
    {
        var name = Topogenia.Application.ProductIdentity.Name;
        var offenders = RepositoryFiles.Enumerate(["src"], ".json")
            .Where(file => Path.GetFileName(file).StartsWith("appsettings", StringComparison.OrdinalIgnoreCase))
            .Where(file => System.Text.Json.JsonDocument.Parse(File.ReadAllText(file)).RootElement.EnumerateObject()
                .Any(property => string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)))
            .Select(RepositoryFiles.Relative)
            .ToList();
        Assert.Contains(RepositoryFiles.Enumerate(["src"], ".json"), file => Path.GetFileName(file) == "appsettings.json");
        Assert.Empty(offenders);
    }

    /// <summary>
    /// D13: the example of the server settings that ships (build/server) names its section by the token that
    /// build/package.mjs replaces with the name of the build, the section that the code reads (`ProductOptions.Section`).
    /// </summary>
    [Fact]
    public void The_example_of_the_server_settings_names_its_section_by_the_token_of_the_product()
    {
        var example = Path.Combine(RepositoryFiles.Root, "build", "server", "appsettings.Production.example.json");
        var options = new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip };
        var sections = JsonDocument.Parse(File.ReadAllText(example), options).RootElement.EnumerateObject().Select(property => property.Name);
        Assert.Equal(["{{PRODUCT}}", "Logging"], sections);
        Assert.Equal(Topogenia.Application.ProductIdentity.Name, Topogenia.Host.Composition.ProductOptions.Section);
        var assembly = File.ReadAllText(Path.Combine(RepositoryFiles.Root, "build", "package.mjs"));
        Assert.Contains(".replaceAll('{{PRODUCT}}', product)", assembly, StringComparison.Ordinal);
        Assert.Contains("copyTemplate('build/server/appsettings.Production.example.json'", assembly, StringComparison.Ordinal);
    }

    /// <summary>D13: in the interface, the product name lives in one translation key per language file.</summary>
    [Fact]
    public void Each_language_file_names_the_product_once()
    {
        var name = Topogenia.Application.ProductIdentity.Name;
        var offenders = RepositoryFiles.Enumerate(["web/src/shared/i18n/locales"], ".json")
            .Where(file => Regex.Matches(File.ReadAllText(file), Regex.Escape(name), RegexOptions.IgnoreCase).Count > 1)
            .Select(RepositoryFiles.Relative)
            .ToList();
        Assert.Empty(offenders);
    }

    /// <summary>
    /// Spec 3.2: the rule <c>ExtraRulesTests.Http_entry_points_are_declared_only_in_the_endpoints_folder</c> and this one
    /// share one detector (<see cref="RouteDetectors.EntryPoints"/>). This one proves that it reads the real code: it sees
    /// the routes of every file of the Endpoints folder that declares one, not the hand-over of the pipeline (a call to the
    /// Endpoints namespace), and nothing outside the allowed places; a detector gone blind (a type of ASP.NET Core renamed) would otherwise leave the rule
    /// green on an empty list.
    /// </summary>
    [Fact]
    public void Http_entry_points_of_any_style_are_in_the_endpoints_folder()
    {
        var found = SemanticSources.Production.Where(file => RouteDetectors.EntryPoints(file).Any()).Select(file => file.Path).ToList();
        var declaring = RepositoryFiles.Enumerate(["src/Topogenia.Host/Endpoints"], ".cs")
            .Where(file => Regex.IsMatch(File.ReadAllText(file), @"\.\s*Map(Get|Post|Put|Patch|Delete|Group|Fallback)\w*\s*\("))
            .Select(RepositoryFiles.Relative)
            .ToList();
        Assert.NotEmpty(declaring);
        Assert.All(declaring, file => Assert.Contains(file, found));
        Assert.DoesNotContain("src/Topogenia.Host/Composition/HostPipeline.cs", found);
        Assert.Contains("MapApi", File.ReadAllText(Path.Combine(RepositoryFiles.Root, "src/Topogenia.Host/Composition/HostPipeline.cs")), StringComparison.Ordinal);
        Assert.All(found, file => Assert.Contains(RouteDetectors.AllowedPlaces, place => file.StartsWith(place, StringComparison.Ordinal)));
    }

    /// <summary>IDE0130 makes every namespace follow its folder, so the Endpoints folder and namespace coincide.</summary>
    [Fact]
    public void Namespaces_follow_folders()
    {
        var editorconfig = Read(".editorconfig");
        Assert.Contains("dotnet_style_namespace_match_folder = true", editorconfig, StringComparison.Ordinal);
        Assert.Contains("dotnet_diagnostic.IDE0130.severity = warning", editorconfig, StringComparison.Ordinal);
    }
}
