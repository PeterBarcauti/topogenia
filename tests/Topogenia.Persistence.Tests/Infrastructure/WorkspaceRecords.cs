// WorkspaceRecords.cs
// Every record held by a workspace, by key, so that a workspace kept in memory and one loaded again can be compared whole.

using Topogenia.Domain.Model;
using Topogenia.Domain.Workspace;

namespace Topogenia.Persistence.Tests.Infrastructure;

/// <summary>The records of a workspace of every type of the model a workspace can hold (the log and the audit aside).</summary>
public static class WorkspaceRecords
{
    private static readonly Func<ProjectWorkspace, IEnumerable<IRecord>>[] Types =
    [
        All<AppUser>, All<Project>, All<ProjectCounter>, All<ProjectMember>, All<DeletionBatch>, All<ProcessMap>, All<Lane>, All<Step>,
        All<Flow>, All<StepRaci>, All<Dissolution>, All<DissolutionStep>, All<DissolutionObject>, All<StepSheet>, All<SheetOperation>,
        All<StepSheetVersion>, All<Document>, All<DocumentUse>, All<PainPoint>, All<PainPointStatusChange>, All<Translation>,
    ];

    public static Dictionary<RecordKey, IRecord> Of(ProjectWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        return Types.SelectMany(type => type(workspace)).ToDictionary(record => record.Key);
    }

    /// <summary>The two workspaces hold the same records, every field equal (dates to the tick).</summary>
    public static void AssertSame(ProjectWorkspace expected, ProjectWorkspace actual)
    {
        var wanted = Of(expected);
        var present = Of(actual);
        Assert.Equal(Sorted(wanted.Keys), Sorted(present.Keys));
        foreach (var (key, record) in wanted)
            Assert.Equal(record, present[key]);
    }

    public static List<string> Sorted(IEnumerable<RecordKey> keys) => keys.Select(key => key.ToString()).Order(StringComparer.Ordinal).ToList();

    private static IEnumerable<IRecord> All<T>(ProjectWorkspace workspace)
        where T : IRecord => workspace.All<T>().Cast<IRecord>();
}
