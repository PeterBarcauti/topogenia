// ProjectGraph.cs
// A complete fictitious project: one record of every type of the domain, every code of every closed list, every optional field set.

using System.Text.RegularExpressions;
using Topogenia.Domain.Model;
using Topogenia.Domain.Values;

namespace Topogenia.Persistence.Tests.Infrastructure;

/// <summary>
/// A coherent set of records of one project, written in one save: users, project, counters, members, deletion batches,
/// root map and detailed map, lanes of the three types, steps of the seven types, flows (one deleted), RACI roles of the
/// four letters, a dissolution and its steps and objects, sheets of every status and frequency, procedure rows of every
/// nature, a validated version, documents of both directions and both location types, uses, pain points and their whole
/// status history, translations of every object, two log entries and an audit event. Dates carry ticks below the
/// millisecond and are UTC. Each graph has its own codes and logins, so many graphs share one database.
/// </summary>
public sealed partial class ProjectGraph
{
    private static int counter;
    private readonly List<object> records = [];

    /// <summary>The glossary of the translations of a graph (ADR 065): several lines, a tab, an accent.</summary>
    public const string Glossary = "monthly closing = clôture mensuelle\n\treconciliation = rapprochement";

    /// <summary>The entries of the value stream (ADR 085) are written: work time, headcount, volume, share, lead times.</summary>
    private readonly bool valueStream;

    private ProjectGraph(int number, Classification classification, string? glossary, bool valueStream)
    {
        this.valueStream = valueStream;
        Number = number;
        Code = CodeOf(number);
        Time = FixedClock.Time.AddMinutes(number);
        Users();
        Project = Add(new Project(Guid.CreateVersion7(), Code, $"Fictitious process {number}", "fr", "Purchase orders of a fictitious plant",
            @"\\files\fictitious\process", classification, "Process team", glossary, 7, At(1), Owner.Id, At(2), Reader.Id, null, null, 3)
        {
            WorkdayHours = valueStream ? 7.5m : WorkTime.DefaultWorkdayHours,
            FteHoursPerMonth = valueStream ? 160m : WorkTime.DefaultFteHoursPerMonth,
        });
        foreach (var kind in Enum.GetValues<CounterKind>())
            Add(new ProjectCounter(Project.Id, kind, 10 + (int)kind));
        Members();
        Batch = Add(new DeletionBatch(Guid.CreateVersion7(), Project.Id, Guid.CreateVersion7(), At(5), Owner.Id, null, null));
        RestoredBatch = Add(new DeletionBatch(Guid.CreateVersion7(), Project.Id, null, At(6), Owner.Id, At(7), Contributor.Id));
        Structure();
        Dissolutions();
        BuildSheets();
        BuildDocuments();
        Journal();
    }

    public int Number { get; }

    /// <summary>The project code: six capital letters, unique per graph.</summary>
    public string Code { get; }

    public DateTime Time { get; }

    public AppUser Owner { get; private set; } = null!;

    public AppUser Contributor { get; private set; } = null!;

    public AppUser Reader { get; private set; } = null!;

    public Project Project { get; }

    public DeletionBatch Batch { get; }

    public DeletionBatch RestoredBatch { get; }

    /// <summary>Every record of the graph, in the order they were built.</summary>
    public IReadOnlyList<object> Records => records;

    /// <summary>The records of one type.</summary>
    public IReadOnlyList<T> Of<T>() => records.OfType<T>().ToList();

    public static ProjectGraph New(Classification classification = Classification.StrictlyConfidential) =>
        new(Interlocked.Increment(ref counter), classification, Glossary, valueStream: true);

    /// <summary>
    /// A project as the versions before the glossary of the translations wrote it (ADR 065): every field but the glossary
    /// and the entries of the value stream (ADR 085), its work time the default one, which the migration gives the projects
    /// it finds. The tests of a database of an earlier version write it with the model of today, then take the base down
    /// to that version by the ways down of the migrations, which drop what came after it.
    /// </summary>
    public static ProjectGraph BeforeGlossary() => new(Interlocked.Increment(ref counter), Classification.StrictlyConfidential, null, valueStream: false);

    /// <summary>A date of the graph: its time plus some minutes and ticks below the millisecond.</summary>
    public DateTime At(int minutes) => Time.AddMinutes(minutes).AddTicks(minutes * 17 + 3);

    /// <summary>A readable code of the project (`ABCDEF-E001`).</summary>
    public string ReadableCode(char letter, int value) => $"{Code}-{letter}{value:000}";

    private T Add<T>(T record)
        where T : notnull
    {
        records.Add(record);
        return record;
    }

    private static string CodeOf(int number)
    {
        var letters = new char[6];
        var value = number + (Environment.ProcessId % 1000 * 100_000);
        for (var i = 5; i >= 0; i--)
        {
            letters[i] = (char)('A' + (value % 26));
            value /= 26;
        }
        return new string(letters);
    }

    /// <summary>
    /// True when <paramref name="login"/> is the login of a user of some graph: the fictitious domain, a name of user, then
    /// the number of the graph. Its number ends it: a digit appended to it is the login of another graph sharing the same
    /// database (`Owner1` and `2` give `Owner12`, the owner of graph 12).
    /// </summary>
    public static bool IsLoginOfAGraph(string login) => GraphLogin().IsMatch(login);

    /// <summary>Another user made from <paramref name="user"/>: a new identifier and a login that no graph gives, whatever its number (<see cref="IsLoginOfAGraph"/>).</summary>
    public static AppUser OtherUser(AppUser user, string tag)
    {
        ArgumentNullException.ThrowIfNull(user);
        var login = $"{user.Login}_{tag}";
        return user with { Id = Guid.CreateVersion7(), Login = login, LoginNormalized = Login.Normalize(login) };
    }

    [GeneratedRegex(@"^FICTITIOUS\\(Owner|Contributor|Reader)[0-9]+$", RegexOptions.IgnoreCase)]
    private static partial Regex GraphLogin();

    private void Users()
    {
        AppUser User(string name, bool admin, string? language) =>
            Add(new AppUser(Guid.CreateVersion7(), $@"FICTITIOUS\{name}{Number}", Login.Normalize($@"FICTITIOUS\{name}{Number}"),
                $"{name} {Number}", $"{name.ToUpperInvariant()}{Number}@example.invalid", language, admin, !admin,
                admin ? Guid.CreateVersion7() : null, At(0), admin ? At(1) : null, null, admin ? 2 : 1));
        Owner = User("Owner", true, "en");
        Contributor = User("Contributor", false, "fr");
        Reader = User("Reader", false, null);
    }

    private void Members()
    {
        Add(new ProjectMember(Project.Id, Owner.Id, MemberRole.Facilitator, true, false, 0, At(1), Owner.Id, null, null));
        Add(new ProjectMember(Project.Id, Contributor.Id, MemberRole.Contributor, false, true, 1, At(1), Owner.Id, At(3), Owner.Id) { Version = 4 });
        Add(new ProjectMember(Project.Id, Reader.Id, MemberRole.Reader, false, false, 2, At(1), Owner.Id, null, null));
    }
}
