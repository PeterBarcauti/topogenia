// MigratedDatabases.cs
// One migrated database per engine shared by the tests of a class, created at the first use and removed at the end.

namespace Topogenia.Persistence.Tests.Infrastructure;

/// <summary>
/// Class fixture of the tests that write rows: each engine gets one database, migrated once, shared by the tests of the
/// class (each test writes its own project, with its own codes and identifiers) and removed when the class is done. A
/// database is kept only once its migration has succeeded (<see cref="TestDatabase.MigratedAsync"/>, which removes it
/// otherwise): a failed preparation fails the test that asked for it, and the next test of the class starts a new one,
/// instead of every test of the class failing on a base that does not exist. Every database is removed at the end, even
/// when the removal of another one fails; the failures are thrown together after.
/// </summary>
public sealed class MigratedDatabases : IAsyncDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Dictionary<EngineKind, TestDatabase> databases = [];
    private readonly Func<EngineKind, Task<TestDatabase>> prepare;

    public MigratedDatabases()
        : this(TestDatabase.MigratedAsync)
    {
    }

    /// <summary>A fixture whose databases come from <paramref name="prepare"/> (the tests of the fixture itself).</summary>
    internal MigratedDatabases(Func<EngineKind, Task<TestDatabase>> prepare) => this.prepare = prepare;

    public async Task<TestDatabase> OfAsync(EngineKind kind)
    {
        await gate.WaitAsync(TestContext.Current.CancellationToken);
        try
        {
            if (!databases.TryGetValue(kind, out var database))
            {
                database = await prepare(kind);
                databases[kind] = database;
            }
            return database;
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        var failures = new List<Exception>();
        foreach (var database in databases.Values)
        {
            try
            {
                await database.DisposeAsync();
            }
            catch (Exception failure)
            {
                failures.Add(failure);
            }
        }
        databases.Clear();
        gate.Dispose();
        if (failures.Count > 0)
            throw new AggregateException("Some test databases could not be removed.", failures);
    }
}
