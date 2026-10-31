// SourceRulesTests.cs
// Rules checked on the source text: raw SQL, triggers, system database, sizes, work markers, product name, UTC dates, outgoing URLs and connections, paths opened (spec 3.2, 4.4, 5.4, D04, D08, D13, D14).

namespace Topogenia.Architecture.Tests;

public sealed class SourceRulesTests
{
    private static readonly string[] SqlProviderFolders = ["src/Topogenia.Persistence.SqlServer/", "src/Topogenia.Persistence.Sqlite/"];

    /// <summary>Every text source that the size and marker rules cover.</summary>
    private static readonly string[] SourceExtensions =
        [".cs", ".ts", ".tsx", ".css", ".mjs", ".html", ".yml", ".yaml", ".csproj", ".props", ".proj", ".sql", ".ps1", ".cmd", ".bat"];

    private static readonly string[] SourceFolders = ["src", "tests", "web/src", "web/e2e", "web/scripts", "build", ".github"];

    private static IEnumerable<string> AllSources() =>
        RepositoryFiles.Enumerate(SourceFolders, SourceExtensions)
            .Concat(RepositoryFiles.Shallow(["", "web"], SourceExtensions))
            .Where(file => !RepositoryFiles.IsGenerated(file))
            .Distinct(StringComparer.Ordinal);

    private static bool InSqlProvider(string relative) => SqlProviderFolders.Any(folder => relative.StartsWith(folder, StringComparison.Ordinal));

    [Fact]
    public void No_raw_sql_outside_the_two_providers()
    {
        var offenders = RepositoryFiles.AllCSharp()
            .Where(file => !InSqlProvider(RepositoryFiles.Relative(file)))
            .Where(file => !RepositoryFiles.Relative(file).StartsWith("tests/Topogenia.Architecture.Tests/", StringComparison.Ordinal))
            .Where(file => SourceDetectors.ContainsRawSql(File.ReadAllText(file)))
            .Select(RepositoryFiles.Relative)
            .ToList();
        Assert.Empty(offenders);
    }

    /// <summary>
    /// The test probes of the SQL Server provider (`*Probe.cs`) create, in a test database, the objects that the start must
    /// refuse (a trigger, a procedure, a function added by hand): they are the only exception, and no type of the
    /// application depends on them (<see cref="ProbeRulesTests"/>, ADR 037).
    /// </summary>
    [Fact]
    public void No_trigger_procedure_or_function_anywhere_including_migrations()
    {
        var offenders = RepositoryFiles.Enumerate(["src"], ".cs", ".sql")
            .Where(file => !ProbeRulesTests.IsTestProbe(RepositoryFiles.Relative(file)))
            .Where(file => file.EndsWith(".cs", StringComparison.Ordinal)
                ? SourceDetectors.CreatesTriggerOrProcedureInCSharp(File.ReadAllText(file))
                : SourceDetectors.CreatesTriggerOrProcedure(File.ReadAllText(file)))
            .Select(RepositoryFiles.Relative)
            .ToList();
        Assert.Empty(offenders);
    }

    /// <summary>
    /// ADR 037: only what a tool wrote whole is spared the rules (the designer files, the snapshot of the model, a migration
    /// without SQL written by hand); a migration with SQL written by hand follows the rules of the form, size and product
    /// name aside (<see cref="RepositoryFiles.IsMigration"/>).
    /// </summary>
    [Fact]
    public void Only_the_files_written_whole_by_a_tool_are_spared_the_rules()
    {
        var migrations = RepositoryFiles.Enumerate(["src"], ".cs").Where(RepositoryFiles.IsMigration).ToList();
        var byHand = migrations.Where(file => !RepositoryFiles.IsGenerated(file)).Select(Path.GetFileName).ToList();
        Assert.Contains("20261023083307_RolesAndReportViews.cs", byHand);
        Assert.Contains("20261023092342_SnapshotIsolation.cs", byHand);
        Assert.Contains("20261023093315_SnapshotIsolationCheck.cs", byHand);
        Assert.All(migrations.Where(file => file.EndsWith(".Designer.cs", StringComparison.Ordinal) || file.EndsWith("ModelSnapshot.cs", StringComparison.Ordinal)),
            file => Assert.True(RepositoryFiles.IsGenerated(file), file));
        Assert.All(migrations.Where(file => file.EndsWith("_Initial.cs", StringComparison.Ordinal)), file => Assert.True(RepositoryFiles.IsGenerated(file), file));
        Assert.False(RepositoryFiles.IsGenerated(Path.Combine(RepositoryFiles.Root, "src", "Topogenia.Persistence", "Schema", "SchemaMigrations.cs")));
    }

    /// <summary>
    /// The samples of the control of the architecture (gate P3, round 3): a `*.g.cs` or a `*.Designer.cs` written by hand,
    /// or a designer file of the migrations without the header of a tool, is not spared the rules.
    /// </summary>
    [Theory]
    [InlineData("src/Topogenia.Persistence.SqlServer/RawQueries.g.cs", Generated, false)]
    [InlineData("src/Topogenia.Persistence.SqlServer/RawQueries.g.cs", ByHand, false)]
    [InlineData("src/Topogenia.Persistence/Schema/Report.Designer.cs", Generated, false)]
    [InlineData("tests/Topogenia.Persistence.Tests/Samples.g.cs", Generated, false)]
    [InlineData("src/Topogenia.Persistence.SqlServer/Migrations/20261023083307_RolesAndReportViews.Designer.cs", ByHand, false)]
    [InlineData("src/Topogenia.Persistence.Sqlite/Migrations/AppDbContextModelSnapshot.cs", ByHand, false)]
    [InlineData("src/Topogenia.Persistence.SqlServer/Migrations/Helpers.g.cs", Generated + "migrationBuilder.Sql(\"SELECT 1\");", false)]
    [InlineData("src/Topogenia.Persistence.SqlServer/Migrations/20261023083307_RolesAndReportViews.Designer.cs", Generated, true)]
    [InlineData("src/Topogenia.Persistence.Sqlite/Migrations/AppDbContextModelSnapshot.cs", Generated, true)]
    [InlineData("src/Topogenia.Persistence.Sqlite/Migrations/20261023074737_Initial.cs", ByHand, true)]
    [InlineData("web/src/api/generated/schema.ts", ByHand, true)]
    public void Only_a_file_written_by_a_tool_where_a_tool_writes_is_generated(string relative, string text, bool generated) =>
        Assert.Equal(generated, RepositoryFiles.IsGenerated(relative, text));

    private const string Generated = "// <auto-generated />\nnamespace N;\n";

    private const string ByHand = "// RawQueries.g.cs\n// Queries written by hand in a file named as a generated one.\nnamespace N;\n";

    [Theory]
    [InlineData("migrationBuilder.Sql(\"GRANT SELECT ON SCHEMA::bi TO r;\");", true)]
    [InlineData("migrationBuilder.Sql(\n    \"\"\"\n    ALTER DATABASE CURRENT SET ALLOW_SNAPSHOT_ISOLATION ON;\n    \"\"\",\n    suppressTransaction: true);", true)]
    [InlineData("migrationBuilder.AddColumn<string>(name: \"code\", table: \"step\");", false)]
    [InlineData("migrationBuilder.CreateTable(name: \"step\", columns: table => new { });", false)]
    public void A_migration_with_sql_written_by_hand_is_recognised(string text, bool byHand) =>
        Assert.Equal(byHand, RepositoryFiles.WritesSqlByHand(text));

    [Fact]
    public void The_application_needs_neither_the_system_database_nor_the_server_procedures()
    {
        var offenders = RepositoryFiles.Enumerate(["src"], ".cs", ".sql", ".json")
            .Where(file => file.EndsWith(".cs", StringComparison.Ordinal)
                ? SourceDetectors.UsesSystemDatabaseInCSharp(File.ReadAllText(file))
                : SourceDetectors.UsesSystemDatabase(File.ReadAllText(file)))
            .Select(RepositoryFiles.Relative)
            .ToList();
        Assert.Empty(offenders);
    }

    [Fact]
    public void No_source_file_exceeds_600_lines()
    {
        var offenders = AllSources()
            .Where(file => !RepositoryFiles.IsMigration(file))
            .Select(file => (Relative: RepositoryFiles.Relative(file), Lines: File.ReadAllLines(file).Length))
            .Where(f => f.Lines > SourceDetectors.MaxFileLines)
            .Select(f => $"{f.Relative}: {f.Lines}")
            .ToList();
        Assert.Empty(offenders);
    }

    [Fact]
    public void No_method_lambda_or_top_level_block_exceeds_80_lines()
    {
        var offenders = RepositoryFiles.AllCSharp()
            .Where(file => !RepositoryFiles.IsMigration(file))
            .SelectMany(file => SourceDetectors.LongMembers(File.ReadAllText(file))
                .Select(member => $"{RepositoryFiles.Relative(file)}:{member.Line} ({member.Lines} lines)"))
            .ToList();
        Assert.Empty(offenders);
    }

    [Fact]
    public void No_work_marker_in_any_source()
    {
        var offenders = AllSources()
            .Where(file => SourceDetectors.ContainsWorkMarker(File.ReadAllText(file)))
            .Select(RepositoryFiles.Relative)
            .ToList();
        Assert.Empty(offenders);
    }

    [Fact]
    public void The_product_name_is_written_only_in_its_constant()
    {
        var name = Topogenia.Application.ProductIdentity.Name;
        var offenders = RepositoryFiles.ProductionCSharp()
            .Where(file => RepositoryFiles.Relative(file) != "src/Topogenia.Application/ProductIdentity.cs" && !RepositoryFiles.IsMigration(file))
            .Where(file => SourceDetectors.WritesProductName(File.ReadAllText(file), name))
            .Select(RepositoryFiles.Relative)
            .ToList();
        Assert.Empty(offenders);
    }

    /// <summary>
    /// ADR 013: the technical names of the persistence (the SQL Server schema, the roles, the assembly of the persistence
    /// tests) come from <c>TechnicalNames</c>, fixed like the namespaces, never from the displayed product name. In the
    /// persistence, only the folder and the file of the data of the desktop mode follow the product (spec 5.2, ADR 035).
    /// </summary>
    [Fact]
    public void The_persistence_derives_no_technical_name_from_the_displayed_product_name()
    {
        var offenders = RepositoryFiles.Enumerate(["src"], ".cs")
            .Where(file => RepositoryFiles.Relative(file).StartsWith("src/Topogenia.Persistence", StringComparison.Ordinal))
            .Where(file => RepositoryFiles.Relative(file) != "src/Topogenia.Persistence.Sqlite/Files/DesktopDataFile.cs")
            .Where(file => SourceDetectors.ReadsProductName(File.ReadAllText(file)))
            .Select(RepositoryFiles.Relative)
            .ToList();
        Assert.Empty(offenders);
        Assert.True(SourceDetectors.ReadsProductName(File.ReadAllText(Path.Combine(RepositoryFiles.Root, "src", "Topogenia.Persistence.Sqlite", "Files", "DesktopDataFile.cs"))));
    }

    /// <summary>Project paths such as the host project folder (spec 3.1) are allowed; any other mention is not.</summary>
    [Fact]
    public void The_interface_writes_the_product_name_only_in_its_translation_files()
    {
        var name = Topogenia.Application.ProductIdentity.Name;
        var offenders = RepositoryFiles.Enumerate(["web/src", "web/e2e", "web/scripts"], ".ts", ".tsx", ".css", ".mjs", ".html", ".json")
            .Concat(RepositoryFiles.Shallow(["web"], ".ts", ".mjs", ".html"))
            .Where(file => !RepositoryFiles.Relative(file).StartsWith("web/src/shared/i18n/locales/", StringComparison.Ordinal))
            .Where(file => System.Text.RegularExpressions.Regex.IsMatch(File.ReadAllText(file),
                System.Text.RegularExpressions.Regex.Escape(name) + @"(?!\.[A-Z])", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            .Select(RepositoryFiles.Relative)
            .ToList();
        Assert.Empty(offenders);
    }

    /// <summary>
    /// D08. One exception, the fixed time of the entries of a package archive, which the framework types as an offset date
    /// (ADR 051); the exception names its file and the file must still hold the use, so that it never outlives its reason.
    /// </summary>
    [Fact]
    public void Production_code_uses_utc_dates_only()
    {
        const string zipTime = "src/Topogenia.Interchange/Packages/ZipEntryTime.cs";
        Assert.Empty(SemanticDetectors.Offenders(SemanticSources.Production, SemanticDetectors.LocalDates, zipTime));
        Assert.NotEmpty(SemanticSources.Production.Where(file => file.Path == zipTime).SelectMany(SemanticDetectors.LocalDates));
    }

    [Fact]
    public void Shipped_code_calls_no_external_address()
    {
        var offenders = RepositoryFiles.ProductionCSharp()
            .Concat(RepositoryFiles.Enumerate(["web/src"], ".ts", ".tsx", ".css", ".html"))
            .Concat(RepositoryFiles.Shallow(["web"], ".html"))
            .Where(file => SourceDetectors.ContainsExternalUrl(File.ReadAllText(file)))
            .Select(RepositoryFiles.Relative)
            .ToList();
        Assert.Empty(offenders);
    }

    /// <summary>
    /// The one exception to D14 decided for 3.0 (spec 3.0 section 6): the connectors of the assistant call the model that
    /// the administrator chose in the settings, never by default; they live in this project and nowhere else.
    /// </summary>
    private const string AssistantConnectors = "src/Topogenia.Assistant/";

    /// <summary>
    /// D14: no outgoing call, whatever the mode. Beyond the addresses written in the text (the rule above), the shipped code
    /// uses no API that connects to another machine (<see cref="ShippedCodeDetectors.OutgoingConnections"/>), read on the
    /// resolved symbols of every production project. The database is reached through the providers of EF Core only, at
    /// the address that the operator configures; the directory of Windows, which the server mode reads for the users and
    /// groups, is the other allowed exception (ADR 014, ADR 045); the connectors of the assistant are the exception of 3.0.
    /// </summary>
    [Fact]
    public void Shipped_code_opens_no_outgoing_connection() =>
        Assert.Empty(SemanticDetectors.Offenders(SemanticSources.Production, ShippedCodeDetectors.OutgoingConnections, AssistantConnectors));

    /// <summary>
    /// Spec 3.0 section 6: a client of the SDK of Anthropic or of the AWS SDK, which calls a model, appears only in the
    /// connectors of the assistant; and both connectors do use theirs (the rule reads them, it is not blind to them).
    /// </summary>
    [Fact]
    public void Only_the_connectors_of_the_assistant_call_a_model()
    {
        Assert.Empty(SemanticDetectors.Offenders(SemanticSources.Production, ShippedCodeDetectors.ModelClients, AssistantConnectors));
        var places = SemanticSources.Production.Where(file => file.Path.StartsWith(AssistantConnectors, StringComparison.Ordinal))
            .SelectMany(ShippedCodeDetectors.ModelClients).ToList();
        Assert.Contains(places, place => place.StartsWith($"{AssistantConnectors}AnthropicConnector.cs:", StringComparison.Ordinal));
        Assert.Contains(places, place => place.StartsWith($"{AssistantConnectors}BedrockConnector.cs:", StringComparison.Ordinal));
    }

    /// <summary>
    /// S-5.4-13 (F-06): the server never follows the location entered for a document, a UNC path being a share of another
    /// machine. Only the files below open a file or a folder by its path (<see cref="ShippedCodeDetectors.FileSystemAccesses"/>:
    /// the APIs of files, the connection strings of SQLite, the start of a process, XML, `file` URIs...), each for a path
    /// of its own that the operator, the command line or the product chooses, never one entered in a map.
    /// </summary>
    private static readonly string[] FileSystemFiles =
    [
        // The desktop mode: its folders and data file, the protection keys of its cookie, its settings, its backups.
        "src/Topogenia.Host/Desktop/DesktopDatabase.cs", "src/Topogenia.Host/Desktop/DesktopPaths.cs", "src/Topogenia.Host/Desktop/DesktopRecovery.cs",
        "src/Topogenia.Host/Desktop/DesktopServices.cs", "src/Topogenia.Host/Desktop/DesktopSettings.cs",
        // The folder of the executable, where the host reads its settings and its pages.
        "src/Topogenia.Host/Composition/HostApplication.cs",
        // The daily log, in the folder of the settings (ADR 045).
        "src/Topogenia.Host/Logging/FileLoggerProvider.cs",
        // The files named on the command line by the operator (--export, --import, --check).
        "src/Topogenia.Host/Startup/ExchangeCommands.cs", "src/Topogenia.Host/Startup/HostCommands.cs",
        // The browser of the user, opened by the shell on the local address of the desktop mode (spec 2 D10).
        "src/Topogenia.Host/Startup/StartupService.cs",
        // The scripts of the migrations, embedded in the assembly.
        "src/Topogenia.Persistence/Schema/MigrationSources.cs",
        // The instructions of the assistant and the documentation of the formats, embedded in the assembly of the connectors (3.0).
        "src/Topogenia.Assistant/AssistantInstructions.cs",
        // The SQLite file of the desktop mode, its connection, its journal, its backups and the checks of its folder (ADR 035, ADR 041).
        "src/Topogenia.Persistence.Sqlite/SqliteEngine.cs",
        "src/Topogenia.Persistence.Sqlite/Backup/SqliteBackupService.cs", "src/Topogenia.Persistence.Sqlite/Backup/SqliteRestore.cs",
        "src/Topogenia.Persistence.Sqlite/Connection/SqliteDataFileGuard.cs", "src/Topogenia.Persistence.Sqlite/Connection/SqliteJournalCheck.cs",
        "src/Topogenia.Persistence.Sqlite/Connection/SqliteWalFile.cs", "src/Topogenia.Persistence.Sqlite/Files/DataFileEnvironment.cs",
        "src/Topogenia.Persistence.Sqlite/Files/DataFileUse.cs", "src/Topogenia.Persistence.Sqlite/Files/FileIdentity.cs",
        "src/Topogenia.Persistence.Sqlite/Files/FolderExposure.cs",
    ];

    /// <summary>
    /// The listed files read no document (<see cref="ShippedCodeDetectors.DocumentReads"/>: the record, its input, its
    /// address, the body of its route, the document of a `.pmap`), and no file that reads one uses a type declared in a
    /// listed file (a helper that takes a path, called with the location entered). The flows of data are not followed
    /// further (a location handed on as a plain string through a third file): `DocumentLocationNeverOpenedTests` of the
    /// host proves at the run, on every route that writes, reads, copies or exports a document, that the location is
    /// never opened.
    /// </summary>
    [Fact]
    public void Shipped_code_opens_files_only_where_listed_and_never_reads_a_document_there()
    {
        var opening = SemanticSources.Production.Where(file => ShippedCodeDetectors.FileSystemAccesses(file).Any()).Select(file => file.Path).Order(StringComparer.Ordinal).ToList();
        Assert.Equal(FileSystemFiles.Order(StringComparer.Ordinal), opening);
        var listed = SemanticSources.Production.Where(file => FileSystemFiles.Contains(file.Path)).ToList();
        Assert.Empty(listed.SelectMany(ShippedCodeDetectors.DocumentReads));
        var openers = listed.SelectMany(ShippedCodeDetectors.DeclaredTypes).ToHashSet(StringComparer.Ordinal);
        var readers = SemanticSources.Production.Where(file => ShippedCodeDetectors.DocumentReads(file).Any()).ToList();
        Assert.Empty(readers.SelectMany(file => ShippedCodeDetectors.UsesOf(file, openers)));
        // Positive controls: the document and its location are read elsewhere in the shipped code (the sheet, the
        // exports), and the listed files declare the types that the readers must not use.
        Assert.Contains(readers, file => file.Path == "src/Topogenia.Host/Api/Documents/DocumentApi.cs");
        Assert.Contains("Topogenia.Host.Desktop.DesktopSettings", openers);
    }

    [Theory]
    [InlineData("class A { string M(string path) => System.IO.File.ReadAllText(path); }")]
    [InlineData("using System.IO; class A { object M(string path) => new FileInfo(path).Length; }")]
    [InlineData("using static System.IO.Directory; class A { bool M(string path) => Exists(path); }")]
    [InlineData("class A { object M(string path) => new System.IO.FileStream(path, System.IO.FileMode.Open); }")]
    [InlineData("class A { object M(string path) => new System.IO.StreamReader(path); }")]
    [InlineData("class A { object M(string path) => System.IO.Compression.ZipFile.OpenRead(path); }")]
    [InlineData("using System.IO.Compression; class A { void M(ZipArchive zip, string path) => zip.ExtractToDirectory(path); }")]
    [InlineData("class A { object M(string path) => new System.IO.StreamWriter(path); }")]
    [InlineData("class A { object M(string path) => new System.IO.FileSystemWatcher(path); }")]
    [InlineData("class A { object M(string path) => new System.IO.DriveInfo(path); }")]
    [InlineData("class A { object M(string path) => System.IO.MemoryMappedFiles.MemoryMappedFile.CreateFromFile(path); }")]
    [InlineData("class A { object M(string path) => new Microsoft.Extensions.FileProviders.PhysicalFileProvider(path); }")]
    [InlineData("class A { object M(string path) => System.Diagnostics.FileVersionInfo.GetVersionInfo(path); }")]
    [InlineData("class A { string M(Microsoft.AspNetCore.Http.HttpResults.PhysicalFileHttpResult reply) => reply.FileName; }")]
    public void Every_way_to_open_a_file_by_its_path_is_detected(string sample) =>
        Assert.NotEmpty(ShippedCodeDetectors.FileSystemAccesses(SemanticSources.Sample(sample, "src/Topogenia.Host/Sample.cs")));

    /// <summary>The other ways to open a path given as text: a connection string, a process, XML, a `file` URI, a load from a file.</summary>
    [Theory]
    [InlineData("using Microsoft.Data.Sqlite; class A { void M(string location) => new SqliteConnection(\"Data Source=\" + location).Open(); }")]
    [InlineData("using Microsoft.Data.Sqlite; class A { object M(string text) => new SqliteConnection(text); }")]
    [InlineData("using Microsoft.Data.Sqlite; class A { object M(string text) => new SqliteConnection { ConnectionString = text }; }")]
    [InlineData("class A { void M(System.Data.Common.DbConnection connection, string text) => connection.ConnectionString = text; }")]
    [InlineData("using Microsoft.Data.Sqlite; class A { string M(string path) => new SqliteConnectionStringBuilder { DataSource = path }.ConnectionString; }")]
    [InlineData("using Microsoft.Data.Sqlite; class A { void M(SqliteConnectionStringBuilder builder, string path) => builder.DataSource = path; }")]
    [InlineData("class A { void M(System.Data.Common.DbConnectionStringBuilder builder, string path) => builder[\"Data Source\"] = path; }")]
    [InlineData("class A { string M(string path) => $\"Mode=ReadOnly;Data Source={path}\"; }")]
    [InlineData("class A { string M(string path) => \"Server=.;AttachDbFilename=\" + path; }")]
    [InlineData("using Microsoft.EntityFrameworkCore; class A { void M(DbContextOptionsBuilder options, string text) => options.UseSqlite(text); }")]
    [InlineData("class A { void M(string location) => System.Diagnostics.Process.Start(location); }")]
    [InlineData("using System.Diagnostics; class A { void M(Process process) => process.Start(); }")]
    [InlineData("using System.Diagnostics; class A { object M(string location) => new ProcessStartInfo(location) { UseShellExecute = true }; }")]
    [InlineData("class A { object M(string location) => System.Xml.XmlReader.Create(location); }")]
    [InlineData("class A { object M(string location) => System.Xml.Linq.XDocument.Load(location); }")]
    [InlineData("class A { void M(System.Xml.Linq.XElement element, string location) => element.Save(location); }")]
    [InlineData("class A { void M(System.Xml.XmlDocument document, string location) => document.Load(location); }")]
    [InlineData("class A { object M(string location) => new System.Xml.XPath.XPathDocument(location); }")]
    [InlineData("class A { object M() => new System.Xml.XmlUrlResolver(); }")]
    [InlineData("class A { object M(string share) => new System.Uri(\"file://\" + share); }")]
    [InlineData("class A { bool M(System.Uri uri) => uri.Scheme == System.Uri.UriSchemeFile; }")]
    [InlineData("class A { string M(System.Uri uri) => uri.IsUnc ? uri.LocalPath : \"\"; }")]
    [InlineData("class A { object M(string path) => System.Reflection.Assembly.LoadFrom(path); }")]
    [InlineData("using System.Security.Cryptography.X509Certificates; class A { object M(string path) => X509CertificateLoader.LoadCertificateFromFile(path); }")]
    [InlineData("class A { object M(string path) => Microsoft.AspNetCore.Http.TypedResults.PhysicalFile(path); }")]
    [InlineData("using Microsoft.AspNetCore.Http; class A { Task M(HttpResponse response, string path) => response.SendFileAsync(path); }")]
    [InlineData("using Microsoft.Extensions.Configuration; class A { void M(IConfigurationBuilder builder, string path) => builder.AddJsonFile(path); }")]
    [InlineData("class A { object M(string folder) => new Microsoft.AspNetCore.Builder.WebApplicationOptions { ContentRootPath = folder }; }")]
    [InlineData("class A { object M(string path) => new Microsoft.Data.SqlClient.SqlConnectionStringBuilder { AttachDBFilename = path }; }")]
    [InlineData("class A { object M(string location) => System.Xml.XmlWriter.Create(location); }")]
    [InlineData("class A { object M(string location) => new System.Xml.XmlTextReader(location); }")]
    [InlineData("class A { object M(string location) => new System.Xml.XmlTextWriter(location, null); }")]
    [InlineData("class A { void M(System.Xml.Xsl.XslCompiledTransform transform, string location) => transform.Load(location); }")]
    [InlineData("class A { void M(System.Data.DataSet data, string location) => data.ReadXml(location); }")]
    [InlineData("class A { object M(string path) => new System.Security.Cryptography.X509Certificates.X509Certificate(path); }")]
    [InlineData("class A { object M(string path) => new System.Security.Cryptography.X509Certificates.X509Certificate2(path); }")]
    [InlineData("using System.Security.Cryptography.X509Certificates; class A { object M(string path) => X509Certificate.CreateFromCertFile(path); }")]
    [InlineData("using System.Security.Cryptography.X509Certificates; class A { object M(string path) => X509Certificate2.CreateFromPemFile(path); }")]
    [InlineData("class A { object M(string path) => System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromAssemblyPath(path); }")]
    [InlineData("class A { object M(string path) => System.Runtime.InteropServices.NativeLibrary.Load(path); }")]
    [InlineData("using Microsoft.AspNetCore.Http; class A { object M(string path) => Results.File(path); }")]
    [InlineData("using Microsoft.Extensions.Configuration; class A { void M(IConfigurationBuilder builder, string folder) => builder.SetBasePath(folder); }")]
    [InlineData("using Microsoft.AspNetCore.Hosting; class A { void M(IWebHostBuilder builder, string folder) => builder.UseContentRoot(folder); }")]
    [InlineData("using Microsoft.Extensions.Hosting; class A { void M(IHostBuilder builder, string folder) => builder.UseContentRoot(folder); }")]
    public void Every_way_to_open_a_path_given_as_text_is_detected(string sample) =>
        Assert.NotEmpty(ShippedCodeDetectors.FileSystemAccesses(SemanticSources.Sample(sample, "src/Topogenia.Host/Sample.cs")));

    [Theory]
    [InlineData("class A { string M(string path) => System.IO.Path.GetFileName(path); }")]
    [InlineData("class A { int M(System.IO.Stream stream) => stream.ReadByte(); }")]
    [InlineData("class A { } // File.ReadAllText(path) in a comment")]
    [InlineData("using Microsoft.Data.Sqlite; class A { bool M(System.Data.Common.DbConnection connection) => connection is SqliteConnection; }")]
    [InlineData("using Microsoft.Data.Sqlite; class A { string M(SqliteConnection connection) => new SqliteConnectionStringBuilder(connection.ConnectionString).DataSource; }")]
    [InlineData("using Microsoft.Data.Sqlite; class A { object M() => new SqliteConnection(); }")]
    [InlineData("class A { object M(System.IO.Stream stream) => System.Xml.XmlReader.Create(stream); }")]
    [InlineData("class A { object M(System.IO.Stream stream) => System.Xml.Linq.XDocument.Load(stream); }")]
    [InlineData("class A { object M(string xml) => System.Xml.Linq.XDocument.Parse(xml); }")]
    [InlineData("class A { object M(System.IO.Stream stream) => new System.Xml.XPath.XPathDocument(stream); }")]
    [InlineData("class A { bool M(string text) => System.Uri.TryCreate(text, System.UriKind.Absolute, out var uri) && uri.Scheme == System.Uri.UriSchemeHttps; }")]
    [InlineData("class A { int M(System.Uri uri) => uri.Port; }")]
    [InlineData("class A { int M() => System.Diagnostics.Process.GetCurrentProcess().Id; }")]
    [InlineData("using Microsoft.AspNetCore.Http; class A { object M(byte[] content) => TypedResults.File(content, \"text/plain\", \"copy.pmap\"); }")]
    [InlineData("using Microsoft.AspNetCore.Http; class A { object M(System.IO.Stream content) => Results.File(content, \"application/zip\", \"copy.zip\"); }")]
    [InlineData("class A { string M(string name) => $\"attachment; filename={name}; the data source of the report\"; }")]
    [InlineData("class A { string M(string profile) => \"profile: \" + profile; }")]
    [InlineData("class A { } // new SqliteConnection(\"Data Source=x\") and Process.Start(location) in a comment")]
    public void A_path_computed_or_a_stream_already_open_is_no_file_opened(string sample) =>
        Assert.Empty(ShippedCodeDetectors.FileSystemAccesses(SemanticSources.Sample(sample, "src/Topogenia.Host/Sample.cs")));

    /// <summary>A file that reads the location of a document and calls a helper of a file that opens paths is seen; one that calls another file is not.</summary>
    [Theory]
    [InlineData("static class Probe { public static bool Exists(string path) => System.IO.File.Exists(path); }",
        "class Route { bool M(Topogenia.Domain.Model.Document document) => Probe.Exists(document.Location!); }", true)]
    [InlineData("static class Probe { public static bool Exists(string path) => System.IO.File.Exists(path); }",
        "static class Pages { public static string Of(string text) => text.Trim(); } class Route { string M(Topogenia.Domain.Model.Document document) => Pages.Of(document.Location!); }", false)]
    public void A_reader_of_a_document_that_calls_a_file_that_opens_paths_is_detected(string opener, string reader, bool detected)
    {
        var files = SemanticSources.Samples(("src/Topogenia.Host/Opener.cs", opener), ("src/Topogenia.Host/Reader.cs", reader));
        Assert.NotEmpty(ShippedCodeDetectors.FileSystemAccesses(files[0]));
        Assert.NotEmpty(ShippedCodeDetectors.DocumentReads(files[1]));
        var openers = ShippedCodeDetectors.DeclaredTypes(files[0]).ToHashSet(StringComparer.Ordinal);
        Assert.Equal(detected, ShippedCodeDetectors.UsesOf(files[1], openers).Any());
    }

    private static bool OpensConnection(string sample) =>
        ShippedCodeDetectors.OutgoingConnections(SemanticSources.Sample(sample, "src/Topogenia.Host/Sample.cs")).Any();

    [Theory]
    [InlineData("class A { async Task M() { using var client = new HttpClient(); await client.GetStringAsync(\"http://127.0.0.1/\"); } }")]
    [InlineData("class A { object M(IHttpClientFactory factory) => factory.CreateClient(); }")]
    [InlineData("using Microsoft.Extensions.DependencyInjection; class A { void M(IServiceCollection services) => services.AddHttpClient(\"update\"); }")]
    [InlineData("using Web = System.Net.Http.HttpClient; class A { Web client = new(); }")]
    [InlineData("class Handler : HttpClientHandler { }")]
    [InlineData("class A { object M() => System.Net.WebRequest.Create(\"http://127.0.0.1/\"); }")]
    [InlineData("class A { object client = new System.Net.WebClient(); }")]
    [InlineData("using System.Net.Sockets; class A { object M() => new TcpClient(\"server\", 443); }")]
    [InlineData("using System.Net.Sockets; class A { object M() => new UdpClient(); }")]
    [InlineData("using System.Net.Sockets; class A { void M(Socket socket) => socket.Connect(\"server\", 443); }")]
    [InlineData("using static System.Net.Dns; class A { object M() => GetHostAddresses(\"server\"); }")]
    [InlineData("class A { object socket = new System.Net.WebSockets.ClientWebSocket(); }")]
    [InlineData("class A { object mail = new System.Net.Mail.SmtpClient(\"server\"); }")]
    [InlineData("class A { object ping = new System.Net.NetworkInformation.Ping(); }")]
    public void Every_way_to_open_an_outgoing_connection_is_detected(string sample) => Assert.True(OpensConnection(sample));

    [Theory]
    [InlineData("using System.Net; class A { string M(int port) => $\"{IPAddress.Loopback}:{port}\"; }")]
    [InlineData("using System.Diagnostics; class A { void M() => Process.Start(new ProcessStartInfo(\"http://127.0.0.1:5080/\") { UseShellExecute = true }); }")]
    [InlineData("class A { int M(Microsoft.AspNetCore.Http.HttpContext context) => context.Response.StatusCode; }")]
    [InlineData("namespace N { class HttpClient { public void Get() { } } class A { void M() => new HttpClient().Get(); } }")]
    [InlineData("class A { } // new HttpClient(), new TcpClient() and Dns.GetHostAddresses in a comment")]
    public void The_loopback_the_browser_the_request_served_and_look_alike_names_are_no_outgoing_connection(string sample) =>
        Assert.False(OpensConnection(sample));
}
