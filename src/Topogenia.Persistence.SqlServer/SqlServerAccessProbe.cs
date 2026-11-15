// SqlServerAccessProbe.cs
// Proves the rights of the database roles from inside a database: users without login, impersonation, effective permissions, changes made by hand.

using System.Data;
using System.Data.Common;
using System.Runtime.CompilerServices;
using Topogenia.Persistence.Model;

[assembly: InternalsVisibleTo(Topogenia.Persistence.Schema.TechnicalNames.PersistenceTests)]

namespace Topogenia.Persistence.SqlServer;

/// <summary>
/// What the tests of the roles need from SQL Server (ADR 032), kept here because no SQL text lives outside the providers
/// (spec 3.2): a user without login (never a login of the server) made member of a role, impersonated on an open
/// connection so that every later command of that connection, those of EF Core included, runs with the rights of the
/// role; the effective permissions of the impersonated user; the rows of a report view; and the changes of rights that an
/// administrator could make by hand, objects of <see cref="CatalogChange"/> included. Visible to the persistence tests
/// only; no type of the application depends on it (ADR 037). Every name is quoted by the server (`QUOTENAME`) or is a
/// constant of the provider, every permission is one of <see cref="DataPermissions"/>.
/// </summary>
internal static class SqlServerAccessProbe
{
    /// <summary>
    /// An object or a right that an administrator could add by hand to a migrated database, each one a way to read the data
    /// outside the two roles or to run code in the database (ADR 032, 037): a function and a synonym of the report schema over
    /// the users, a trigger on a table of the data or on the history of EF Core, a procedure, a sequence, a type, a trigger of
    /// the database, the right to read every table given to `public`, a synonym outside the two schemas given to the report
    /// role; a view and a table of the report schema named as the history of EF Core (in another case, in its exact case),
    /// the users or a report view given to the report role as their owner, a schema owned by the report role, and a view of
    /// `dbo` over the users (a function, as no view lives outside `bi`), marked as shipped by Microsoft, alone or given to
    /// the report role; an application role (<see cref="ApplicationRole"/>) alone, or member of one of the two roles, and a
    /// database role member of one of the two roles.
    /// </summary>
    public enum CatalogChange
    {
        ReportFunction,
        ReportSynonym,
        TableTrigger,
        HistoryTrigger,
        Procedure,
        Sequence,
        UserType,
        DatabaseTrigger,
        PublicGrant,
        SynonymOutsideTheSchemas,
        ReportViewNamedAsTheHistory,
        ReportTableNamedAsTheHistory,
        UsersOwnedByTheReports,
        ReportViewOwnedByTheReports,
        SchemaOwnedByTheReports,
        ShippedFunctionOutsideTheSchemas,
        ShippedFunctionGivenToTheReports,
        ApplicationRoleAlone,
        ApplicationRoleInTheApplicationRole,
        ApplicationRoleInTheReportRole,
        DatabaseRoleInTheApplicationRole,
        DatabaseRoleInTheReportRole,
    }

    /// <summary>The columns of a report view, in their order, and its rows (a database null is a null).</summary>
    public sealed record ReportViewRows(IReadOnlyList<string> Columns, IReadOnlyList<object?[]> Rows);

    /// <summary>The permissions on data that the roles grant or deny.</summary>
    public static IReadOnlyList<string> DataPermissions { get; } = ["SELECT", "INSERT", "UPDATE", "DELETE"];

    /// <summary>The name of the history of EF Core in lower case, which a database of the default collation takes for it.</summary>
    public static string HistoryInLowerCase { get; } = TableNames.MigrationsHistory.ToLowerInvariant();

    /// <summary>The application role that <see cref="CatalogChange"/> creates: any session that gives its password takes its rights.</summary>
    public const string ApplicationRole = "probe_application";

    /// <summary>
    /// The password of <see cref="ApplicationRole"/>, which lives in a test database only: drawn for each process, so that no
    /// password is written in the shipped code (spec 5.1); the role is created and used by the same run.
    /// </summary>
    private static readonly string ApplicationRolePassword = "Pr0be-" + Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16));

    /// <summary>Creates a user without login and makes it a member of the role.</summary>
    public static async Task CreateMemberAsync(DbConnection connection, string user, string role, CancellationToken cancellationToken)
    {
        await CreateUserAsync(connection, user, cancellationToken);
        await AddMemberAsync(connection, role, user, cancellationToken);
    }

    /// <summary>Creates a user without login, member of no role: it holds only what `public` holds.</summary>
    public static Task CreateUserAsync(DbConnection connection, string user, CancellationToken cancellationToken) =>
        ExecuteAsync(connection, cancellationToken,
            "DECLARE @text nvarchar(max) = N'CREATE USER ' + QUOTENAME(@user) + N' WITHOUT LOGIN;'; EXEC sys.sp_executesql @text;",
            ("@user", user));

    /// <summary>Makes a principal of the database (a user or a role) a member of a role.</summary>
    public static Task AddMemberAsync(DbConnection connection, string role, string member, CancellationToken cancellationToken) =>
        ExecuteAsync(connection, cancellationToken,
            "DECLARE @text nvarchar(max) = N'ALTER ROLE ' + QUOTENAME(@role) + N' ADD MEMBER ' + QUOTENAME(@member) + N';'; " +
            "EXEC sys.sp_executesql @text;",
            ("@role", role), ("@member", member));

    /// <summary>
    /// Runs the next commands of the connection as the user, until <see cref="RevertAsync"/>. The statement is sent without
    /// parameter: a parameterised command runs in its own batch, at the end of which the server would revert already. The
    /// name is therefore limited to letters, digits and underscores.
    /// </summary>
    public static Task ImpersonateAsync(DbConnection connection, string user, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(user);
        if (!user.All(character => char.IsAsciiLetterOrDigit(character) || character == '_'))
            throw new ArgumentException($"{user} is not a name of a test user.", nameof(user));
        return ExecuteAsync(connection, cancellationToken, $"EXECUTE AS USER = N'{user}';");
    }

    /// <summary>Comes back to the rights of the account that opened the connection.</summary>
    public static Task RevertAsync(DbConnection connection, CancellationToken cancellationToken) =>
        ExecuteAsync(connection, cancellationToken, "REVERT;");

    /// <summary>
    /// The data permissions that the current user holds on each object (`schema.name`), as `schema.name|PERMISSION` in ordinal order, computed
    /// by the server (`HAS_PERMS_BY_NAME`: roles, grants and denies together). An object that the user cannot see has none.
    /// </summary>
    public static async Task<IReadOnlyList<string>> PermissionsAsync(DbConnection connection, IEnumerable<string> objects, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(objects);
        var held = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var name in objects)
        {
            foreach (var permission in DataPermissions)
            {
                await using var command = Command(connection,
                    "SELECT HAS_PERMS_BY_NAME(QUOTENAME(PARSENAME(@object, 2)) + N'.' + QUOTENAME(PARSENAME(@object, 1)), N'OBJECT', @permission);",
                    ("@object", name), ("@permission", permission));
                if (await command.ExecuteScalarAsync(cancellationToken) is int and 1)
                    held.Add($"{name}|{permission}");
            }
        }
        return [.. held];
    }

    /// <summary>Every row of a view of the report schema, with the rights of the current user.</summary>
    public static async Task<ReportViewRows> ReadViewAsync(DbConnection connection, string view, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(view);
        if (!view.All(character => character is (>= 'a' and <= 'z') or '_'))
            throw new ArgumentException($"{view} is not the name of a report view.", nameof(view));
        await using var command = Command(connection, $"SELECT * FROM [{SqlServerAccess.ReportSchema}].[{view}];");
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var columns = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToList();
        var rows = new List<object?[]>();
        while (await reader.ReadAsync(cancellationToken))
            rows.Add(Enumerable.Range(0, reader.FieldCount).Select(i => reader.IsDBNull(i) ? null : reader.GetValue(i)).ToArray());
        return new ReportViewRows(columns, rows);
    }

    /// <summary>Every row of the users, with the rights of the current user (or of the application role it took).</summary>
    public static async Task<ReportViewRows> ReadUsersAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await using var command = Command(connection, $"SELECT * FROM [{SqlServerEngine.Schema}].[{TableNames.AppUser}];");
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var columns = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToList();
        var rows = new List<object?[]>();
        while (await reader.ReadAsync(cancellationToken))
            rows.Add(Enumerable.Range(0, reader.FieldCount).Select(i => reader.IsDBNull(i) ? null : reader.GetValue(i)).ToArray());
        return new ReportViewRows(columns, rows);
    }

    /// <summary>
    /// Gives the session the rights of <see cref="ApplicationRole"/> by its password (`sp_setapprole`), as any session that
    /// knows it could, an impersonated one included; the cookie returned gives the session back
    /// (<see cref="UnsetApplicationRoleAsync"/>). Called as a procedure: the server refuses it inside another one.
    /// </summary>
    public static async Task<byte[]> SetApplicationRoleAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        await using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "sys.sp_setapprole";
        Parameter(command, "@rolename", DbType.String, ApplicationRole);
        Parameter(command, "@password", DbType.String, ApplicationRolePassword);
        Parameter(command, "@fCreateCookie", DbType.Boolean, true);
        var cookie = Parameter(command, "@cookie", DbType.Binary, DBNull.Value);
        cookie.Direction = ParameterDirection.Output;
        cookie.Size = 8000;
        await command.ExecuteNonQueryAsync(cancellationToken);
        return (byte[])cookie.Value!;
    }

    /// <summary>Gives the session back the rights it had before <see cref="SetApplicationRoleAsync"/>.</summary>
    public static async Task UnsetApplicationRoleAsync(DbConnection connection, byte[] cookie, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        await using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "sys.sp_unsetapprole";
        Parameter(command, "@cookie", DbType.Binary, cookie);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>Grants a data permission on an object (`schema.name`) to a principal, as an administrator could by hand.</summary>
    public static Task GrantAsync(DbConnection connection, string permission, string target, string principal, CancellationToken cancellationToken) =>
        ChangeAsync(connection, "GRANT", permission, target, principal, cancellationToken);

    /// <summary>Removes a grant or a deny of a data permission on an object (`schema.name`), as an administrator could by hand.</summary>
    public static Task RevokeAsync(DbConnection connection, string permission, string target, string principal, CancellationToken cancellationToken) =>
        ChangeAsync(connection, "REVOKE", permission, target, principal, cancellationToken);

    /// <summary>Removes a member from a role, as an administrator could by hand.</summary>
    public static Task RemoveMemberAsync(DbConnection connection, string role, string member, CancellationToken cancellationToken) =>
        ExecuteAsync(connection, cancellationToken,
            "DECLARE @text nvarchar(max) = N'ALTER ROLE ' + QUOTENAME(@role) + N' DROP MEMBER ' + QUOTENAME(@member) + N';'; " +
            "EXEC sys.sp_executesql @text;",
            ("@role", role), ("@member", member));

    /// <summary>Grants a data permission on the whole database (every table of every schema) to a principal, as an administrator could by hand.</summary>
    public static Task GrantOnDatabaseAsync(DbConnection connection, string permission, string principal, CancellationToken cancellationToken) =>
        ChangeOnDatabaseAsync(connection, "GRANT", permission, principal, cancellationToken);

    /// <summary>Removes the grant of <see cref="GrantOnDatabaseAsync"/>.</summary>
    public static Task RevokeOnDatabaseAsync(DbConnection connection, string permission, string principal, CancellationToken cancellationToken) =>
        ChangeOnDatabaseAsync(connection, "REVOKE", permission, principal, cancellationToken);

    /// <summary>Makes the change by hand.</summary>
    public static Task ApplyAsync(DbConnection connection, CatalogChange change, CancellationToken cancellationToken) =>
        ExecuteAsync(connection, cancellationToken, Statements(change).Apply);

    /// <summary>Undoes the change made by <see cref="ApplyAsync"/>.</summary>
    public static Task UndoAsync(DbConnection connection, CatalogChange change, CancellationToken cancellationToken) =>
        ExecuteAsync(connection, cancellationToken, Statements(change).Undo);

    /// <summary>The statement that makes the change and the one that undoes it; each runs alone in its batch.</summary>
    private static (string Apply, string Undo) Statements(CatalogChange change) =>
        RoleStatements(change) ?? ObjectStatements(change);

    /// <summary>The changes of <see cref="CatalogChange"/> that add a principal: an application role, a database role.</summary>
    private static (string Apply, string Undo)? RoleStatements(CatalogChange change)
    {
        var application = $"CREATE APPLICATION ROLE [{ApplicationRole}] WITH PASSWORD = N'{ApplicationRolePassword}';";
        return change switch
        {
            CatalogChange.ApplicationRoleAlone => (application, $"DROP APPLICATION ROLE [{ApplicationRole}];"),
            CatalogChange.ApplicationRoleInTheApplicationRole => InRole(application, ApplicationRole, "APPLICATION ROLE", SqlServerAccess.ApplicationRole),
            CatalogChange.ApplicationRoleInTheReportRole => InRole(application, ApplicationRole, "APPLICATION ROLE", SqlServerAccess.ReportRole),
            CatalogChange.DatabaseRoleInTheApplicationRole => InRole("CREATE ROLE [probe_nested];", "probe_nested", "ROLE", SqlServerAccess.ApplicationRole),
            CatalogChange.DatabaseRoleInTheReportRole => InRole("CREATE ROLE [probe_nested];", "probe_nested", "ROLE", SqlServerAccess.ReportRole),
            _ => null,
        };
    }

    private static (string Apply, string Undo) InRole(string create, string member, string kind, string role) => (
        $"{create} ALTER ROLE [{role}] ADD MEMBER [{member}];",
        $"ALTER ROLE [{role}] DROP MEMBER [{member}]; DROP {kind} [{member}];");

    private static (string Apply, string Undo) ObjectStatements(CatalogChange change)
    {
        var data = $"[{SqlServerEngine.Schema}]";
        var report = $"[{SqlServerAccess.ReportSchema}]";
        var users = $"{data}.[{TableNames.AppUser}]";
        return change switch
        {
            CatalogChange.ReportFunction => (
                $"CREATE FUNCTION {report}.[leak]() RETURNS TABLE AS RETURN SELECT * FROM {users};", $"DROP FUNCTION {report}.[leak];"),
            CatalogChange.ReportSynonym => ($"CREATE SYNONYM {report}.[users] FOR {users};", $"DROP SYNONYM {report}.[users];"),
            CatalogChange.TableTrigger => (
                $"CREATE TRIGGER {data}.[probe_users_trigger] ON {users} AFTER INSERT AS SET NOCOUNT ON;", $"DROP TRIGGER {data}.[probe_users_trigger];"),
            CatalogChange.HistoryTrigger => (
                $"CREATE TRIGGER {data}.[probe_history_trigger] ON {data}.[{TableNames.MigrationsHistory}] AFTER INSERT AS SET NOCOUNT ON;",
                $"DROP TRIGGER {data}.[probe_history_trigger];"),
            CatalogChange.Procedure => ($"CREATE PROCEDURE {data}.[purge_trash] AS SET NOCOUNT ON;", $"DROP PROCEDURE {data}.[purge_trash];"),
            CatalogChange.Sequence => ($"CREATE SEQUENCE {data}.[probe_sequence];", $"DROP SEQUENCE {data}.[probe_sequence];"),
            CatalogChange.UserType => ($"CREATE TYPE {data}.[probe_code] FROM nvarchar(10);", $"DROP TYPE {data}.[probe_code];"),
            CatalogChange.DatabaseTrigger => (
                "CREATE TRIGGER [probe_database_trigger] ON DATABASE FOR CREATE_TABLE AS SET NOCOUNT ON;",
                "DROP TRIGGER [probe_database_trigger] ON DATABASE;"),
            CatalogChange.PublicGrant => ("GRANT SELECT TO [public];", "REVOKE SELECT FROM [public];"),
            CatalogChange.SynonymOutsideTheSchemas => (
                $"CREATE SYNONYM [dbo].[probe_users] FOR {users}; GRANT SELECT ON [dbo].[probe_users] TO [{SqlServerAccess.ReportRole}];",
                "DROP SYNONYM [dbo].[probe_users];"),
            CatalogChange.ReportViewNamedAsTheHistory => (
                $"CREATE VIEW [bi].[{HistoryInLowerCase}] AS SELECT * FROM {users};", $"DROP VIEW [bi].[{HistoryInLowerCase}];"),
            CatalogChange.ReportTableNamedAsTheHistory => (
                $"CREATE TABLE {report}.[{TableNames.MigrationsHistory}] ([id] int NOT NULL);", $"DROP TABLE {report}.[{TableNames.MigrationsHistory}];"),
            CatalogChange.UsersOwnedByTheReports => (
                $"ALTER AUTHORIZATION ON OBJECT::{users} TO [{SqlServerAccess.ReportRole}];", $"ALTER AUTHORIZATION ON OBJECT::{users} TO SCHEMA OWNER;"),
            CatalogChange.ReportViewOwnedByTheReports => (
                $"ALTER AUTHORIZATION ON OBJECT::{report}.[{TableNames.Project}] TO [{SqlServerAccess.ReportRole}];",
                $"ALTER AUTHORIZATION ON OBJECT::{report}.[{TableNames.Project}] TO SCHEMA OWNER;"),
            CatalogChange.SchemaOwnedByTheReports => (
                $"CREATE SCHEMA [probe_reports] AUTHORIZATION [{SqlServerAccess.ReportRole}];", "DROP SCHEMA [probe_reports];"),
            CatalogChange.ShippedFunctionOutsideTheSchemas => (
                $"EXEC(N'CREATE FUNCTION [dbo].[probe_shipped]() RETURNS TABLE AS RETURN SELECT * FROM {users}'); " +
                "EXEC sys.sp_MS_marksystemobject N'dbo.probe_shipped';",
                "DROP FUNCTION [dbo].[probe_shipped];"),
            CatalogChange.ShippedFunctionGivenToTheReports => (
                $"EXEC(N'CREATE FUNCTION [dbo].[probe_shipped]() RETURNS TABLE AS RETURN SELECT * FROM {users}'); " +
                $"EXEC sys.sp_MS_marksystemobject N'dbo.probe_shipped'; GRANT SELECT ON [dbo].[probe_shipped] TO [{SqlServerAccess.ReportRole}];",
                "DROP FUNCTION [dbo].[probe_shipped];"),
            _ => throw new ArgumentOutOfRangeException(nameof(change), change, "Not a change of the catalogue."),
        };
    }

    private static Task ChangeOnDatabaseAsync(DbConnection connection, string statement, string permission, string principal, CancellationToken cancellationToken)
    {
        if (!DataPermissions.Contains(permission, StringComparer.Ordinal))
            throw new ArgumentException($"{permission} is not a data permission.", nameof(permission));
        var preposition = statement == "GRANT" ? "TO" : "FROM";
        return ExecuteAsync(connection, cancellationToken,
            $"DECLARE @text nvarchar(max) = N'{statement} {permission} {preposition} ' + QUOTENAME(@principal) + N';'; EXEC sys.sp_executesql @text;",
            ("@principal", principal));
    }

    private static Task ChangeAsync(DbConnection connection, string statement, string permission, string target, string principal, CancellationToken cancellationToken)
    {
        if (!DataPermissions.Contains(permission, StringComparer.Ordinal))
            throw new ArgumentException($"{permission} is not a data permission.", nameof(permission));
        var preposition = statement == "GRANT" ? "TO" : "FROM";
        return ExecuteAsync(connection, cancellationToken,
            $"DECLARE @text nvarchar(max) = N'{statement} {permission} ON OBJECT::' + QUOTENAME(PARSENAME(@object, 2)) + N'.' + " +
            $"QUOTENAME(PARSENAME(@object, 1)) + N' {preposition} ' + QUOTENAME(@principal) + N';'; EXEC sys.sp_executesql @text;",
            ("@object", target), ("@principal", principal));
    }

    private static async Task ExecuteAsync(DbConnection connection, CancellationToken cancellationToken, string text, params (string Name, string Value)[] parameters)
    {
        await using var command = Command(connection, text, parameters);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static DbParameter Parameter(DbCommand command, string name, DbType type, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value;
        command.Parameters.Add(parameter);
        return parameter;
    }

    private static DbCommand Command(DbConnection connection, string text, params (string Name, string Value)[] parameters)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var command = connection.CreateCommand();
        command.CommandText = text;
        foreach (var (name, value) in parameters)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            command.Parameters.Add(parameter);
        }
        return command;
    }
}
