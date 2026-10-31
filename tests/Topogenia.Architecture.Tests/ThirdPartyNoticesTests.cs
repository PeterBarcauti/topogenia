// ThirdPartyNoticesTests.cs
// Spec 0.2: every third-party component shipped has a permissive licence, or an exception written in an ADR.

using System.Text.Json;
using System.Text.RegularExpressions;

namespace Topogenia.Architecture.Tests;

public sealed partial class ThirdPartyNoticesTests
{
    /// <summary>The licences of the rule of the spec 0.2, by their SPDX identifier.</summary>
    private static readonly HashSet<string> Permissive =
        ["MIT", "MIT-0", "Apache-2.0", "BSD-2-Clause", "BSD-3-Clause", "ISC", "OFL-1.1", "0BSD", "BlueOak-1.0.0", "CC0-1.0",
         "Unlicense", "Zlib", "Python-2.0", "CC-BY-4.0"];

    /// <summary>The components shipped under another licence, each with the decision that accepts it.</summary>
    private static readonly Dictionary<string, string> Exceptions = new(StringComparer.Ordinal)
    {
        ["Microsoft.Data.SqlClient.SNI.runtime"] = "024-composants-natifs-sql-server.md",
    };

    /// <summary>Whether an SPDX expression may ship: one permissive choice of an OR, every part of an AND permissive.</summary>
    public static bool IsPermissive(string expression)
    {
        var text = expression.Trim();
        while (text.StartsWith('(') && text.EndsWith(')'))
            text = text[1..^1].Trim();
        if (text.Contains(" OR ", StringComparison.Ordinal))
            return text.Split(" OR ").Any(IsPermissive);
        if (text.Contains(" AND ", StringComparison.Ordinal))
            return text.Split(" AND ").All(IsPermissive);
        return Permissive.Contains(text);
    }

    [Fact]
    public void Every_component_shipped_has_a_permissive_licence_or_its_exception_in_an_adr()
    {
        var rows = Rows();
        var shipped = rows.Where(row => row.Usage == "livré").ToList();
        Assert.NotEmpty(shipped);
        var offenders = shipped
            .Where(row => !IsPermissive(row.Licence))
            .Where(row => !Exceptions.TryGetValue(row.Component, out var adr)
                || !File.Exists(Path.Combine(RepositoryFiles.Root, "docs", "decisions", adr)))
            .Select(row => $"{row.Component} {row.Version}: {row.Licence}")
            .ToList();
        Assert.Empty(offenders);
        Assert.All(rows, row => Assert.Contains(row.Usage, new[] { "livré", "tests", "développement", "non servi" }));    }

    /// <summary>
    /// The file is the one the generator gives today: each package of the npm lock has its row (name and version), and
    /// each package carried in the browser bundle of exceljs (read from its source map) has its row, shipped.
    /// </summary>
    [Fact]
    public void The_notices_list_every_package_of_the_lock_and_every_package_carried_in_the_exceljs_bundle()
    {
        var rows = Rows();
        using var lockFile = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryFiles.Root, "web", "package-lock.json")));
        var locked = lockFile.RootElement.GetProperty("packages").EnumerateObject()
            .Where(package => package.Name.StartsWith("node_modules/", StringComparison.Ordinal))
            .Select(package => (Id: package.Name[(package.Name.LastIndexOf("node_modules/", StringComparison.Ordinal) + 13)..],
                Version: package.Value.GetProperty("version").GetString() ?? ""))
            .ToList();
        Assert.NotEmpty(locked);
        var stale = locked.Where(package => !rows.Any(row => row.Component == package.Id && row.Version == package.Version))
            .Select(package => $"{package.Id} {package.Version}").ToList();
        Assert.Empty(stale);
        var map = Path.Combine(RepositoryFiles.Root, "web", "node_modules", "exceljs", "dist", "exceljs.min.js.map");
        using var bundle = JsonDocument.Parse(File.ReadAllText(map));
        var carried = bundle.RootElement.GetProperty("sources").EnumerateArray()
            .Select(source => source.GetString() ?? "")
            .Where(source => source.Contains("node_modules/", StringComparison.Ordinal))
            .Select(source => source[(source.LastIndexOf("node_modules/", StringComparison.Ordinal) + 13)..].Split('/'))
            .Select(parts => parts[0].StartsWith('@') ? $"{parts[0]}/{parts[1]}" : parts[0])
            .Distinct(StringComparer.Ordinal)
            .ToList();
        Assert.True(carried.Count > 50, $"{carried.Count} packages read in the map of exceljs");
        var missing = carried.Where(id => !rows.Any(row => row.Component == id && row.Version.StartsWith("dans exceljs", StringComparison.Ordinal) && row.Usage == "livré"))
            .ToList();
        Assert.Empty(missing);
    }

    /// <summary>
    /// F-41 (R4-m2): the whole file is the one the generator writes from the restore graphs, the lock and the packages the
    /// built interface serves (`web/served-packages.json`, written by `npm run build`): a usage or a licence changed by hand,
    /// a row left after a package went, fail. Each package served is a package of production of the lock.
    /// </summary>
    [Fact]
    public async Task The_notices_are_the_file_the_generator_writes_and_the_served_packages_are_of_production()
    {
        using var lockFile = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryFiles.Root, "web", "package-lock.json")));
        var packages = lockFile.RootElement.GetProperty("packages");
        using var served = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryFiles.Root, "web", "served-packages.json")));
        var paths = served.RootElement.EnumerateArray().Select(item => item.GetProperty("path").GetString() ?? "").ToList();
        Assert.True(paths.Count > 20, $"{paths.Count} packages served");
        var outside = paths.Where(path => !packages.TryGetProperty(path, out var entry) || entry.TryGetProperty("dev", out _)).ToList();
        Assert.Empty(outside);

        using var check = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("node", "build/third-party-notices.mjs --check")
        {
            WorkingDirectory = RepositoryFiles.Root, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false,
        })!;
        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        bounded.CancelAfter(TimeSpan.FromMinutes(2));
        var errors = check.StandardError.ReadToEndAsync(bounded.Token);
        var output = check.StandardOutput.ReadToEndAsync(bounded.Token);
        await check.WaitForExitAsync(bounded.Token);
        Assert.True(check.ExitCode == 0, await errors);
        Assert.Contains("up to date", await output, StringComparison.Ordinal);
    }

    /// <summary>P7-T04: the checked list of docs/securite.md is the one of the rows S-5.x of the coverage (build/security-list.mjs --check).</summary>
    [Fact]
    public async Task The_security_list_is_the_one_of_the_coverage()
    {
        using var check = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("node", "build/security-list.mjs --check")
        {
            WorkingDirectory = RepositoryFiles.Root, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false,
        })!;
        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        bounded.CancelAfter(TimeSpan.FromMinutes(1));
        var errors = check.StandardError.ReadToEndAsync(bounded.Token);
        var output = check.StandardOutput.ReadToEndAsync(bounded.Token);
        await check.WaitForExitAsync(bounded.Token);
        Assert.True(check.ExitCode == 0, await errors);
        Assert.Contains("up to date", await output, StringComparison.Ordinal);
    }

    [Fact]
    public void The_rule_takes_a_permissive_choice_and_refuses_an_unknown_or_a_copyleft_licence()
    {
        Assert.True(IsPermissive("MIT"));
        Assert.True(IsPermissive("(MPL-2.0 OR Apache-2.0)"));
        Assert.True(IsPermissive("(MIT AND Zlib)"));
        Assert.True(IsPermissive("MIT OR SEE LICENSE IN FEEL-FREE.md"));
        Assert.False(IsPermissive("unknown"));
        Assert.False(IsPermissive("MPL-2.0"));
        Assert.False(IsPermissive("(MIT AND GPL-3.0-only)"));
        Assert.False(IsPermissive("file: LICENSE.txt"));
    }

    private sealed record Row(string Component, string Version, string Licence, string Usage);

    private static List<Row> Rows() =>
        File.ReadAllLines(Path.Combine(RepositoryFiles.Root, "THIRD-PARTY-NOTICES.md"))
            .Select(line => RowPattern().Match(line))
            .Where(match => match.Success)
            .Select(match => new Row(match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value.Trim(), match.Groups[4].Value.Trim()))
            .ToList();

    [GeneratedRegex(@"^\| (?:NuGet|npm) \| `([^`]+)` \| ([^|]+) \| ([^|]+) \| ([^|]+) \|$")]
    private static partial Regex RowPattern();
}
