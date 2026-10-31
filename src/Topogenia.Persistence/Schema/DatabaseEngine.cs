// DatabaseEngine.cs
// One database engine (SQL Server or SQLite): how to open the common context on it, its catalogue and its migrations.

using System.Reflection;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Topogenia.Persistence.Schema;

/// <summary>
/// A database reached through one provider. The provider builds the options of the common <see cref="AppDbContext"/>
/// (its EF Core provider, its model customizer with the schema, filters and CHECK constraints, its migrations assembly)
/// and gives its catalogue (<see cref="SchemaCatalog"/>: history, description, rights and refusals of the start). What
/// else a provider needs lives in the provider: the connection settings of SQLite in its options and its connection
/// interceptor, the translation of the errors of the unit of work in its dialect (<c>EngineDialect</c>).
/// </summary>
public abstract class DatabaseEngine
{
    /// <summary>
    /// The delay of the migrations when the host gives none: 10 minutes (ADR 041). It bounds each statement of a migration
    /// and of the check under the lock, and the wait for the lock of the migrations while another process migrates.
    /// </summary>
    public static TimeSpan DefaultMigrationTimeout { get; } = TimeSpan.FromMinutes(10);

    /// <summary>The longest delay of the migrations accepted: one day, so that a hung migration elsewhere never blocks for good.</summary>
    public static TimeSpan MaximumMigrationTimeout { get; } = TimeSpan.FromDays(1);

    private readonly TimeSpan migrationTimeout = DefaultMigrationTimeout;

    /// <summary>The assembly that holds the migrations and their embedded sources: the one of the provider.</summary>
    public Assembly MigrationsAssembly => GetType().Assembly;

    public abstract SchemaCatalog Catalog { get; }

    /// <summary>
    /// The delay of the contexts of the migrations (<see cref="CreateMigrationContext"/>), independent of the ordinary
    /// command timeout of the application, which every other context keeps (ADR 029 for SQLite, ADR 041): from 1 second
    /// to <see cref="MaximumMigrationTimeout"/>, counted in whole seconds (rounded up), <see cref="DefaultMigrationTimeout"/>
    /// when not given. A setting of the engine that the host passes (`--migrate`, P4-T09).
    /// </summary>
    public TimeSpan MigrationTimeout
    {
        get => migrationTimeout;
        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, TimeSpan.FromSeconds(1));
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, MaximumMigrationTimeout);
            migrationTimeout = value;
        }
    }

    /// <summary>The delay of the migrations in whole seconds, as the providers count a command timeout.</summary>
    public int MigrationTimeoutSeconds => (int)Math.Ceiling(MigrationTimeout.TotalSeconds);

    /// <summary>A new context on this database, with the given interceptors added to those of the provider.</summary>
    public AppDbContext CreateContext(params IInterceptor[] interceptors) => new(Options(interceptors, null));

    /// <summary>
    /// A new context for the check and the migration of the schema: every command it sends, those of the lock of the
    /// migrations and of the catalogue included, has the delay <see cref="MigrationTimeout"/> instead of the ordinary one.
    /// </summary>
    public AppDbContext CreateMigrationContext(params IInterceptor[] interceptors) => new(Options(interceptors, MigrationTimeoutSeconds));

    /// <summary>
    /// The options of a context; <paramref name="commandTimeoutSeconds"/>, when given, replaces the ordinary command
    /// timeout of the provider for every command of the context and of its connection.
    /// </summary>
    protected abstract Microsoft.EntityFrameworkCore.DbContextOptions<AppDbContext> Options(IReadOnlyList<IInterceptor> interceptors, int? commandTimeoutSeconds);
}
