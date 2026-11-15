// SourceRulesTests.cs
// Rules checked on the source text: raw SQL, triggers, system database, sizes, logger messages of a partial class in one file, work markers, product name, UTC dates, outgoing URLs and connections (the table of the providers of the assistant alone, ADR 098); the paths opened are in the other file of the class (spec 3.2, 4.4, 5.4, D04, D08, D13, D14).

using System.Text.RegularExpressions;

namespace Topogenia.Architecture.Tests;

public sealed partial class SourceRulesTests
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
    public void A_partial_class_carries_its_logger_messages_in_one_of_its_files()
    {
        var offenders = SourceDetectors.SplitLoggerMessages(RepositoryFiles.ProductionCSharp().Select(file => (RepositoryFiles.Relative(file), File.ReadAllText(file))))
            .Select(split => $"{split.Type}: {string.Join(", ", split.Files)}")
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

    /// <summary>
    /// The table of the providers of the assistant (ADR 098): the only file of the shipped code where the address of a
    /// provider is written, each one listed in `docs/securite.md`.
    /// </summary>
    private const string ProviderTable = "src/Topogenia.Assistant/AssistantProviders.cs";

    [Fact]
    public void Shipped_code_calls_no_external_address()
    {
        var offenders = RepositoryFiles.ProductionCSharp()
            .Concat(RepositoryFiles.Enumerate(["web/src"], ".ts", ".tsx", ".css", ".html"))
            .Concat(RepositoryFiles.Shallow(["web"], ".html"))
            .Where(file => RepositoryFiles.Relative(file) != ProviderTable && SourceDetectors.ContainsExternalUrl(File.ReadAllText(file)))
            .Select(RepositoryFiles.Relative)
            .ToList();
        Assert.Empty(offenders);
        // The table is not spared whole: its addresses in HTTPS are held to the list of the security document (the rule
        // below), and it writes no address in clear but the loopback of the machine.
        Assert.Empty(ClearAddresses(File.ReadAllText(Path.Combine(RepositoryFiles.Root, ProviderTable))));
    }

    /// <summary>
    /// The addresses a table of the providers writes that are refused: every address that is not in HTTPS, whatever its
    /// scheme, but the loopback of the machine by its literal address, at a port and with a path only (the models of this
    /// machine, in clear since nothing leaves it). A host named (`localhost` too, which the machine resolves), a host
    /// built from a text of the settings, or a loopback followed by another host is refused (R5-18).
    /// </summary>
    private static IReadOnlyList<string> ClearAddresses(string table) =>
        [.. Regex.Matches(table, @"\b(?!https://)[a-z][a-z0-9+.-]*://[^""\r\n]*", RegexOptions.IgnoreCase).Select(match => match.Value)
            .Where(address => !Regex.IsMatch(address, @"^http://127\.0\.0\.1:(\d+|\{[^{}]+\})(/[^@\\]*)?$"))];

    [Theory]
    [InlineData("new(Kind.Other, \"http://host.example/v1\", true),")]
    [InlineData("Kind.Other => \"HTTP://HOST.EXAMPLE/V1\",")]
    [InlineData("Kind.Other => $\"http://{settings.Resource}/v1\",")]
    [InlineData("Kind.Other => $\"http://{settings.Resource}.host.example/v1\",")]
    [InlineData("Kind.Local => $\"http://localhost:{settings.Port}/v1\",")]
    [InlineData("Kind.Local => $\"http://127.0.0.1.host.example:{settings.Port}/v1\",")]
    [InlineData("Kind.Local => $\"http://127.0.0.1:{settings.Port}@host.example/v1\",")]
    [InlineData("Kind.Local => $\"http://127.0.0.1:{settings.Port}/v1/../@host.example\",")]
    [InlineData("Kind.Local => $\"http://127.0.0.1{settings.Resource}:{settings.Port}/v1\",")]
    [InlineData("Kind.Local => $\"http://[::1]:{settings.Port}/v1\",")]
    [InlineData("Kind.Other => \"ws://127.0.0.1:1234/v1\",")]
    [InlineData("Kind.Other => \"ftp://files.host.example/models\",")]
    [InlineData("// See http://host.example/v1 for the API.")]
    public void A_table_of_the_providers_with_an_address_in_clear_outside_the_loopback_is_refused(string line) =>
        Assert.Single(ClearAddresses($"public static class Table\n{{\n    {line}\n    new(Kind.Maker, \"https://api.host.example/v1\", true),\n}}\n"));

    [Theory]
    [InlineData("new(Kind.Maker, \"https://api.host.example/v1\", true),")]
    [InlineData("Kind.Cloud => $\"https://{settings.Resource}.host.example/openai/v1\",")]
    [InlineData("Kind.Local => $\"http://127.0.0.1:{settings.Port ?? AssistantConnectors.DefaultPort(kind)}/v1\",")]
    [InlineData("Kind.Local => \"http://127.0.0.1:11434/v1\",")]
    [InlineData("Kind.Local => $\"http://127.0.0.1:{settings.Port}\",")]
    public void A_table_of_the_providers_in_https_or_on_the_loopback_by_its_literal_address_is_admitted(string line) =>
        Assert.Empty(ClearAddresses($"public static class Table\n{{\n    {line}\n}}\n"));

    /// <summary>
    /// ADR 098: the table of the providers does write addresses (the rule above is not blind to it), each host of it is in
    /// the list of the security document, and no address is built from a text but the name of a resource of Azure.
    /// </summary>
    [Fact]
    public void The_addresses_of_the_providers_of_the_assistant_are_those_the_security_document_lists()
    {
        var table = File.ReadAllText(Path.Combine(RepositoryFiles.Root, ProviderTable));
        Assert.True(SourceDetectors.ContainsExternalUrl(table));
        var hosts = Regex.Matches(table, @"https://(\{[^}]+\}\.)?([a-z0-9.-]+)").Select(match => match.Groups[2].Value).Distinct().ToList();
        Assert.Equal(8, hosts.Count);
        var security = File.ReadAllText(Path.Combine(RepositoryFiles.Root, "docs", "securite.md"));
        Assert.All(hosts, host => Assert.Contains($"`{host}`", security, StringComparison.Ordinal));
        Assert.Equal(["{settings.Resource}"], Regex.Matches(table, @"https://(\{[^}]+\})").Select(match => match.Groups[1].Value).Distinct());
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
