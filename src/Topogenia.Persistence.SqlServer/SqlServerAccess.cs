// SqlServerAccess.cs
// Names of the database roles and of the report schema of the server mode, derived from the fixed technical schema (spec 4.4, 5.3, ADR 013).

namespace Topogenia.Persistence.SqlServer;

/// <summary>
/// The access layer of a SQL Server database (1.x `db/004`): the role of the account of the application, the role of the
/// reports and the schema of the report views. The two role names derive from the schema of the tables, a fixed
/// technical name, not the product name (ADR 013); the migration that creates them writes them as they are (ADR 032).
/// </summary>
public static class SqlServerAccess
{
    /// <summary>The schema of the report views (`bi`, as in the 1.x), read by the report role only.</summary>
    public const string ReportSchema = "bi";

    /// <summary>
    /// The role of the account of the application: reads and writes the data of the tables, only reads the two histories of
    /// the migrations, only appends to the histories of the data (spec 5.3).
    /// </summary>
    public static string ApplicationRole { get; } = SqlServerEngine.Schema + "_app";

    /// <summary>The role of the reports (Power BI): reads the report views and nothing else (spec 5.3).</summary>
    public static string ReportRole { get; } = SqlServerEngine.Schema + "_bi";
}
