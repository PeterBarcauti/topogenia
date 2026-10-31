// SqlServerSchemaCatalog.cs
// History of the schema and description of the catalogue on SQL Server: every object, right and option of the database that the application relies on (spec 4.4, ADR 025, 031, 032, 036).

using System.Data;
using System.Data.Common;
using System.Globalization;
using Microsoft.Data.SqlClient;
using Topogenia.Application.Ports;
using Topogenia.Persistence.Model;
using Topogenia.Persistence.Schema;

namespace Topogenia.Persistence.SqlServer;

/// <summary>
/// Reads and writes `schema_history` and describes the schema of the product and the `bi` schema from the system views
/// (`sys.tables`, `sys.columns`, `sys.indexes`, `sys.foreign_keys`, `sys.check_constraints`, `sys.default_constraints`,
/// `sys.views`), one line per object, the history table of EF Core aside (that table only, in the schema of the
/// product, by its exact name); every other object of the database that Microsoft does not ship, whatever its schema
/// (functions, synonyms, triggers, procedures, sequences, types, tables or views outside the two schemas; an object
/// marked as shipped outside the schemas of the system too), the triggers of the database, and an owner set on an
/// object: none exists in a database that the migrations made (D04), so any of them is a change. And the access layer of
/// the server mode (ADR 032): every schema of the database other than those of the system and its owner, the two roles,
/// every permission of the database (the right to connect of each user and the grants of the objects of the system
/// aside), the roles that the two roles belong to, and the number of members of the fixed roles that open the data
/// (<see cref="DataRoles"/>), every member of the two roles that is not a user (a database role, an application role) and
/// every application role. The users that an administrator adds to the two roles (the accounts of the application and of
/// the reports) and the members of `db_owner` (the administrators) are not described: they belong to each installation. The
/// options of the database that the reads need (snapshot isolation, read committed snapshot) are described too: a database
/// where they were removed by hand is refused at the start rather than failing at each read (error 3952); and the options
/// that let code or rights of the database reach outside it (trust, chaining of ownership across databases), when set
/// (ADR 037).
/// </summary>
internal sealed class SqlServerSchemaCatalog : SchemaCatalog
{
    /// <summary>The migration that sets the options of the database for the snapshot reads, outside any transaction (ADR 031).</summary>
    internal const string OptionsMigration = "20261023092342_SnapshotIsolation";

    /// <summary>
    /// The migration, in a transaction, that checks the options and records <see cref="OptionsMigration"/>. The options are
    /// part of the description from this migration on: before it, a database keeps the description recorded by the
    /// migration before the options, whatever they are; so a run stopped after setting them, or after EF Core recorded
    /// <see cref="OptionsMigration"/> outside any transaction, leaves the database behind, not refused, and the next run
    /// records everything (<see cref="SchemaVerification"/>).
    /// </summary>
    internal const string OptionsCheckMigration = "20261023093315_SnapshotIsolationCheck";

    private static readonly string History = $"[{SqlServerEngine.Schema}].[{TableNames.SchemaHistory}]";

    private static readonly string Migrations = $"[{SqlServerEngine.Schema}].[{TableNames.MigrationsHistory}]";

    /// <summary>The tables that an account of the application must be able to read: the two histories and the data (the projects).</summary>
    private static readonly string[] ReadProbes = [Migrations, History, $"[{SqlServerEngine.Schema}].[{TableNames.Project}]"];

    /// <summary>Invalid object name: the table does not exist yet.</summary>
    private const int InvalidObject = 208;

    /// <summary>
    /// The database cannot be opened (4060): absent, or the login has no user in it; SQL Server adds 18456 to it. The
    /// 1.x read 4060, the first number, as a database not available (A-5-10).
    /// </summary>
    private const int CannotOpenDatabase = 4060;

    /// <summary>
    /// The login is refused by the instance (18456 without 4060): the account, not the database, is at fault. The 1.x read
    /// it as a refused login (« SQL Server refuses this account »); 2.0 reads it as missing rights (ADR 036, round 3).
    /// </summary>
    private const int LoginFailed = 18456;

    /// <summary>Permission refused on an object (229) or in the database (262). The 1.x read 262 only; 229 is read since ADR 036.</summary>
    private static readonly int[] Refused = [229, 262];

    /// <summary>
    /// The creation of the database failed (1802): SQL Server adds it to the cause of the failure (a lock, the disk, a
    /// path). It stands for missing rights only when it comes alone. The 1.x read it, with 262, as missing rights by the
    /// first number of the failure only (`SqlException.Number`); 2.0 reads every number of the failure (ADR 036).
    /// </summary>
    private const int CreationFailed = 1802;

    /// <summary>
    /// The errors that a contention or a broken connection gives, gone when the statement runs again: the timeout of the
    /// client (-2), the connection broken (64, 233, 10053, 10054, 10060), no lock resource (1204), victim of a deadlock
    /// (1205), timeout of a lock (1222), exclusive lock on `model` not obtained by the creation of a database (1807), lock
    /// on the database not obtained by a change of its options (5061), and the database or the service busy in Azure SQL
    /// (40197, 40501, 40613, 49918, 49919, 49920).
    /// </summary>
    private static readonly int[] Transient = [-2, 64, 233, 1204, 1205, 1222, 1807, 5061, 10053, 10054, 10060, 40197, 40501, 40613, 49918, 49919, 49920];

    /// <summary>
    /// The fixed roles whose members read or write every table, create objects that read them through the ownership chain,
    /// give rights or copy the database. `db_owner` (the administrators), `db_accessadmin` (it adds users, who hold only what
    /// `public` holds, which is described) and the two deny roles (they only remove rights) are not among them.
    /// </summary>
    internal static IReadOnlyList<string> DataRoles { get; } = ["db_backupoperator", "db_datareader", "db_datawriter", "db_ddladmin", "db_securityadmin"];

    /// <summary>
    /// The objects of the two schemas of the application, the history table of EF Core aside: that table only, in the
    /// schema of the product, by its exact name (a view or a table of the same name, in any case, is described). @schema,
    /// @report, @excluded, @app, @reports, @migrations, @options and @data0 to @data4 (<see cref="DataRoles"/>) are bound
    /// by <see cref="DescribeAsync"/>.
    /// </summary>
    private const string Scope = "s.name IN (@schema, @report) AND NOT (t.type = 'U' AND s.name = @schema COLLATE Latin1_General_BIN2 " +
        "AND t.name = @excluded COLLATE Latin1_General_BIN2)";

    /// <summary>
    /// The queries of the tables and views of the two schemas and of what belongs to them (columns, indexes, keys,
    /// constraints, defaults, definitions of the views).
    /// </summary>
    private static readonly string[] SchemaQueries =
    [
        $"SELECT 'T', s.name + '.' + t.name FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id WHERE {Scope}",
        "SELECT 'C', s.name + '.' + t.name, c.name, ty.name, c.max_length, c.precision, c.scale, c.is_nullable, c.collation_name, " +
        "c.is_identity, c.is_computed FROM sys.columns c JOIN sys.tables t ON t.object_id = c.object_id " +
        $"JOIN sys.schemas s ON s.schema_id = t.schema_id JOIN sys.types ty ON ty.user_type_id = c.user_type_id WHERE {Scope}",
        "SELECT 'I', s.name + '.' + t.name, i.name, i.type_desc, i.is_unique, i.is_primary_key, i.is_unique_constraint, i.filter_definition " +
        $"FROM sys.indexes i JOIN sys.tables t ON t.object_id = i.object_id JOIN sys.schemas s ON s.schema_id = t.schema_id WHERE i.type > 0 AND {Scope}",
        "SELECT 'IC', s.name + '.' + t.name, i.name, ic.key_ordinal, c.name, ic.is_descending_key, ic.is_included_column " +
        "FROM sys.index_columns ic JOIN sys.indexes i ON i.object_id = ic.object_id AND i.index_id = ic.index_id " +
        "JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id JOIN sys.tables t ON t.object_id = i.object_id " +
        $"JOIN sys.schemas s ON s.schema_id = t.schema_id WHERE {Scope}",
        "SELECT 'F', s.name + '.' + t.name, fk.name, rs.name + '.' + rt.name, fk.delete_referential_action_desc, " +
        "fk.update_referential_action_desc, fk.is_disabled, fk.is_not_trusted FROM sys.foreign_keys fk " +
        "JOIN sys.tables t ON t.object_id = fk.parent_object_id JOIN sys.schemas s ON s.schema_id = t.schema_id " +
        $"JOIN sys.tables rt ON rt.object_id = fk.referenced_object_id JOIN sys.schemas rs ON rs.schema_id = rt.schema_id WHERE {Scope}",
        "SELECT 'FC', s.name + '.' + t.name, fk.name, fkc.constraint_column_id, pc.name, rc.name " +
        "FROM sys.foreign_key_columns fkc JOIN sys.foreign_keys fk ON fk.object_id = fkc.constraint_object_id " +
        "JOIN sys.columns pc ON pc.object_id = fkc.parent_object_id AND pc.column_id = fkc.parent_column_id " +
        "JOIN sys.columns rc ON rc.object_id = fkc.referenced_object_id AND rc.column_id = fkc.referenced_column_id " +
        $"JOIN sys.tables t ON t.object_id = fk.parent_object_id JOIN sys.schemas s ON s.schema_id = t.schema_id WHERE {Scope}",
        "SELECT 'K', s.name + '.' + t.name, cc.name, cc.definition, cc.is_disabled, cc.is_not_trusted " +
        "FROM sys.check_constraints cc JOIN sys.tables t ON t.object_id = cc.parent_object_id " +
        $"JOIN sys.schemas s ON s.schema_id = t.schema_id WHERE {Scope}",
        "SELECT 'D', s.name + '.' + t.name, c.name, dc.definition FROM sys.default_constraints dc " +
        "JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id " +
        $"JOIN sys.tables t ON t.object_id = dc.parent_object_id JOIN sys.schemas s ON s.schema_id = t.schema_id WHERE {Scope}",
        "SELECT 'V', s.name + '.' + t.name, m.definition FROM sys.views t JOIN sys.schemas s ON s.schema_id = t.schema_id " +
        $"JOIN sys.sql_modules m ON m.object_id = t.object_id WHERE {Scope}",
    ];

    /// <summary>
    /// The queries of the rest of the database: schemas, roles, objects outside the description above, owners, triggers
    /// of the database, types, permissions, members, options.
    /// </summary>
    private static readonly string[] DatabaseQueries =
    [
        // S|schema|owner: every schema but those of the system (dbo, guest, INFORMATION_SCHEMA, sys and those of the fixed
        // roles), so that a new schema is seen with its owner; an owner that the account cannot see reads empty.
        "SELECT 'S', s.name, o.name FROM sys.schemas s LEFT JOIN sys.database_principals o ON o.principal_id = s.principal_id " +
        "WHERE s.schema_id > 4 AND s.schema_id < 16384",
        "SELECT 'R', r.name, o.name FROM sys.database_principals r LEFT JOIN sys.database_principals o ON o.principal_id = r.owning_principal_id " +
        "WHERE r.type = 'R' AND r.name IN (@app, @reports)",
        // X|schema.object|type: every object that Microsoft does not ship, except the tables and the views of the two
        // schemas and the constraints of a table, described above (the history of EF Core and its constraints aside). An
        // object marked as shipped (`sp_MS_marksystemobject`) counts outside the schemas of the system: there, a database
        // holds only the three queues of the service broker of `dbo`.
        "SELECT 'X', s.name + '.' + o.name, RTRIM(o.type) FROM sys.objects o JOIN sys.schemas s ON s.schema_id = o.schema_id " +
        "WHERE (o.is_ms_shipped = 0 OR (s.name NOT IN ('sys', 'INFORMATION_SCHEMA') AND o.type <> 'SQ')) " +
        "AND NOT (s.name IN (@schema, @report) AND o.type IN ('U', 'V')) " +
        "AND NOT (o.parent_object_id <> 0 AND o.type IN ('PK', 'UQ', 'F', 'C', 'D'))",
        // W|schema.object|owner: an owner set on an object (`ALTER AUTHORIZATION`); the migrations set none, each object
        // belongs to the owner of its schema. The number of the owner, which the account of the application reads as an
        // administrator does, not its name.
        "SELECT 'W', s.name + '.' + o.name, o.principal_id FROM sys.objects o JOIN sys.schemas s ON s.schema_id = o.schema_id " +
        "WHERE o.principal_id IS NOT NULL",
        "SELECT 'TR', t.name, t.type FROM sys.triggers t WHERE t.parent_class = 0 AND t.is_ms_shipped = 0",
        "SELECT 'Y', s.name + '.' + t.name FROM sys.types t JOIN sys.schemas s ON s.schema_id = t.schema_id WHERE t.is_user_defined = 1",
        // P|class|schema of the object|object|column|schema|principal|other securable|permission|state|grantee: every
        // permission, except the right to connect of a user (guest aside) and the grants on the objects of the system
        // (shipped, in the schemas of the system).
        "SELECT 'P', p.class_desc, os.name, o.name, c.name, ss.name, dp.name, CASE WHEN p.class NOT IN (0, 1, 3, 4) THEN p.major_id END, " +
        "p.permission_name, p.state_desc, g.name " +
        "FROM sys.database_permissions p JOIN sys.database_principals g ON g.principal_id = p.grantee_principal_id " +
        "LEFT JOIN sys.all_objects o ON p.class = 1 AND o.object_id = p.major_id LEFT JOIN sys.schemas os ON os.schema_id = o.schema_id " +
        "LEFT JOIN sys.columns c ON p.class = 1 AND p.minor_id > 0 AND c.object_id = p.major_id AND c.column_id = p.minor_id " +
        "LEFT JOIN sys.schemas ss ON p.class = 3 AND ss.schema_id = p.major_id " +
        "LEFT JOIN sys.database_principals dp ON p.class = 4 AND dp.principal_id = p.major_id " +
        "WHERE NOT (p.class = 0 AND p.permission_name = 'CONNECT' AND g.name <> 'guest') " +
        "AND (p.class <> 1 OR o.is_ms_shipped = 0 OR os.name NOT IN ('sys', 'INFORMATION_SCHEMA'))",
        "SELECT 'M', r.name, m.name FROM sys.database_role_members rm JOIN sys.database_principals r ON r.principal_id = rm.role_principal_id " +
        "JOIN sys.database_principals m ON m.principal_id = rm.member_principal_id WHERE m.name IN (@app, @reports)",
        // MN|role|member|type: a member of one of the two roles that is not a user. A database role passes the rights of the
        // role to its own members, an application role to any session that gives its password (`sp_setapprole`, a member of
        // the reports included): neither is an account of an installation (ADR 032, point 2, revised).
        "SELECT 'MN', r.name, m.name, m.type FROM sys.database_role_members rm JOIN sys.database_principals r ON r.principal_id = rm.role_principal_id " +
        "JOIN sys.database_principals m ON m.principal_id = rm.member_principal_id WHERE r.name IN (@app, @reports) AND m.type IN ('R', 'A')",
        // AR|role: every application role of the database, whatever it is member of; the migrations create none.
        "SELECT 'AR', p.name FROM sys.database_principals p WHERE p.type = 'A'",
        // MF|fixed role|members: counted, not named, so that the account of the application, which cannot see the users,
        // reads the same line as an administrator.
        "SELECT 'MF', r.name, COUNT(*) FROM sys.database_role_members rm JOIN sys.database_principals r ON r.principal_id = rm.role_principal_id " +
        "WHERE r.is_fixed_role = 1 AND r.name IN (@data0, @data1, @data2, @data3, @data4) GROUP BY r.name",
        // O|snapshot isolation|read committed snapshot: the options of the current database (always visible to its users),
        // described once the migration that checks them is applied (ADR 031); a database without the history of EF Core has none.
        "IF OBJECT_ID(@migrations, N'U') IS NOT NULL SELECT 'O', d.snapshot_isolation_state_desc, d.is_read_committed_snapshot_on " +
        $"FROM sys.databases d WHERE d.database_id = DB_ID() AND EXISTS (SELECT 1 FROM {Migrations} h WHERE h.MigrationId = @options)",
        // B|option: an option of the current database that lets its code or its rights reach outside it, set by no
        // migration; a line only when it is on, so that a database that never had it keeps its description.
        "SELECT 'B', 'TRUSTWORTHY' FROM sys.databases d WHERE d.database_id = DB_ID() AND d.is_trustworthy_on = 1 " +
        "UNION ALL SELECT 'B', 'DB_CHAINING' FROM sys.databases d WHERE d.database_id = DB_ID() AND d.is_db_chaining_on = 1",
    ];

    /// <summary>
    /// One query per kind of object; each row is one line of the description, its columns joined by `|` (in C#: the
    /// names of the catalogue and the collation of the database differ, so the server cannot concatenate them).
    /// </summary>
    private static readonly string[] DescribeQueries = [.. SchemaQueries, .. DatabaseQueries];

    public override async Task<IReadOnlyList<SchemaHistoryEntry>> HistoryAsync(DbConnection connection, DbTransaction? transaction, CancellationToken cancellationToken)
    {
        await using var exists = Command(connection, transaction, "SELECT OBJECT_ID(@table, N'U')", ("@table", History));
        if (await exists.ExecuteScalarAsync(cancellationToken) is null or DBNull)
            return [];
        await using var select = Command(connection, transaction,
            $"SELECT migration_id, migration_fingerprint, schema_fingerprint, application_version, applied_at FROM {History}");
        await using var reader = await select.ExecuteReaderAsync(cancellationToken);
        var entries = new List<SchemaHistoryEntry>();
        while (await reader.ReadAsync(cancellationToken))
        {
            entries.Add(new SchemaHistoryEntry(
                reader.GetString(0),
                reader.GetString(1),
                await reader.IsDBNullAsync(2, cancellationToken) ? null : reader.GetString(2),
                reader.GetString(3),
                DateTime.SpecifyKind(reader.GetDateTime(4), DateTimeKind.Utc)));
        }
        return entries;
    }

    public override async Task RecordAsync(DbConnection connection, DbTransaction transaction, SchemaHistoryEntry entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);
        await using var insert = Command(connection, transaction,
            $"INSERT INTO {History} (migration_id, migration_fingerprint, schema_fingerprint, application_version, applied_at) " +
            "VALUES (@id, @migration, @schema, @version, @at)",
            ("@id", entry.MigrationId), ("@migration", entry.MigrationFingerprint), ("@schema", entry.SchemaFingerprint),
            ("@version", entry.ApplicationVersion), ("@at", UtcDates.ToStore(entry.AppliedAt)));
        await insert.ExecuteNonQueryAsync(cancellationToken);
    }

    public override async Task<IReadOnlyList<string>> DescribeAsync(DbConnection connection, DbTransaction? transaction, CancellationToken cancellationToken)
    {
        var lines = new List<string>();
        foreach (var query in DescribeQueries)
        {
            await using var command = Command(connection, transaction, query,
                ("@schema", SqlServerEngine.Schema), ("@report", SqlServerAccess.ReportSchema), ("@excluded", TableNames.MigrationsHistory),
                ("@app", SqlServerAccess.ApplicationRole), ("@reports", SqlServerAccess.ReportRole),
                ("@migrations", Migrations), ("@options", OptionsCheckMigration), ("@data0", DataRoles[0]), ("@data1", DataRoles[1]),
                ("@data2", DataRoles[2]), ("@data3", DataRoles[3]), ("@data4", DataRoles[4]));
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                lines.Add(string.Join('|', Enumerable.Range(0, reader.FieldCount).Select(i => Text(reader.GetValue(i)))));
        }
        return lines;
    }

    /// <summary>
    /// Reads no row of each table of <see cref="ReadProbes"/>: SQL Server refuses a table that the account cannot read
    /// (229) even when it hides its metadata, which would otherwise make the histories look empty. A missing table is no
    /// refusal: the database is new or behind.
    /// </summary>
    public override async Task CheckAccessAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        foreach (var table in ReadProbes)
        {
            await using var command = Command(connection, null, $"SELECT TOP (0) 1 FROM {table};");
            try
            {
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (SqlException failure) when (failure.Number == InvalidObject)
            {
                // Not created yet.
            }
        }
    }

    /// <summary>
    /// The numbers of every error of the first SQL Server failure of the chain: a contention first (the failure is
    /// transient, whatever comes with it), then a database that cannot be opened (4060, with or without 18456), then
    /// refused rights, a login refused by the instance (18456 alone) included; any other failure is left as it is.
    /// </summary>
    public override string? RefusalOf(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is not SqlException failure)
                continue;
            var numbers = failure.Errors.Cast<SqlError>().Select(error => error.Number).Append(failure.Number).ToHashSet();
            if (numbers.Overlaps(Transient))
                return SchemaCodes.DatabaseBusy;
            if (numbers.Contains(CannotOpenDatabase))
                return SchemaCodes.DatabaseUnreachable;
            return numbers.Overlaps(Refused) || numbers.Contains(LoginFailed) || numbers.SetEquals([CreationFailed]) ? SchemaCodes.InsufficientRights : null;
        }
        return null;
    }

    /// <summary>The numbers of every error of the first SQL Server failure of the chain, in the order of the server.</summary>
    public override IReadOnlyList<int> NumbersOf(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqlException failure)
                return [.. failure.Errors.Cast<SqlError>().Select(error => error.Number)];
        }
        return [];
    }

    /// <summary>
    /// EF Core says a database does not exist when its opening fails with 4060, which is also the answer to a login
    /// without user in an existing database: without reading `master` (A-5-12), nothing tells the two apart, so the check
    /// of the server mode refuses both as unreachable, as the 1.x said « not available » (ADR 036).
    /// </summary>
    public override string? AbsentDatabaseRefusal => SchemaCodes.DatabaseUnreachable;

    private static string Text(object value) => value is DBNull ? "" : Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";

    private static DbCommand Command(DbConnection connection, DbTransaction? transaction, string text, params (string Name, object? Value)[] parameters)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = text;
        foreach (var (name, value) in parameters)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            // A date parameter is `datetime` by default in SQL Server (3 ms): `datetime2` keeps every tick (D08).
            if (value is DateTime)
                parameter.DbType = DbType.DateTime2;
            command.Parameters.Add(parameter);
        }
        return command;
    }
}
