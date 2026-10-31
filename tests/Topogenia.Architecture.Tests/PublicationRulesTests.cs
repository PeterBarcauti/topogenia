// PublicationRulesTests.cs
// Spec 6.1, 6.2, ADR 071, ADR 072: the publication of the desktop mode is one self-contained executable for win-x64 that writes nothing outside the folder of the product; the one of the server mode is for IIS; the chain of the packages carries no script of the system (ADR 076); each package of the core carries its licence and its mark and is archived (ADR 087).

using System.Xml.Linq;

namespace Topogenia.Architecture.Tests;

public sealed class PublicationRulesTests
{
    private static string Build(string file) => File.ReadAllText(Path.Combine(RepositoryFiles.Root, "build", file));

    private static XElement Desktop() =>
        XDocument.Load(Path.Combine(RepositoryFiles.Root, "src", "Topogenia.Host", "Properties", "PublishProfiles", "Desktop.pubxml"))
            .Root!.Element("PropertyGroup")!;

    [Theory]
    [InlineData("RuntimeIdentifier", "win-x64")]
    [InlineData("SelfContained", "true")]
    [InlineData("PublishSingleFile", "true")]
    [InlineData("Configuration", "Release")]
    public void The_desktop_publication_is_one_self_contained_executable_for_windows(string property, string value) =>
        Assert.Equal(value, Desktop().Element(property)?.Value);

    /// <summary>
    /// A single file that extracts its native libraries writes them under %TEMP% at its start (spec 6.1: nothing written
    /// outside %LOCALAPPDATA%\Topogenia): SQLite and the driver of SQL Server stay beside the executable.
    /// </summary>
    [Fact]
    public void The_native_libraries_are_never_extracted_and_nothing_of_iis_nor_of_debugging_ships()
    {
        var profile = Desktop();
        Assert.Equal("false", profile.Element("IncludeNativeLibrariesForSelfExtract")?.Value);
        Assert.Null(profile.Element("IncludeAllContentForSelfExtract"));
        Assert.Equal(("false", "false", "false", "none"), (profile.Element("PublishIISAssets")?.Value, profile.Element("CopyOutputSymbolsToPublishDirectory")?.Value,
            profile.Element("PublishDocumentationFile")?.Value, profile.Element("AllowedReferenceRelatedFileExtensions")?.Value));
    }

    /// <summary>
    /// Spec 5.4 (S-5.4-21): no script at the run, neither to start nor to install: the shipped code starts no process but
    /// the browser of the user on the address of the application, and the ZIP carries no `.bat`, `.cmd` nor `.ps1`.
    /// </summary>
    [Fact]
    public void The_shipped_code_starts_no_process_but_the_browser_and_the_zip_carries_no_script()
    {
        var starts = RepositoryFiles.ProductionCSharp()
            .SelectMany(file => File.ReadLines(file).Select((line, index) => (file, line, index)))
            .Where(item => item.line.Contains("Process.Start", StringComparison.Ordinal) || item.line.Contains("new Process(", StringComparison.Ordinal))
            .Select(item => $"{RepositoryFiles.Relative(item.file)}:{item.index + 1}")
            .ToList();
        Assert.Equal(["src/Topogenia.Host/Startup/StartupService.cs"], starts.Select(start => start.Split(':')[0]).Distinct());
        Assert.Single(starts);
        Assert.Contains("class ShellBrowserLauncher", File.ReadAllText(Path.Combine(RepositoryFiles.Root, "src", "Topogenia.Host", "Startup", "StartupService.cs")), StringComparison.Ordinal);
        // build/package.mjs refuses a ZIP that carries a script, at each build.
        Assert.Contains("pdb|ps1|psm1|bat|cmd|vbs|wsf", Build("package.mjs"), StringComparison.Ordinal);
    }

    private static readonly string[] SystemScripts = [".ps1", ".psm1", ".psd1", ".bat", ".cmd", ".vbs", ".wsf"];

    /// <summary>
    /// ADR 076: nothing of the chain of the packages looks like a malware to an antivirus or an EDR. The repository carries
    /// no script of the system (PowerShell, batch, VBScript); the scripts of build/ start their tools directly, never
    /// through a shell, and call no tool of Windows that malware borrows (archiver, hasher, downloader, script hosts); the
    /// ZIP is written and read back by the tasks of the SDK, with no task of code compiled by MSBuild.
    /// </summary>
    [Fact]
    public void The_chain_of_the_packages_carries_no_system_script_and_starts_its_tools_directly()
    {
        var scripts = RepositoryFiles.Enumerate(["build", "src", "tests", "web/src", "web/scripts", "web/e2e", "docs", "samples", ".github", ".config"], SystemScripts)
            .Concat(RepositoryFiles.Shallow(["", "web"], SystemScripts))
            .Select(RepositoryFiles.Relative);
        Assert.Empty(scripts);
        var borrowed = RepositoryFiles.Enumerate(["build"], ".mjs")
            .Where(file => System.Text.RegularExpressions.Regex.IsMatch(File.ReadAllText(file),
                @"(?i)\b(powershell|pwsh|cmd\.exe|certutil|tar\.exe|bitsadmin|mshta|[wc]script|rundll32|regsvr32)\b|shell:\s*true|\bexec(File)?Sync\b|import\s*\{[^}]*\bexec\b"))
            .Select(RepositoryFiles.Relative);
        Assert.Empty(borrowed);
        var package = Build("package.mjs");
        Assert.Contains("shell: false", package, StringComparison.Ordinal);
        Assert.Contains("archiveProject, '-t:Zip'", package, StringComparison.Ordinal);
        Assert.Contains("archiveProject, '-t:Unzip'", package, StringComparison.Ordinal);
        Assert.Contains("The ZIP gives back other bytes for", package, StringComparison.Ordinal);
        var archive = Build("archive.proj");
        Assert.Contains("<ZipDirectory ", archive, StringComparison.Ordinal);
        Assert.Contains("<Unzip ", archive, StringComparison.Ordinal);
        Assert.DoesNotContain("UsingTask", archive, StringComparison.Ordinal);
    }

    /// <summary>
    /// ADR 076: what ships carries no mark that antivirus heuristics read as malware: the single file of the desktop stores
    /// its assemblies uncompressed (nothing unpacked in memory at its start), and the obfuscation of the core renames
    /// without hiding the strings nor setting the attribute against disassembly; beside each ZIP, the SHA-256 of each of
    /// its files, for the allow-list of an antivirus or an EDR.
    /// </summary>
    [Fact]
    public void What_ships_carries_no_mark_of_a_packer_and_its_files_are_listed_by_their_sha256()
    {
        Assert.Equal("false", Desktop().Element("EnableCompressionInSingleFile")?.Value);
        if (CoreSources.ArePresent)
        {
            var obfuscar = XDocument.Load(Path.Combine(RepositoryFiles.Root, "build", "core", "obfuscar.xml")).Root!.Elements("Var")
                .ToDictionary(variable => variable.Attribute("name")!.Value, variable => variable.Attribute("value")!.Value, StringComparer.Ordinal);
            Assert.Equal(("false", "false"), (obfuscar["SuppressIldasm"], obfuscar["HideStrings"]));
        }
        Assert.Contains("`${stage}.files.sha256`", Build("package.mjs"), StringComparison.Ordinal);
    }

    /// <summary>
    /// ADR 048, ADR 074: what ships carries the core from its package (signed by strong name, obfuscated), never built
    /// from its sources; build/package.mjs refuses, at each build, a package older than the sources, a publication without
    /// it, an assembly of the core on the server that is not the one of the package, and a lock file of the repository changed.
    /// </summary>
    [Fact]
    public void The_publication_carries_the_package_of_the_core_and_leaves_the_lock_files()
    {
        var script = Build("package.mjs");
        Assert.Contains("'-p:UseCorePackage=true'", script, StringComparison.Ordinal);
        Assert.Contains("'-p:NuGetLockFilePath=obj/packages.publish.lock.json'", script, StringComparison.Ordinal);
        // The core is checked before the publication.
        Assert.InRange(script.IndexOf("const locks = prepare();", StringComparison.Ordinal), 1, script.IndexOf("['publish',", StringComparison.Ordinal));
        foreach (var check in new[] { "The package of the core is older than", "The publication does not carry", "The publication carries the core built from its sources",
            "is not the one of its package", "The publication changed a lock file of the repository" })
            Assert.Contains(check, script, StringComparison.Ordinal);
    }

    /// <summary>
    /// ADR 087: each package of the core carries the licence it is delivered under and a mark only the holder makes, and
    /// is archived with the holder. The chain refuses a pack without them and gives them to the build of the domain, whose
    /// assembly carries them; build/core/licence.mjs makes the mark by an HMAC of its secret, checks it in the obfuscated
    /// assembly, archives the package, both assemblies, the map of names and the sources, keeps the key, the secret and the
    /// archive outside the repository, and starts its tools directly.
    /// </summary>
    [Fact]
    public void Each_package_of_the_core_carries_its_licence_and_its_mark_and_is_archived_outside_the_repository()
    {
        if (!CoreSources.ArePresent)
            return;
        var chain = Build(Path.Combine("core", "Topogenia.Core.csproj"));
        Assert.Contains("'$(CoreLicence)' == '' Or '$(CoreMark)' == ''", chain, StringComparison.Ordinal);
        Assert.Contains("-p:CoreLicence=$(CoreLicence)", chain, StringComparison.Ordinal);
        Assert.Contains("-p:CoreMark=$(CoreMark)", chain, StringComparison.Ordinal);
        var metadata = XDocument.Load(Path.Combine(RepositoryFiles.Root, "src", "Topogenia.Domain", "Topogenia.Domain.csproj")).Descendants("AssemblyMetadata")
            .Select(item => (item.Attribute("Include")?.Value, item.Attribute("Value")?.Value));
        Assert.Equal([("Licence", "$(CoreLicence)"), ("LicenceMark", "$(CoreMark)")], metadata);
        var tool = Build(Path.Combine("core", "licence.mjs"));
        foreach (var expected in new[] { "createHmac('sha256', secret)", "shell: false", "must be kept outside the repository", "'Mapping.txt'", "licences.json",
            "does not carry its licence and its mark", "one number, one licensee", "NOT AUTHENTIC", "is never replaced" })
            Assert.Contains(expected, tool, StringComparison.Ordinal);
        Assert.Contains("node build/core/licence.mjs pack", Build("package.mjs"), StringComparison.Ordinal);
    }

    /// <summary>
    /// Each ZIP carries the licences of the product, of the core (from its package) and of the components (the bundles of
    /// the interface have no banner), its readme and its SBOM, and its SHA-256 beside it: build/package.mjs checks the
    /// entries of the ZIP it wrote, at each build.
    /// </summary>
    [Fact]
    public void The_zip_carries_the_licences_and_its_sbom_and_is_checked_at_each_build()
    {
        var assembly = Build("package.mjs");
        Assert.Contains("['Desktop', 'Server'].includes(mode)", assembly, StringComparison.Ordinal);
        Assert.Contains("`-p:PublishProfile=${mode}`", assembly, StringComparison.Ordinal);
        // Spec 5.4, ADR 073: the interface built from its sources as `npm run build` does, the notices checked against it,
        // the entry point of the signature.
        Assert.Contains("\"build\": \"tsc -b && vite build\"", File.ReadAllText(Path.Combine(RepositoryFiles.Root, "web", "package.json")), StringComparison.Ordinal);
        foreach (var step in new[] { "'typescript', 'bin', 'tsc'), '-b']", "'vite', 'bin', 'vite.js'), 'build']", "'third-party-notices.mjs'), '--check']",
            "certificate: { type: 'string' }", "run('signtool'", "assemble(published, locks" })
            Assert.Contains(step, assembly, StringComparison.Ordinal);
        foreach (var entry in new[] { "'README.txt'", "'sbom.cdx.json'", "'licenses/LICENSE.txt'", "'licenses/NOTICE.txt'", "'licenses/THIRD-PARTY-NOTICES.md'",
            "'licenses/CORE-LICENSE.md'", "'licenses/Inter-OFL-1.1.md'", "'licenses/Lucide-ISC.txt'" })
            Assert.Contains(entry, assembly, StringComparison.Ordinal);
        foreach (var step in new[] { "join(core, 'LICENSE.md')", "'build', 'sbom.mjs'", "The ZIP lacks", "The ZIP carries", "createHash('sha256')", "`${file}.sha256`" })
            Assert.Contains(step, assembly, StringComparison.Ordinal);
    }

    /// <summary>Spec 6.2: the server mode depends on the Hosting Bundle of .NET 10 and ships the web.config of IIS, its example of settings and its guide.</summary>
    [Fact]
    public void The_server_publication_depends_on_the_framework_and_ships_its_web_config_its_settings_and_its_guide()
    {
        var profile = XDocument.Load(Path.Combine(RepositoryFiles.Root, "src", "Topogenia.Host", "Properties", "PublishProfiles", "Server.pubxml")).Root!.Element("PropertyGroup")!;
        Assert.Equal(("false", "false", "win-x64"), (profile.Element("SelfContained")?.Value, profile.Element("PublishSingleFile")?.Value, profile.Element("RuntimeIdentifier")?.Value));
        var assembly = Build("package.mjs");
        foreach (var source in new[] { "copyTemplate('build/server/appsettings.Production.example.json'", "join(root, 'docs', 'exploitation-serveur.md')",
            "copyTemplate('build/server/server-readme.txt'", "'web.config', 'appsettings.Production.example.json', 'exploitation-serveur.md'" })
            Assert.Contains(source, assembly, StringComparison.Ordinal);
    }

    /// <summary>IIS takes the largest request of the host (a package, ExchangeApi.PackageLimit, and 1 MiB of envelope) and names no server.</summary>
    [Fact]
    public void The_web_config_of_iis_takes_a_package_of_100_mib_and_names_no_server()
    {
        var config = XDocument.Load(Path.Combine(RepositoryFiles.Root, "src", "Topogenia.Host", "web.config"));
        var filtering = config.Descendants("requestFiltering").Single();
        Assert.Equal("true", filtering.Attribute("removeServerHeader")?.Value);
        Assert.Equal((Topogenia.Host.Api.Exchange.ExchangeApi.PackageLimit + 1024 * 1024).ToString(System.Globalization.CultureInfo.InvariantCulture),
            filtering.Element("requestLimits")?.Attribute("maxAllowedContentLength")?.Value);
        Assert.Equal("X-Powered-By", config.Descendants("customHeaders").Single().Element("remove")?.Attribute("name")?.Value);
    }
}
