// CreationRaces.cs
// A unit that lost the race to create a record it creates or updates by its natural key runs again and updates it (ADR 033, A-5-14).

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;
using Topogenia.Persistence.Errors;

namespace Topogenia.Persistence.Writing;

/// <summary>
/// Three records are created or updated by their natural key, as the 1.x `MERGE ... WITH (HOLDLOCK)` did (spec annex A 5):
/// a user by its normalised login (`Utilisateurs.cs`), a translation by its object, field and language (`Traductions.cs`),
/// a use by its document and step (`ServiceFiches.Documents.cs`). The domain creates the record when the workspace does
/// not hold it; a concurrent unit that created it since the loading makes the creation fail on the unique key. That
/// refusal is not a conflict between two users: the unit runs again from the start (with the retries of spec 4.3-5), loads
/// the record and updates it, so that the write is portable and safe under concurrency without any hint of an engine. Any
/// other unique key keeps its refusal (`CONFLIT_UNICITE`).
/// </summary>
internal static class CreationRaces
{
    /// <summary>The lost race carried by the exception, or null.</summary>
    public static StoreError? Of(Exception failure, IModel model) =>
        failure is DomainException { Code: ErrorCodes.UniqueConflict } refusal
        && refusal.Details.TryGetValue(StoreRefusals.ConstraintDetail, out var value) && value is string constraint
        && KeysOf(model).Contains(constraint)
            ? new StoreError(StoreErrorKind.CreationRace, constraint)
            : null;

    /// <summary>The names of the natural keys of the records created or updated by their key.</summary>
    public static IReadOnlySet<string> KeysOf(IModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        var user = Entity(model, typeof(AppUser));
        var login = user.GetIndexes().Single(index => index.IsUnique && index.Properties is [{ Name: nameof(AppUser.LoginNormalized) }]);
        return new HashSet<string>(StringComparer.Ordinal)
        {
            login.GetDatabaseName()!,
            Entity(model, typeof(Translation)).FindPrimaryKey()!.GetName()!,
            Entity(model, typeof(DocumentUse)).FindPrimaryKey()!.GetName()!,
        };
    }

    private static IEntityType Entity(IModel model, Type type) =>
        model.FindEntityType(type) ?? throw new InvalidOperationException($"{type.Name} is not a record of the model.");
}
