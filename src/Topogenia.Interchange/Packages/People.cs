// People.cs
// Logins of the package: a user identifier becomes its login, or nothing at all when the export leaves out the people.

using System.Text.Json.Nodes;

namespace Topogenia.Interchange.Packages;

/// <summary>
/// Writes the people of the package by login (spec 7.2: authors exported by login, anonymised when the option asks for
/// it). Without people, no login is written anywhere, the property is left out. A user named by an exported record but
/// absent from the logins given by the caller is a fault of the caller, never written as an empty author.
/// </summary>
internal sealed class People(Func<Guid, string> login, bool hidden)
{
    public People(IReadOnlyDictionary<Guid, string> logins, bool hidden)
        : this(id => logins.TryGetValue(id, out var found)
            ? found
            : throw new InvalidOperationException($"No login was given for the user {id} named by the exported project."), hidden)
    {
    }

    public bool Hidden { get; } = hidden;

    /// <summary>Sets <paramref name="property"/> to the login of <paramref name="userId"/>, unless hidden or null.</summary>
    public void Write(JsonObject target, string property, Guid? userId)
    {
        if (Hidden || userId is not { } id)
            return;
        target[property] = login(id);
    }

    /// <summary>The creation and update authors and times of a record (times are written even without people).</summary>
    public void WriteAudit(JsonObject target, DateTime createdAt, Guid? createdBy, DateTime? updatedAt, Guid? updatedBy)
    {
        target["createdAt"] = Json.CanonicalJson.Date(createdAt);
        Write(target, "createdBy", createdBy);
        if (updatedAt is { } updated)
            target["updatedAt"] = Json.CanonicalJson.Date(updated);
        Write(target, "updatedBy", updatedBy);
    }
}
