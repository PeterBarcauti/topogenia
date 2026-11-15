// DomainModelRulesTests.cs
// Rules of the domain model and of its folders: versioned records, stamping, record keys, versions and commit in the workspace, counters never replayed, no folder cycle.

using Topogenia.Domain.Model;
using Topogenia.Domain.Structure;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;

namespace Topogenia.Architecture.Tests;

public sealed partial class DomainModelRulesTests
{
    /// <summary>
    /// The records of the model that carry no version (D06), each with the reason why no concurrent change has to be
    /// detected on it. Any other record is versioned; this list is exact (architecture.md 2.1).
    /// </summary>
    private static readonly Dictionary<Type, string> UnversionedRecords = new()
    {
        [typeof(ProjectCounter)] = "only moves forward under the project lock and is never replayed (D07)",
        [typeof(DeletionBatch)] = "never edited by a request: created by the workspace at the first deletion of an operation, marked restored by an undo, dated again by a redo, removed by the purge; the undo checks its objects and its content (AR-81)",
        [typeof(OperationLogEntry)] = "the journal itself, written by the Application",
        [typeof(SheetOperation)] = "a row of the procedure of a sheet, written only by the save of the draft, which moves the version of the sheet whenever a row changes (SheetWorkflow.Save touches the sheet)",
        [typeof(StepSheetVersion)] = "a frozen validated version, never modified",
        [typeof(PainPointStatusChange)] = "append-only history",
        [typeof(AuditEvent)] = "append-only security journal",
        [typeof(DissolutionStep)] = "created and removed with its versioned dissolution, never modified",
        [typeof(DissolutionObject)] = "created and removed with its versioned dissolution, never modified",
    };

    [Fact]
    public void Every_record_of_the_model_is_versioned_unless_listed_with_its_reason()
    {
        var records = typeof(IRecord).Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(IRecord).IsAssignableFrom(type))
            .ToList();
        var unversioned = records.Where(type => !typeof(IVersionedRecord).IsAssignableFrom(type)).Select(type => type.Name).Order(StringComparer.Ordinal);

        Assert.Equal(UnversionedRecords.Keys.Select(type => type.Name).Order(StringComparer.Ordinal), unversioned);
        Assert.All(records, type => Assert.Equal("Topogenia.Domain.Model", type.Namespace));
    }

    /// <summary>
    /// D06, AR-32: only the workspace stamps a version and commits; a rule never does (architecture.md 2.2). Read on the
    /// resolved symbols: a call, a method group or a using static directive of ProjectWorkspace.Commit or of the Stamp of
    /// a versioned record is seen; the workspace holds both (positive control).
    /// </summary>
    [Fact]
    public void Only_the_workspace_stamps_a_record_or_commits_a_change_set()
    {
        CoreSources.Require();
        Assert.Empty(SemanticDetectors.Offenders(SemanticSources.Production, SemanticDetectors.Stamps, SemanticDetectors.WorkspaceFolder));
        Assert.Empty(SemanticDetectors.Offenders(SemanticSources.Domain, SemanticDetectors.Commits, SemanticDetectors.WorkspaceFolder));
        Assert.Contains(SemanticSources.Domain, file => file.Path == SemanticDetectors.WorkspaceFolder + "Versioning.cs" && SemanticDetectors.Stamps(file).Any());
    }

    /// <summary>
    /// D06, AR-32: a rule never writes a version nor an update audit: no assignment of Version, UpdatedAt or UpdatedBy of a
    /// versioned record outside Workspace/Versioning (with expression, initialiser, statement) and no creation with a
    /// version other than 1. A record whose key changes is a new record, so `with { Version = 1 }` stays allowed.
    /// </summary>
    [Fact]
    public void Only_the_versioning_writes_a_version_or_an_update_audit()
    {
        CoreSources.Require();
        Assert.Empty(SemanticDetectors.Offenders(SemanticSources.Domain, SemanticDetectors.VersionWrites, SemanticDetectors.WorkspaceFolder + "Versioning.cs"));
    }

    /// <summary>
    /// ARC4-M3: a record key is built only by the KeyOf method of its record, in the model; every rule, query and the
    /// workspace call it, so that a key is never written twice (positive control: the model builds them).
    /// </summary>
    [Fact]
    public void Record_keys_are_built_only_by_the_records_of_the_model()
    {
        CoreSources.Require();
        Assert.Empty(SemanticDetectors.Offenders(SemanticSources.Production, SemanticDetectors.RecordKeyBuilds, SemanticSources.DomainFolder + "Model/"));
        Assert.Contains(SemanticSources.Domain, file => file.Path == SemanticSources.DomainFolder + "Model/StructureRecords.cs" && SemanticDetectors.RecordKeyBuilds(file).Any());
    }

    /// <summary>
    /// ARC4 minor 4: Versioning.Update is internal to the domain and only the rules of the users call it, for the profile
    /// that belongs to no project (AR-07); a project record is stamped by the commit of its workspace only.
    /// </summary>
    [Fact]
    public void Only_the_rules_of_the_users_stamp_a_record_outside_a_workspace()
    {
        CoreSources.Require();
        const string userRules = SemanticSources.DomainFolder + "Projects/UserRules.cs";
        Assert.Empty(SemanticDetectors.Offenders(SemanticSources.Production, SemanticDetectors.VersioningUpdates, userRules));
        Assert.Contains(SemanticSources.Domain, file => file.Path == userRules && SemanticDetectors.VersioningUpdates(file).Any());
        Assert.True(typeof(Versioning).GetMethod("Update", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)?.IsAssembly);
    }

    /// <summary>D07: neither the undo nor the redo puts a readable code counter back, so that a code is never given twice.</summary>
    [Fact]
    public void Readable_code_counters_are_never_replayed_by_an_undo_or_a_redo()
    {
        var projectId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var actor = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var now = new DateTime(2026, 10, 23, 8, 0, 0, DateTimeKind.Utc);
        var next = 0x10;
        OperationContext Context() => new(actor, now, "fr", AppSettings.Default, () => new Guid($"00000000-0000-0000-0001-{next++:x12}"));
        var project = new Project(projectId, "PRJ", "Projet", "fr", null, null, Classification.Internal, null, null, 0, now, actor, null, null, null, null, 1);
        var workspace = new ProjectWorkspace(projectId, [project, new ProjectCounter(projectId, CounterKind.Step, 4)]);
        var context = Context();
        workspace.NextCode(CounterKind.Step);
        var forward = workspace.Commit(context);
        var counter = RecordKey.Of("counter", projectId, Codes.Of(CounterKind.Step));

        var undoContext = Context();
        UndoReplay.Undo(workspace, undoContext, forward);
        var undo = workspace.Commit(undoContext);
        Assert.Equal(5, workspace.Find<ProjectCounter>(counter)!.LastValue);
        workspace.Put(workspace.Find<ProjectCounter>(counter)! with { LastValue = 7 });
        workspace.Commit(Context());
        UndoReplay.Redo(workspace, Context(), forward, undo);
        Assert.Equal(7, workspace.Find<ProjectCounter>(counter)!.LastValue);
    }

    /// <summary>The cycles of a dependency graph, each written as its folders in order, the first one repeated at the end.</summary>
    internal static IReadOnlyList<string> Cycles(IReadOnlyDictionary<string, HashSet<string>> graph)
    {
        var cycles = new List<string>();
        var state = new Dictionary<string, int>(StringComparer.Ordinal);
        var path = new List<string>();
        void Visit(string node)
        {
            state[node] = 1;
            path.Add(node);
            foreach (var target in graph.GetValueOrDefault(node, []).Order(StringComparer.Ordinal))
            {
                if (state.GetValueOrDefault(target) == 1)
                    cycles.Add(string.Join(" -> ", path.Skip(path.IndexOf(target)).Append(target)));
                else if (state.GetValueOrDefault(target) == 0)
                    Visit(target);
            }
            path.RemoveAt(path.Count - 1);
            state[node] = 2;
        }
        foreach (var node in graph.Keys.Order(StringComparer.Ordinal).Where(node => state.GetValueOrDefault(node) == 0))
            Visit(node);
        return cycles;
    }

    /// <summary>
    /// Spec 3.3: the folders of the domain form layers; no folder depends, even indirectly, on a folder that depends on it.
    /// The graph is read on the compiled domain: every name resolved by the semantic model (using directives, partial
    /// names such as `Dissolutions.X` written inside the domain namespace, full names, members, extension methods).
    /// </summary>
    [Fact]
    public void The_folders_of_the_domain_depend_on_each_other_without_cycle()
    {
        CoreSources.Require();
        var graph = SemanticDetectors.FolderGraph(SemanticSources.Domain);
        Assert.Contains("Workspace", graph.GetValueOrDefault("Structure", []));
        Assert.Empty(Cycles(graph));
    }

    /// <summary>The lower layers of the domain, from the bottom (architecture.md 2.4); every other folder is a folder of rules.</summary>
    private static readonly string[][] LowerLayers = [["Errors", "Values"], ["Model"], ["Workspace"], ["Structure"]];

    /// <summary>Folders that may depend on the listed folders only: Blocks is a pure function of identifiers and flows.</summary>
    private static readonly Dictionary<string, string[]> OnlyOn = new(StringComparer.Ordinal) { ["Blocks"] = ["Errors", "Values", "Model"] };

    /// <summary>Folders of rules kept under others: Dissolutions is read by Layout and Selection and never reads them.</summary>
    private static readonly Dictionary<string, string[]> NeverOn = new(StringComparer.Ordinal) { ["Dissolutions"] = ["Layout", "Selection"] };

    private static int Layer(string folder) =>
        Array.FindIndex(LowerLayers, layer => layer.Contains(folder, StringComparer.Ordinal)) is var index and >= 0 ? index : LowerLayers.Length;

    /// <summary>
    /// The references of the domain that break the order of its layers: a folder may refer to a folder of a lower layer,
    /// or of its own layer (Errors and Values, the folders of rules between them, kept without cycle by the test above),
    /// never to a folder of a higher layer; Blocks only to Errors, Values and Model; Dissolutions neither to Layout nor to
    /// Selection. Each breach is written "from -> to at path:line".
    /// </summary>
    internal static IReadOnlyList<string> LayerBreaches(IEnumerable<SourceFile> files)
    {
        var breaches = new List<string>();
        foreach (var file in files)
        {
            var parts = file.DomainPath.Split('/');
            if (parts.Length < 2)
                continue;
            var from = parts[0];
            foreach (var (to, where) in SemanticDetectors.FolderReferences(file).Where(reference => reference.Folder != from))
            {
                var allowed = OnlyOn.TryGetValue(from, out var only) ? only.Contains(to, StringComparer.Ordinal)
                    : Layer(to) <= Layer(from) && !(NeverOn.TryGetValue(from, out var never) && never.Contains(to, StringComparer.Ordinal));
                if (!allowed)
                    breaches.Add($"{from} -> {to} at {where}");
            }
        }
        return breaches;
    }

    /// <summary>
    /// ARC4 minor 6: the folders of the domain follow the order of architecture.md 2.4, not only an order without cycle:
    /// Values and Errors, then Model, Workspace, Structure, then Blocks and the folders of rules (positive control: the
    /// structure refers to the workspace, the dissolutions to the blocks).
    /// </summary>
    [Fact]
    public void The_folders_of_the_domain_follow_the_order_of_the_layers()
    {
        CoreSources.Require();
        var graph = SemanticDetectors.FolderGraph(SemanticSources.Domain);
        Assert.Contains("Workspace", graph.GetValueOrDefault("Structure", []));
        Assert.Contains("Blocks", graph.GetValueOrDefault("Dissolutions", []));
        var breaches = LayerBreaches(SemanticSources.Domain);
        Assert.True(breaches.Count == 0, string.Join(Environment.NewLine, breaches));
    }

    [Fact]
    public void The_cycle_detector_finds_a_cycle_through_several_folders_and_accepts_layers()
    {
        var cyclic = new Dictionary<string, HashSet<string>>
        {
            ["Workspace"] = ["Flows"], ["Flows"] = ["Structure"], ["Structure"] = ["Workspace", "Model"], ["Model"] = [],
        };
        var layered = new Dictionary<string, HashSet<string>> { ["Flows"] = ["Structure", "Model"], ["Structure"] = ["Model"], ["Model"] = [] };

        Assert.Equal(["Flows -> Structure -> Workspace -> Flows"], Cycles(cyclic));
        Assert.Empty(Cycles(layered));
    }
}
