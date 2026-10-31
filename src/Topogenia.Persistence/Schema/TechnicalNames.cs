// TechnicalNames.cs
// The fixed technical names of the solution (namespaces, assemblies, SQL Server schema and roles), kept apart from the displayed product name (ADR 013).

namespace Topogenia.Persistence.Schema;

/// <summary>
/// The technical names, fixed like the namespaces of the solution (spec 3.1): the root of the namespaces and of the
/// assemblies, the assembly of the persistence tests, and the SQL Server schema of the tables and of the histories of the
/// migrations, from which the two roles derive (spec 4.1, 4.4). They are code names, never the displayed product name
/// (<c>ProductIdentity.Name</c>, D13): renaming the product changes neither the schema that the migrations froze, nor the
/// roles, nor the assembly that the providers open their internals to. The root is the identifier of the root namespace
/// itself, so it is written as an identifier, never as text (ADR 013).
/// </summary>
public static class TechnicalNames
{
    /// <summary>The root of the namespaces and of the assemblies of the solution.</summary>
    public const string Root = nameof(Topogenia);

    /// <summary>The assembly of the persistence tests, the only friend of the two providers (ADR 037).</summary>
    public const string PersistenceTests = Root + ".Persistence.Tests";

    /// <summary>The SQL Server schema of the tables and of the histories of the migrations: the root in lower case, as the migrations froze it.</summary>
    public static string Schema { get; } = Root.ToLowerInvariant();
}
