// ConfigurationRulesTests.cs
// Build configuration rules: solution layout, common props, pinned versions, strict TypeScript, licences of the repository and of the core, no secret shipped (spec 3.1, 5.1, D01, D02, NF10, ADR 048).

using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Topogenia.Architecture.Tests;

public sealed partial class ConfigurationRulesTests
{
    private static string Read(string relative) => File.ReadAllText(Path.Combine(RepositoryFiles.Root, relative));

    private static XElement? Property(string relativeProps, string name) =>
        XDocument.Parse(Read(relativeProps)).Descendants(name).FirstOrDefault();

    [Theory]
    [InlineData("Topogenia.sln")]
    [InlineData("Directory.Build.props")]
    [InlineData("Directory.Packages.props")]
    [InlineData("global.json")]
    [InlineData("web/package.json")]
    [InlineData("web/package-lock.json")]
    [InlineData("docs/architecture.md")]
    public void The_solution_has_the_files_of_spec_3_1(string relative) =>
        Assert.True(File.Exists(Path.Combine(RepositoryFiles.Root, relative)), relative);

    [Theory]
    [InlineData("TargetFramework", "net10.0")]
    [InlineData("Nullable", "enable")]
    [InlineData("TreatWarningsAsErrors", "true")]
    [InlineData("ManagePackageVersionsCentrally", "true")]
    [InlineData("CentralPackageTransitivePinningEnabled", "true")]
    [InlineData("RestorePackagesWithLockFile", "true")]
    [InlineData("EnforceCodeStyleInBuild", "true")]
    public void Directory_build_props_sets_the_common_rules(string name, string value) =>
        Assert.Equal(value, Property("Directory.Build.props", name)?.Value);

    [Fact]
    public void Global_json_pins_the_sdk_10_feature_band_and_the_test_runner()
    {
        using var json = JsonDocument.Parse(Read("global.json"));
        var sdk = json.RootElement.GetProperty("sdk");
        Assert.Equal("10.0.402", sdk.GetProperty("version").GetString());
        Assert.Equal("latestPatch", sdk.GetProperty("rollForward").GetString());
        Assert.Equal("Microsoft.Testing.Platform", json.RootElement.GetProperty("test").GetProperty("runner").GetString());
    }

    [Fact]
    public void Entity_framework_core_10_is_the_data_access_and_dapper_is_absent()
    {
        var versions = XDocument.Parse(Read("Directory.Packages.props")).Descendants("PackageVersion")
            .ToDictionary(e => (string)e.Attribute("Include")!, e => (string)e.Attribute("Version")!);
        foreach (var package in new[] { "Microsoft.EntityFrameworkCore.SqlServer", "Microsoft.EntityFrameworkCore.Sqlite" })
            Assert.StartsWith("10.", versions[package], StringComparison.Ordinal);
        Assert.DoesNotContain(versions.Keys, key => key.Contains("Dapper", StringComparison.OrdinalIgnoreCase));
        // Exact versions only; the AWS SDK numbers its own in four parts (`4.0.101.14`).
        Assert.All(versions.Values, version => Assert.Matches(@"^\d+\.\d+\.\d+(\.\d+)?$", version));
    }

    [Fact]
    public void Npm_versions_are_exact_and_locked()
    {
        using var json = JsonDocument.Parse(Read("web/package.json"));
        var ranges = new[] { "dependencies", "devDependencies" }
            .Where(section => json.RootElement.TryGetProperty(section, out _))
            .SelectMany(section => json.RootElement.GetProperty(section).EnumerateObject())
            .Where(entry => !Regex.IsMatch(entry.Value.GetString() ?? "", @"^\d+\.\d+\.\d+$"))
            .Select(entry => entry.Name)
            .ToList();
        Assert.Empty(ranges);
        using var lockFile = JsonDocument.Parse(Read("web/package-lock.json"));
        Assert.Equal(3, lockFile.RootElement.GetProperty("lockfileVersion").GetInt32());
    }

    [Fact]
    public void Typescript_is_strict_and_lint_allows_no_warning()
    {
        using var tsconfig = JsonDocument.Parse(Read("web/tsconfig.json"), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
        var options = tsconfig.RootElement.GetProperty("compilerOptions");
        Assert.True(options.GetProperty("strict").GetBoolean());
        Assert.True(options.GetProperty("noUncheckedIndexedAccess").GetBoolean());
        using var package = JsonDocument.Parse(Read("web/package.json"));
        Assert.Contains("--max-warnings 0", package.RootElement.GetProperty("scripts").GetProperty("lint").GetString(), StringComparison.Ordinal);
        Assert.Contains("strictTypeChecked", Read("web/eslint.config.mjs"), StringComparison.Ordinal);
    }

    [GeneratedRegex(@"SPDX-License-Identifier|Licensed under|\bCopyright\b|©|\bAuthor\s*:|\bAuteur\s*:|^\s*(//|#|<!--|/\*|--|REM)\s*Date\s*:", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex LicenceOrAuthorHeader();

    /// <summary>
    /// ADR 048: the repository is under Apache-2.0 (LICENSE at the root, the holder in NOTICE), the core of the domain and
    /// its tests under the proprietary licence (LICENSE.md in each of the two folders); spec 0.2: no licence, author nor
    /// date in the headers of the files.
    /// </summary>
    [Fact]
    public void The_repository_is_under_apache_the_core_under_its_own_licence_and_no_file_has_a_licence_author_or_date_header()
    {
        Assert.StartsWith("Apache License", Read("LICENSE").TrimStart(), StringComparison.Ordinal);
        Assert.Contains("Version 2.0, January 2004", Read("LICENSE"), StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(RepositoryFiles.Root, "LICENSE.md")));
        Assert.Contains("Copyright 2026 Peter Barcauti", Read("NOTICE"), StringComparison.Ordinal);
        // The two licences of the core lie beside its sources; the public repository has neither (its package carries one).
        foreach (var core in CoreSources.ArePresent ? new[] { "src/Topogenia.Domain/LICENSE.md", "tests/Topogenia.Domain.Tests/LICENSE.md" } : [])
        {
            Assert.Contains("Tous droits réservés", Read(core), StringComparison.Ordinal);
            Assert.Contains("All rights reserved", Read(core), StringComparison.Ordinal);
        }
        // The licence files themselves name the holder; every other file of the repository carries no such header: the
        // folders read in depth, and the files directly at the root and at the root of web (ADR 048, round 2 of the gate P4).
        string[] licenceFiles = ["LICENSE", "LICENSE.md", "NOTICE"];
        string[] textFiles = [".cs", ".ts", ".tsx", ".css", ".mjs", ".html", ".sql", ".ps1", ".cmd", ".bat", ".yml", ".yaml", ".csproj", ".props", ".proj", ".targets",
            ".md", ".json", ".sln", ".editorconfig", ".gitattributes", ".gitignore"];
        var offenders = RepositoryFiles.Enumerate(["src", "tests", "web/src", "web/scripts", "web/e2e", "build", ".github", ".config", "samples", "docs"], textFiles)
            .Concat(RepositoryFiles.Shallow(["", "web"], textFiles))
            .Where(file => !licenceFiles.Contains(Path.GetFileName(file), StringComparer.Ordinal))
            .Where(file => LicenceOrAuthorHeader().IsMatch(string.Join("\n", File.ReadLines(file).Take(5))))
            .Select(RepositoryFiles.Relative)
            .ToList();
        Assert.Empty(offenders);
    }

    [Fact]
    public void The_licence_header_detector_catches_its_samples()
    {
        Assert.Matches(LicenceOrAuthorHeader(), "// SPDX-License-Identifier: MIT");
        Assert.Matches(LicenceOrAuthorHeader(), "// Copyright 2026 Camille Exemple");
        Assert.Matches(LicenceOrAuthorHeader(), "-- (c) Copyright Exemple");
        Assert.Matches(LicenceOrAuthorHeader(), "<!-- © 2026 Exemple -->");
        Assert.Matches(LicenceOrAuthorHeader(), "// Auteur: Camille Exemple");
        Assert.Matches(LicenceOrAuthorHeader(), "# Date: 23/10/2026");
        Assert.DoesNotMatch(LicenceOrAuthorHeader(), "// Dates are written in UTC");
    }

    /// <summary>
    /// Spec 5.1: no secret in the code, the published configuration or the browser. Read: the C# of the production code,
    /// the JSON files of `src` (the `appsettings*.json` and the messages; the lock files of the packages aside), the scripts
    /// and pages of the interface, the configuration files at the root of `web` (`vite.config.ts`, whose `define` enters the
    /// bundle) and its environment files (`.env*`, whose `VITE_*` variables enter the bundle), the test probes of the
    /// providers included: the application role of `SqlServerAccessProbe` takes a password drawn at run time, never written
    /// in the code.
    /// </summary>
    [Fact]
    public void Shipped_code_and_settings_carry_no_secret()
    {
        var csharp = RepositoryFiles.ProductionCSharp()
            .SelectMany(file => ShippedCodeDetectors.SecretsInCSharp(File.ReadAllText(file)).Select(place => $"{RepositoryFiles.Relative(file)} {place}"));
        var settings = RepositoryFiles.Enumerate(["src"], ".json")
            .Where(file => Path.GetFileName(file) != "packages.lock.json" && !RepositoryFiles.IsGenerated(file))
            .SelectMany(file => ShippedCodeDetectors.SecretsInSettings(File.ReadAllText(file)).Select(place => $"{RepositoryFiles.Relative(file)} {place}"));
        var browser = RepositoryFiles.Enumerate(["web/src"], ".ts", ".tsx", ".html")
            .Concat(RepositoryFiles.Shallow(["web"], ".html", ".ts", ".mjs"))
            .Where(file => ShippedCodeDetectors.HasSecretInScript(File.ReadAllText(file)))
            .Select(RepositoryFiles.Relative);
        var web = Path.Combine(RepositoryFiles.Root, "web");
        var environment = (Directory.Exists(web) ? Directory.EnumerateFiles(web, ".env*") : [])
            .Where(file => ShippedCodeDetectors.HasSecretInEnvironment(File.ReadAllText(file)))
            .Select(RepositoryFiles.Relative);
        Assert.Empty(csharp.Concat(settings).Concat(browser).Concat(environment).ToList());
    }

    [Theory]
    [InlineData("class A { const string Main = \"Server=db;Database=app;User ID=app;Password=Example-1;\"; }")]
    [InlineData("class A { string main = \"Data Source=db;Uid=app;Pwd=Example-1\"; }")]
    [InlineData("class A { void M(System.Data.Common.DbConnection c, string host) => c.ConnectionString = $\"Server={host};Password=Example-1\"; }")]
    [InlineData("class A { string sql = \"CREATE LOGIN app WITH PASSWORD = N'Example-1'\"; }")]
    [InlineData("class A { string storage = \"AccountName=a;AccountKey=abc123==\"; }")]
    [InlineData("class A { string key = \"\"\"\n-----BEGIN RSA PRIVATE KEY-----\nMIIB\n\"\"\"; }")]
    [InlineData("class A { private const string ApiKey = \"k-123456\"; }")]
    [InlineData("class Options { public string Password { get; set; } = \"Example-1\"; }")]
    [InlineData("class Options { public string ClientSecret => \"s-123\"; }")]
    [InlineData("class A { void M(Options o) => o.AdminPassword = \"Example-1\"; }")]
    [InlineData("class A { object M() => new Options { AccessToken = \"t-123\" }; }")]
    [InlineData("class A { void M(IDictionary<string, string> s) => s[\"Database:Password\"] = \"Example-1\"; }")]
    [InlineData("class A { object M() => Connect(user: \"app\", password: \"Example-1\"); }")]
    [InlineData("class A { object o = new { ClientSecret = \"s-123\" }; }")]
    [InlineData("class A { object o = new Dictionary<string, string?> { { \"Database:Password\", \"Example-1\" } }; }")]
    [InlineData("class A { object o = KeyValuePair.Create(\"Authentication:ClientSecret\", \"s-123\"); }")]
    [InlineData("class A { object o = new KeyValuePair<string, string>(\"Database__Password\", \"Example-1\"); }")]
    [InlineData("class A { const string Password = \"Example-\" + \"1\"; }")]
    [InlineData("class A { string main = \"Server=db;\" + \"Password=\" + \"Example-1\"; }")]
    public void A_secret_written_in_csharp_is_detected(string sample) => Assert.NotEmpty(ShippedCodeDetectors.SecretsInCSharp(sample));

    [Theory]
    [InlineData("class A { const string PasswordField = \"password\"; }")]
    [InlineData("class A { const string Password = \"\"; }")]
    [InlineData("class A { string sessionSecret = NewSecret(); static string NewSecret() => \"\"; }")]
    [InlineData("class A { void M(DbCommand c, string p) => Parameter(c, \"@password\", p); }")]
    [InlineData("class A { string M(string secret) => $\"CREATE APPLICATION ROLE [r] WITH PASSWORD = N'{secret}'\"; }")]
    [InlineData("class A { string main = \"Server=.;Database=app;Integrated Security=true;Password=;\"; }")]
    [InlineData("class A { string M(string h, string p) => string.Format(\"Server={0};Password={1}\", h, p); }")]
    [InlineData("class A { object o = new Dictionary<string, string?> { { \"Database:Password\", \"\" }, { \"Desktop:Port\", \"5080\" } }; }")]
    [InlineData("class A { object M(string p) => KeyValuePair.Create(\"Database:Password\", p); }")]
    [InlineData("class A { string M(string p) => \"Server=db;\" + \"Password=\" + p; }")]
    [InlineData("// Password=Example-1 and ApiKey = \"k-1\" in a comment\nclass A { }")]
    public void Names_placeholders_empty_values_and_comments_are_no_secret(string sample) => Assert.Empty(ShippedCodeDetectors.SecretsInCSharp(sample));

    [Theory]
    [InlineData("{ \"ConnectionStrings\": { \"Main\": \"Server=db;User ID=app;Password=Example-1\" } }")]
    [InlineData("{ \"Authentication\": { \"ClientSecret\": \"s-123\" } }")]
    [InlineData("{ \"Mail\": { \"Smtp\": { \"Password\": \"Example-1\" } } }")]
    [InlineData("{ \"Keys\": [ { \"ApiKey\": \"k-1\" } ] }")]
    [InlineData("// settings\n{ \"Signing\": { \"Certificate\": \"-----BEGIN PRIVATE KEY-----\" }, }")]
    public void A_secret_written_in_the_settings_is_detected(string sample) => Assert.NotEmpty(ShippedCodeDetectors.SecretsInSettings(sample));

    [Theory]
    [InlineData("{ \"ConnectionStrings\": { \"Main\": \"Server=.;Database=app;Integrated Security=true;TrustServerCertificate=true\" } }")]
    [InlineData("{ \"Desktop\": { \"Port\": 5080 }, \"Logging\": { \"LogLevel\": { \"Default\": \"Information\" } } }")]
    [InlineData("{ \"Authentication\": { \"ClientSecret\": \"\" } }")]
    [InlineData("{ \"PASSWORD_REQUIRED\": \"Le mot de passe est requis\" }")]
    public void Windows_authentication_empty_values_and_texts_are_no_secret_in_the_settings(string sample) =>
        Assert.Empty(ShippedCodeDetectors.SecretsInSettings(sample));

    [Theory]
    [InlineData("const apiKey = \"k-123456\";", true)]
    [InlineData("export const config = { clientSecret: 's-123' };", true)]
    [InlineData("<meta name=\"db\" content=\"Server=db;Password=Example-1\">", true)]
    [InlineData("<input type=\"password\" autocomplete=\"current-password\">", false)]
    [InlineData("const password = \"\";", false)]
    [InlineData("const label = t(\"password.label\");", false)]
    [InlineData("function check(secret: string): boolean { return secret.length > 0; }", false)]
    public void A_secret_written_in_a_script_or_a_page_is_detected(string sample, bool secret) =>
        Assert.Equal(secret, ShippedCodeDetectors.HasSecretInScript(sample));

    [Theory]
    [InlineData("VITE_API_KEY=k-123456", true)]
    [InlineData("# local\nexport DB_PASSWORD=\"Example-1\"", true)]
    [InlineData("VITE_CLIENT_SECRET = s-123", true)]
    [InlineData("VITE_API_KEY=", false)]
    [InlineData("VITE_API_KEY=\"\"", false)]
    [InlineData("VITE_PASSWORD_MIN_LENGTH=12\n# VITE_API_KEY=k-1 left as a comment", false)]
    public void A_secret_written_in_an_environment_file_of_the_interface_is_detected(string sample, bool secret) =>
        Assert.Equal(secret, ShippedCodeDetectors.HasSecretInEnvironment(sample));

    /// <summary>D12: identifiers of the code are in English, so they are plain ASCII (no accented French word).</summary>
    [Fact]
    public void Identifiers_are_ascii()
    {
        var offenders = RepositoryFiles.AllCSharp()
            .Where(file => CSharpSyntaxTree.ParseText(File.ReadAllText(file), cancellationToken: TestContext.Current.CancellationToken)
                .GetRoot(TestContext.Current.CancellationToken).DescendantTokens()
                .Any(token => token.IsKind(SyntaxKind.IdentifierToken) && token.ValueText.Any(c => c > 127)))
            .Select(RepositoryFiles.Relative)
            .ToList();
        Assert.Empty(offenders);
    }
}
