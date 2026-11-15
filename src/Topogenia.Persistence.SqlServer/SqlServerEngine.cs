// SqlServerEngine.cs
// The SQL Server engine of the server mode: options of the common context, fixed technical schema of the tables (spec 4.1, ADR 013).

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Topogenia.Persistence.Model;
using Topogenia.Persistence.Schema;

namespace Topogenia.Persistence.SqlServer;

/// <summary>
/// A SQL Server database. The tables live in the fixed technical schema (<see cref="TechnicalNames.Schema"/>, ADR 013),
/// the history tables of the migrations too; the migrations of this assembly create it and freeze its name. An ordinary context keeps
/// the command timeout of the connection string (30 s by default). A context of the migrations has the delay of the
/// migrations in its options and in its connection string, so that the commands that EF Core sends (statements of the
/// migrations, the application lock of the migrations, which waits as long as its command may) and those that the
/// catalogue creates on the connection have it alike (ADR 041).
/// </summary>
public sealed class SqlServerEngine : DatabaseEngine
{
    private readonly string connectionString;

    public SqlServerEngine(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        this.connectionString = connectionString;
    }

    /// <summary>The schema of the tables: a fixed technical name that the migrations froze, not the product name (spec 4.1, ADR 013).</summary>
    public static string Schema { get; } = TechnicalNames.Schema;

    public override SchemaCatalog Catalog { get; } = new SqlServerSchemaCatalog();

    protected override DbContextOptions<AppDbContext> Options(IReadOnlyList<IInterceptor> interceptors, int? commandTimeoutSeconds) =>
        new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(ConnectionString(commandTimeoutSeconds), sql =>
            {
                sql.MigrationsAssembly(typeof(SqlServerEngine).Assembly.GetName().Name)
                    .MigrationsHistoryTable(TableNames.MigrationsHistory, Schema);
                if (commandTimeoutSeconds is { } seconds)
                    sql.CommandTimeout(seconds);
            })
            .ReplaceService<IModelCustomizer, SqlServerModelCustomizer>()
            .UseTextSearchParity()
            .AddInterceptors(interceptors)
            .Options;

    /// <summary>The connection string as given, or with the given command timeout (the default of the commands of its connections).</summary>
    private string ConnectionString(int? commandTimeoutSeconds) => commandTimeoutSeconds is { } seconds
        ? new SqlConnectionStringBuilder(connectionString) { CommandTimeout = seconds }.ConnectionString
        : connectionString;
}
