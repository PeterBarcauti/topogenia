// DetectorSamplesTests.cs
// Proves that each source detector catches faulty samples and accepts correct ones, so that a green rule means something.

namespace Topogenia.Architecture.Tests;

public sealed class DetectorSamplesTests
{
    private const string Product = "Acme";

    [Theory]
    [InlineData("""class A { string s = "SELECT id FROM step"; }""")]
    [InlineData("""class A { string s = "SELECT id " + "FROM step"; }""")]
    [InlineData("class A { string s = \"\"\"\n    UPDATE step\n    SET label = 'x'\n    \"\"\"; }")]
    [InlineData("class A { string s = @\"DELETE\nFROM flow\"; }")]
    [InlineData("""class A { string s = "UPDATE dbo.Maps SET x = 1"; }""")]
    [InlineData("""class A { string s = "UPDATE [Maps] SET x = 1"; }""")]
    [InlineData("""class A { string s = "MERGE INTO t USING s ON 1=1"; }""")]
    [InlineData("""class A { string s = "TRUNCATE TABLE t"; }""")]
    [InlineData("""class A { string s = "DROP VIEW bi.step"; }""")]
    [InlineData("""class A { string s = "EXEC topogenia.purge"; }""")]
    [InlineData("""class A { string s = $"INSERT INTO {t} VALUES (1)"; }""")]
    [InlineData("""class A { void M(object db) { db.FromSqlRaw("x"); } }""")]
    [InlineData("""class A { void M(object db) { db.ExecuteSqlInterpolated($"x"); } }""")]
    [InlineData("""class A { string s = "PRAGMA foreign_keys = ON"; }""")]
    [InlineData("""class A { string s = "select id, label from step where map_id = @m"; }""")]
    [InlineData("""class A { string s = "insert into flow values (1)"; }""")]
    [InlineData("""class A { static string Q() => "SELECT id FROM step"; }""")]
    [InlineData("""class A { A() => _ = "DELETE FROM flow"; }""")]
    [InlineData("""class A { void M(string q = "UPDATE step SET x = 1") { } }""")]
    [InlineData("""class A { string s = "select s.id from step s where s.map_id = 1"; }""")]
    [InlineData("""class A { string s = "delete from topogenia.step where id = 1"; }""")]
    [InlineData("""class A { string s = "update topogenia.step set label = 'x'"; }""")]
    [InlineData("""class A { string s = "Select Id From Step"; }""")]
    [InlineData("""class A { string s = "select m.id from map m join step s on s.map_id = m.id"; }""")]
    [InlineData("""class A { string s = "select count(*) from step"; }""")]
    [InlineData("""class A { string s = "select max(number) from sheet_version where sheet_id = @s"; }""")]
    [InlineData("""class A { string s = "select s.* from step s"; }""")]
    [InlineData("""class A { string s = $"select {c} from step s where s.id = 1"; }""")]
    [InlineData("""class A { void M(dynamic cmd) { cmd.CommandText = "x"; } }""")]
    [InlineData("""class A { void M(dynamic e) { e.HasFilter("deleted_at IS NULL"); } }""")]
    [InlineData("""class A { void M(dynamic e) { e.HasCheckConstraint("ck", "x > 0"); } }""")]
    [InlineData("""class A { void M(dynamic e) { e.HasDefaultValueSql("now()"); } }""")]
    [InlineData("""class A { string s = "insert or ignore into flow values (1)"; }""")]
    [InlineData("""class A { string s = "exec sp_getapplock"; }""")]
    [InlineData("""class A { string s = "vacuum into 'copy.db'"; }""")]
    [InlineData("""class A { string s = "pragma integrity_check"; }""")]
    [InlineData("""class A { string F(string c) => $"{c} IS NULL"; }""")]
    [InlineData("""class A { string F(string c) => $"{c} IS NOT NULL"; }""")]
    [InlineData("""class A { string F(string c, string v) => $"{c} IN ({v})"; }""")]
    [InlineData("""class A { string F(string c) => $"{c} BETWEEN 0 AND 100"; }""")]
    [InlineData("""class A { string F(string a, string b) => $"{a} <> {b}"; }""")]
    [InlineData("""class A { string F(string[] p) => "(" + string.Join(" AND ", p) + ")"; }""")]
    [InlineData("""class A { string F(string[] p) { var o = " OR "; return string.Join(o, p); } }""")]
    [InlineData("""class A { string F(string v) => $"'{v.Replace("'", "''")}'"; }""")]
    [InlineData("""class A { string F(string c) => $"CAST({c} AS REAL)"; }""")]
    [InlineData("""class A { string s = "[label] LIKE 'A%'"; }""")]
    public void Raw_sql_is_detected(string sample) => Assert.True(SourceDetectors.ContainsRawSql(sample));

    [Theory]
    [InlineData("""class A { string s = "Select a step from the list"; }""")]
    [InlineData("""class A { string s = "Update the map"; }""")]
    [InlineData("""class A { int Select(int x) => x; }""")]
    [InlineData("""class A { string s = "Delete the selected steps from the map"; }""")]
    [InlineData("""class A { string s = "Insert a step before"; }""")]
    [InlineData("""class A { string s = "Select one of the steps from the list to paste it"; }""")]
    [InlineData("""class A { string s = "The count (of steps) from this map"; }""")]
    [InlineData("""class A { string s = "Between two steps and a flow"; }""")]
    [InlineData("""class A { string s = "Sign in (optional) or create an account"; }""")]
    [InlineData("""class A { string s = "Pick one or more lanes and a step"; }""")]
    [InlineData("""class A { bool F(string l) => l.Contains("COLLATE", System.StringComparison.Ordinal); }""")]
    [InlineData("""class A { string s = "<>"; }""")]
    public void Ordinary_text_is_not_taken_for_sql(string sample) => Assert.False(SourceDetectors.ContainsRawSql(sample));

    [Theory]
    [InlineData("CREATE TRIGGER t ON step AFTER INSERT")]
    [InlineData("create or alter procedure purge_project")]
    [InlineData("migrationBuilder.Sql(\"CREATE FUNCTION f()\");")]
    [InlineData("CREATE TEMP TRIGGER t AFTER INSERT ON step")]
    [InlineData("create temporary trigger t after update on flow")]
    [InlineData("CREATE/**/PROCEDURE p AS SELECT 1")]
    [InlineData("create -- a comment\ntrigger t after insert on step")]
    public void Triggers_and_procedures_are_detected(string sample) => Assert.True(SourceDetectors.CreatesTriggerOrProcedure(sample));

    /// <summary>The samples of the control of the architecture (gate P3, round 2): strings split, holes, SQL comments.</summary>
    [Theory]
    [InlineData("migrationBuilder.Sql(\"CREATE \" + \"TRIGGER t ON step AFTER INSERT AS SELECT 1\");")]
    [InlineData("migrationBuilder.Sql($\"CREATE {kind} purge AS SELECT 1\");")]
    [InlineData("migrationBuilder.Sql(\"CREATE/**/PROCEDURE p AS SELECT 1\");")]
    [InlineData("migrationBuilder.Sql(\"EXEC('CREATE PROC'+'EDURE p AS SELECT 1')\");")]
    [InlineData("migrationBuilder.Sql(\"CREATE TRIGGER t ON step AFTER INSERT AS SELECT 1\");")]
    [InlineData("class A { string s = \"CREATE OR ALTER\" + \" FUNCTION f() RETURNS int AS BEGIN RETURN 1 END\"; }")]
    public void Triggers_and_procedures_are_detected_in_csharp(string sample) => Assert.True(SourceDetectors.CreatesTriggerOrProcedureInCSharp(sample));

    /// <summary>
    /// The samples of the control of the architecture (gate P3, round 3): a keyword cut between two C# literals, between two
    /// SQL literals of a dynamic statement in C#, and between two SQL literals of a script; then the same cut written otherwise.
    /// </summary>
    [Theory]
    [InlineData("migrationBuilder.Sql(\"CREATE TRIG\" + \"GER t ON step AFTER INSERT AS SELECT 1\");", true)]
    [InlineData("migrationBuilder.Sql(\"EXEC(N'CRE' + N'ATE FUNCTION f() RETURNS int AS BEGIN RETURN 1 END')\");", true)]
    [InlineData("EXEC(N'CREATE TRIG' + N'GER t ON step AFTER INSERT AS SELECT 1')", false)]
    [InlineData("class A { string s = \"CREATE PROCE\" + \"DURE p AS SELECT 1\"; }", true)]
    [InlineData("class A { string s = $\"CREATE TRIG{\"GER\"} t ON step AFTER INSERT AS SELECT 1\"; }", true)]
    [InlineData("migrationBuilder.Sql(\"EXEC(N'CR' + \" + \"N'EATE FUNCTION f() RETURNS int AS BEGIN RETURN 1 END')\");", true)]
    [InlineData("migrationBuilder.Sql(\"EXEC(N'EXEC(N''CRE'' + N''ATE PROCEDURE p AS SELECT 1'')')\");", true)]
    [InlineData("migrationBuilder.Sql(\"EXEC(N'CRE' /* a */ + -- b\\n N'ATE PROCEDURE p AS SELECT 1')\");", true)]
    [InlineData("EXEC('ALT' + 'ER FUNCTION f() RETURNS int AS BEGIN RETURN 1 END')", false)]
    [InlineData("SELECT 'CREATE TRIG' || 'GER t AFTER INSERT ON step BEGIN SELECT 1; END'", false)]
    public void A_keyword_cut_between_two_literals_is_detected(string sample, bool csharp) =>
        Assert.True(csharp ? SourceDetectors.CreatesTriggerOrProcedureInCSharp(sample) : SourceDetectors.CreatesTriggerOrProcedure(sample));

    [Theory]
    [InlineData("class A { string s = \"SELECT N'a' + N'b' FROM step\"; }")]
    [InlineData("class A { string s = \"CREATE TABLE t\" + \"RIGGER (id int)\"; }")]
    [InlineData("class A { string a = \"CREATE\"; string b = \"TRIGGER\"; }")]
    [InlineData("class A { string s = \"EXEC(N'SELECT 1' + N' FROM step')\"; }")]
    public void Literals_joined_without_a_cut_keyword_are_not_taken_for_a_trigger(string sample) =>
        Assert.False(SourceDetectors.CreatesTriggerOrProcedureInCSharp(sample));

    [Theory]
    [InlineData("migrationBuilder.CreateTable(name: \"step\", columns: table => new { });")]
    [InlineData("class A { string s = \"Creates a trigger of the database\"; }")]
    public void Ordinary_csharp_is_not_taken_for_a_trigger(string sample) => Assert.False(SourceDetectors.CreatesTriggerOrProcedureInCSharp(sample));

    [Theory]
    [InlineData("EXEC sp_getapplock @Resource = 'migrate'")]
    [InlineData("migrationBuilder.Sql(\"CREATE DATABASE topogenia\");")]
    [InlineData("drop database Topogenia_Test_1")]
    [InlineData("ALTER DATABASE Topogenia SET ALLOW_SNAPSHOT_ISOLATION ON")]
    [InlineData("alter database [Topogenia_Dev] set read_committed_snapshot on with rollback immediate")]
    [InlineData("ALTER DATABASE [CURRENT] SET ALLOW_SNAPSHOT_ISOLATION ON")]
    [InlineData("ALTER DATABASE CURRENTDB SET RECOVERY SIMPLE")]
    [InlineData("ALTER DATABASE SCOPED CONFIGURATION SET MAXDOP = 1")]
    [InlineData("USE master; ALTER DATABASE CURRENT SET ALLOW_SNAPSHOT_ISOLATION ON")]
    [InlineData("ALTER DATABASE CURRENT SET TRUSTWORTHY ON")]
    [InlineData("ALTER DATABASE CURRENT SET DB_CHAINING ON;")]
    [InlineData("ALTER DATABASE CURRENT MODIFY NAME = Other;")]
    [InlineData("ALTER DATABASE CURRENT SET SINGLE_USER WITH ROLLBACK IMMEDIATE;")]
    [InlineData("ALTER DATABASE CURRENT ADD FILE (NAME = extra, FILENAME = 'D:\\extra.ndf');")]
    [InlineData("ALTER DATABASE CURRENT SET ENCRYPTION OFF;")]
    [InlineData("ALTER DATABASE CURRENT COLLATE Latin1_General_BIN2;")]
    [InlineData("ALTER DATABASE CURRENT SET ALLOW_SNAPSHOT_ISOLATION ON, TRUSTWORTHY ON;")]
    [InlineData("ALTER DATABASE CURRENT SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE, DB_CHAINING ON;")]
    [InlineData("ALTER DATABASE CURRENT SET ALLOW_SNAPSHOT_ISOLATION ON; ALTER DATABASE CURRENT SET TRUSTWORTHY ON;")]
    [InlineData("alter database current set recovery simple")]
    [InlineData("USE [master];")]
    [InlineData("SELECT name FROM master.sys.databases")]
    [InlineData("SELECT 1 FROM [master].[dbo].[spt_values]")]
    [InlineData("Server=.;Initial Catalog=master;Integrated Security=true")]
    [InlineData("Server=.;Database = master;Trusted_Connection=True")]
    [InlineData("ALTER DATABASE CURRENT SET ALLOW_SNAPSHOT_ISOLATION ON\n, TRUSTWORTHY ON;")]
    [InlineData("ALTER DATABASE CURRENT SET ALLOW_SNAPSHOT_ISOLATION ON /* x */, TRUSTWORTHY ON;")]
    [InlineData("ALTER DATABASE CURRENT SET ALLOW_SNAPSHOT_ISOLATION ON -- x\n, TRUSTWORTHY ON;")]
    [InlineData("ALTER/**/DATABASE CURRENT SET TRUSTWORTHY ON;")]
    public void The_system_database_is_detected(string sample) => Assert.True(SourceDetectors.UsesSystemDatabase(sample));

    /// <summary>The samples of the control of the architecture (gate P3, round 2): holes, concatenations, quotes, SQL comments.</summary>
    [Theory]
    [InlineData("ExecuteAsync(c, $\"ALTER DATABASE CURRENT SET ALLOW_SNAPSHOT_ISOLATION {\"ON, TRUSTWORTHY ON\"};\", t);")]
    [InlineData("Run(\"ALTER DATABASE CURRENT SET ALLOW_SNAPSHOT_ISOLATION ON\" + \", TRUSTWORTHY ON;\");")]
    [InlineData("migrationBuilder.Sql(\"ALTER DATABASE CURRENT SET READ_COMMITTED_SNAPSHOT ON\\\", DB_CHAINING ON\");")]
    [InlineData("ExecuteAsync(c, $\"ALTER DATABASE CURRENT SET ALLOW_SNAPSHOT_ISOLATION {Option()};\", t);")]
    [InlineData("ExecuteAsync(c, $\"ALTER DATABASE CURRENT SET ALLOW_SNAPSHOT_ISOLATION {(on ? \"ON\" : \"ON, TRUSTWORTHY ON\")};\", t);")]
    [InlineData("Run(\"ALTER DATABASE CURRENT SET ALLOW_SNAPSHOT_ISOLATION ON /**/\" + \", TRUSTWORTHY ON;\");")]
    [InlineData("Run(\"USE \" + \"master;\");")]
    [InlineData("// EXEC sp_getapplock")]
    public void The_system_database_is_detected_in_csharp(string sample) => Assert.True(SourceDetectors.UsesSystemDatabaseInCSharp(sample));

    [Theory]
    [InlineData("ExecuteAsync(c, $\"ALTER DATABASE CURRENT SET ALLOW_SNAPSHOT_ISOLATION {option};\", t);")]
    [InlineData("ExecuteAsync(connection, $\"ALTER DATABASE CURRENT SET ALLOW_SNAPSHOT_ISOLATION {(on ? \"ON\" : \"OFF\")};\", token);")]
    [InlineData("ExecuteAsync(c, $\"ALTER DATABASE CURRENT SET READ_COMMITTED_SNAPSHOT {(on ? \"ON\" : \"OFF\")} WITH ROLLBACK IMMEDIATE;\", t);")]
    [InlineData("migrationBuilder.Sql(\n    \"\"\"\n    IF (SELECT snapshot_isolation_state FROM sys.databases WHERE database_id = DB_ID()) = 0\n        ALTER DATABASE CURRENT SET ALLOW_SNAPSHOT_ISOLATION ON;\n    \"\"\",\n    suppressTransaction: true);")]
    [InlineData("// The master map of the project is its root map.")]
    public void Ordinary_csharp_is_not_taken_for_the_system_database(string sample) => Assert.False(SourceDetectors.UsesSystemDatabaseInCSharp(sample));

    [Theory]
    [InlineData("Server=.;Database=Topogenia_Dev;Trusted_Connection=True")]
    [InlineData("// The master map of the project is its root map.")]
    [InlineData("SELECT name FROM sys.tables")]
    [InlineData("var database = context.Database;")]
    [InlineData("ALTER DATABASE CURRENT SET ALLOW_SNAPSHOT_ISOLATION ON")]
    [InlineData("alter database current set read_committed_snapshot on with rollback immediate")]
    [InlineData("IF (SELECT snapshot_isolation_state FROM sys.databases WHERE database_id = DB_ID()) = 0\n    ALTER DATABASE CURRENT\n    SET ALLOW_SNAPSHOT_ISOLATION ON;")]
    [InlineData("ALTER DATABASE CURRENT SET READ_COMMITTED_SNAPSHOT OFF WITH ROLLBACK IMMEDIATE;")]
    [InlineData("ALTER DATABASE CURRENT SET ALLOW_SNAPSHOT_ISOLATION ON\nGO")]
    public void Ordinary_text_is_not_taken_for_the_system_database(string sample) => Assert.False(SourceDetectors.UsesSystemDatabase(sample));

    [Fact]
    public void Views_outside_the_bi_schema_are_detected()
    {
        Assert.True(SourceDetectors.CreatesViewOutsideBi("CREATE VIEW topogenia.step_summary AS SELECT 1"));
        Assert.True(SourceDetectors.CreatesViewOutsideBi("create temp view v as select 1"));
        Assert.False(SourceDetectors.CreatesViewOutsideBi("CREATE VIEW bi.step AS SELECT 1"));
        Assert.False(SourceDetectors.CreatesViewOutsideBi("CREATE OR ALTER VIEW [bi].[flow] AS SELECT 1"));
    }

    [Theory]
    [InlineData("// to" + "do: later")]
    [InlineData("// TO" + "DO_x")]
    [InlineData("/* FIX" + "ME */")]
    [InlineData("# ha" + "ck")]
    [InlineData("// TB" + "D later")]
    [InlineData("// WI" + "P")]
    [InlineData("// A FAI" + "RE : relire")]
    [InlineData("// À fai" + "re : relire")]
    [InlineData("throw new System.NotImplemented" + "Exception();")]
    public void Work_markers_are_detected(string sample) => Assert.True(SourceDetectors.ContainsWorkMarker(sample));

    [Theory]
    [InlineData("// a todolist is a list")]
    [InlineData("var hackathon = 1;")]
    [InlineData("// the wiper and the tbdx field")]
    public void Words_containing_a_marker_are_not_markers(string sample) => Assert.False(SourceDetectors.ContainsWorkMarker(sample));

    [Theory]
    [InlineData("""class A { string s = "Acme"; }""")]
    [InlineData("""class A { string s = "my acme tool"; }""")]
    [InlineData("class A { string s = \"\"\"\n  Acme\n  \"\"\"; }")]
    [InlineData("""class A { System.ReadOnlySpan<byte> s => "Acme"u8; }""")]
    [InlineData("class A { } // Acme")]
    [InlineData("/// <summary>Acme</summary>\nclass A { }")]
    [InlineData("#region Acme\nclass A { }\n#endregion")]
    [InlineData("class A { string s = $\"{1}Acme\"; }")]
    public void The_product_name_outside_identifiers_is_detected(string sample) =>
        Assert.True(SourceDetectors.WritesProductName(sample, Product));

    [Theory]
    [InlineData("namespace Acme.Domain; class A { }")]
    [InlineData("using Acme.Domain; class A { Acme.Domain.B b; }")]
    public void The_product_name_in_identifiers_is_allowed(string sample) =>
        Assert.False(SourceDetectors.WritesProductName(sample, Product));

    [Theory]
    [InlineData("class A { static string S { get; } = ProductIdentity.Name.ToLowerInvariant(); }")]
    [InlineData("[assembly: InternalsVisibleTo(Acme.Domain.ProductIdentity.Name + \".Tests\")]")]
    [InlineData("using static Acme.Domain.ProductIdentity; class A { string s = Name; }")]
    [InlineData("using P = Acme.Domain.ProductIdentity; class A { string s = P.Name; }")]
    public void Code_that_reads_the_product_name_is_detected(string sample) => Assert.True(SourceDetectors.ReadsProductName(sample));

    [Theory]
    [InlineData("/// <summary>Not <see cref=\"ProductIdentity\"/>.</summary>\nclass A { }")]
    [InlineData("class A { string s = TechnicalNames.Schema; } // not ProductIdentity")]
    public void Code_that_does_not_read_the_product_name_is_accepted(string sample) => Assert.False(SourceDetectors.ReadsProductName(sample));

    [Fact]
    public void Long_methods_lambdas_bodies_initialisers_and_top_level_blocks_are_detected()
    {
        var body = string.Concat(Enumerable.Repeat("        x++;\n", 85));
        var terms = string.Concat(Enumerable.Repeat("        + 1\n", 90));
        Assert.NotEmpty(SourceDetectors.LongMembers($"class A {{ void M() {{ var x = 0;\n{body} }} }}"));
        Assert.NotEmpty(SourceDetectors.LongMembers($"class A {{ System.Action f = () => {{ var x = 0;\n{body} }}; }}"));
        Assert.NotEmpty(SourceDetectors.LongMembers($"var x = 0;\n{body}"));
        Assert.NotEmpty(SourceDetectors.LongMembers($"class A {{ int P => 0\n{terms}; }}"));
        Assert.NotEmpty(SourceDetectors.LongMembers($"class A {{ int f = 0\n{terms}; }}"));
        Assert.Empty(SourceDetectors.LongMembers("class A { void M() { } }"));
    }

    [Fact]
    public void Long_typescript_test_cases_are_detected()
    {
        var body = string.Concat(Enumerable.Repeat("    expect(1).toBe(1);\n", 85));
        Assert.NotEmpty(SourceDetectors.LongTestCases($"describe('a', () => {{\n  it('b', () => {{\n{body}  }});\n}});\n"));
        Assert.Empty(SourceDetectors.LongTestCases("it('b', () => {\n  expect(1).toBe(1);\n});\n"));
    }

    [Theory]
    [InlineData("class A { System.DateTimeOffset d; }")]
    [InlineData("class A { object d = System.DateTime.Now; }")]
    [InlineData("class A { object d = DateTime.Today; }")]
    [InlineData("class A { object d = System.TimeProvider.System.GetUtcNow(); }")]
    [InlineData("class A { object d = System.TimeProvider.System.GetLocalNow(); }")]
    [InlineData("class A { object d = global::System.DateTime.Now; }")]
    public void Local_dates_are_detected(string sample) => Assert.True(SourceDetectors.UsesLocalDate(sample));

    [Fact]
    public void Utc_dates_are_allowed() => Assert.False(SourceDetectors.UsesLocalDate("class A { object d = System.DateTime.UtcNow; }"));

    [Theory]
    [InlineData("class A { object d = System.DateTime.UtcNow; }")]
    [InlineData("class A { object d = DateTime.UtcNow; }")]
    [InlineData("class A { object g = Guid.CreateVersion7(); }")]
    [InlineData("class A { System.Func<System.Guid> f = global::System.Guid.CreateVersion7; }")]
    [InlineData("class A { A(System.TimeProvider clock) { } }")]
    [InlineData("class A { object c = TimeProvider.System; }")]
    public void The_clock_and_the_identifier_factory_are_detected(string sample) =>
        Assert.True(SourceDetectors.ReadsClockOrCreatesIdentifier(sample));

    [Theory]
    [InlineData("class A { object d = context.Now; }")]
    [InlineData("class A { object g = context.NewId(); }")]
    [InlineData("class A { object d = step.UpdatedAt; }")]
    [InlineData("class A { } // the operation context reads DateTime.UtcNow")]
    public void Values_given_by_the_operation_context_are_not_the_clock(string sample) =>
        Assert.False(SourceDetectors.ReadsClockOrCreatesIdentifier(sample));

    [Theory]
    [InlineData("/// <summary>Vérifie que la case est libre.</summary>\nclass A { }")]
    [InlineData("/// <summary>Place la carte dans le couloir.</summary>\nclass A { }")]
    [InlineData("/** <summary>Une étape sur la carte.</summary> */\nclass A { }")]
    [InlineData("class A {\n    /// <param name=\"x\">la valeur est dans le couloir</param>\n    void M(int x) { }\n}")]
    public void French_xml_documentation_comments_are_detected(string sample) => Assert.True(SourceDetectors.HasFrenchComment(sample));

    [Theory]
    [InlineData("/// <summary>Checks that the cell is free (the 1.x `Deplacer`).</summary>\nclass A { }")]
    [InlineData("class A {\n    /// <param name=\"x\">The value of the lane.</param>\n    void M(int x) { }\n}")]
    public void English_xml_documentation_comments_are_accepted(string sample) => Assert.False(SourceDetectors.HasFrenchComment(sample));

    [Theory]
    [InlineData("fetch('https://cdn.example.com/x.js')")]
    [InlineData("<link href=\"http://fonts.example/css\">")]
    [InlineData("src: url(//fonts.gstatic.com/s/inter.woff2)")]
    [InlineData("new WebSocket('wss://telemetry.example.com/x')")]
    [InlineData("fetch('https://localhost.example.com/x')")]
    public void External_addresses_are_detected(string sample) => Assert.True(SourceDetectors.ContainsExternalUrl(sample));

    [Theory]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\">")]
    [InlineData("const api = 'http://127.0.0.1:5080';")]
    [InlineData("const api = 'http://localhost:5080/api';")]
    [InlineData("// a comment line")]
    public void Local_and_namespace_addresses_are_allowed(string sample) => Assert.False(SourceDetectors.ContainsExternalUrl(sample));

    [Theory]
    [InlineData(0x1F600)]
    [InlineData(0x2705)]
    [InlineData(0x231A)]
    [InlineData(0x23F0)]
    [InlineData(0x2B50)]
    [InlineData(0x2B55)]
    [InlineData(0x270B)]
    [InlineData(0x25B6)]
    [InlineData(0x2139)]
    [InlineData(0x25FD)]
    public void Emoji_are_detected(int codePoint) => Assert.True(SourceDetectors.ContainsEmoji(char.ConvertFromUtf32(codePoint)));

    [Theory]
    [InlineData("× + ○ › →")]
    [InlineData("é à ç")]
    public void Typographic_symbols_are_not_emoji(string sample) => Assert.False(SourceDetectors.ContainsEmoji(sample));
}
