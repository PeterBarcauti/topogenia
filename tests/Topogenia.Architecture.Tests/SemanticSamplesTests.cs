// SemanticSamplesTests.cs
// Proves that each detector on resolved symbols catches every way round it (using static, alias, method group, other names) and accepts correct code.

namespace Topogenia.Architecture.Tests;

public sealed class SemanticSamplesTests
{
    private const string Usings = "using Topogenia.Domain.Errors; using Topogenia.Domain.Model; using Topogenia.Domain.Selection; using Topogenia.Domain.Workspace;\n";

    private static bool Detects(Func<SourceFile, IEnumerable<string>> detector, string sample) =>
        detector(SemanticSources.Sample(Usings + sample)).Any();

    [Theory]
    [InlineData("using static System.DateTime; class A { object d = UtcNow; }")]
    [InlineData("using static System.Guid; class A { object g = NewGuid(); }")]
    [InlineData("using D = System.DateTime; class A { object d = D.Now; }")]
    [InlineData("using G = System.Guid; class A { System.Func<System.Guid> f = G.CreateVersion7; }")]
    [InlineData("class A { System.Func<Guid> f = Guid.NewGuid; }")]
    [InlineData("class A { object d = DateTime.Today; }")]
    [InlineData("using static System.DateTimeOffset; class A { object d = UtcNow; }")]
    [InlineData("class A { long t = System.Diagnostics.Stopwatch.GetTimestamp(); }")]
    [InlineData("class A { object w = System.Diagnostics.Stopwatch.StartNew(); }")]
    [InlineData("class A { int r = Random.Shared.Next(); }")]
    [InlineData("class A { object r = new Random(4); }")]
    [InlineData("class A { int t = Environment.TickCount; }")]
    [InlineData("using static System.Environment; class A { long t = TickCount64; }")]
    [InlineData("class A { object c = TimeProvider.System; }")]
    [InlineData("class Clock : TimeProvider { }")]
    [InlineData("class A { byte[] b = System.Security.Cryptography.RandomNumberGenerator.GetBytes(4); }")]
    public void Every_way_to_reach_the_clock_or_an_identifier_factory_is_detected(string sample) =>
        Assert.True(Detects(SemanticDetectors.ClockOrIdentifierFactories, sample));

    [Theory]
    [InlineData("class A { object D(OperationContext c) => c.Now; }")]
    [InlineData("class A { object G(OperationContext c) => c.NewId(); }")]
    [InlineData("class A { object d = DateTime.MinValue; object g = Guid.Empty; }")]
    [InlineData("class Random { } class A { Random r = new(); }")]
    [InlineData("class A { } // DateTime.UtcNow and Guid.NewGuid in a comment")]
    public void Values_of_the_operation_context_and_look_alike_names_are_not_the_clock(string sample) =>
        Assert.False(Detects(SemanticDetectors.ClockOrIdentifierFactories, sample));

    [Theory]
    [InlineData("using static System.DateTime; class A { object d = Now; }")]
    [InlineData("using static System.DateTime; class A { object d = Today; }")]
    [InlineData("using T = System.DateTimeOffset; class A { T d; }")]
    [InlineData("class A { object d = DateTime.UtcNow.ToLocalTime(); }")]
    [InlineData("class A { object D(TimeProvider p) => p.GetLocalNow(); }")]
    public void Local_dates_written_in_any_way_are_detected(string sample) => Assert.True(Detects(SemanticDetectors.LocalDates, sample));

    [Theory]
    [InlineData("using static System.Guid; class A { object g = NewGuid(); }")]
    [InlineData("using G = System.Guid; class A { object g = G.NewGuid(); }")]
    [InlineData("class A { System.Func<Guid> f = Guid.NewGuid; }")]
    public void Random_guids_written_in_any_way_are_detected(string sample) => Assert.True(Detects(SemanticDetectors.RandomGuids, sample));

    [Fact]
    public void A_version_seven_guid_is_not_a_random_guid() =>
        Assert.False(Detects(SemanticDetectors.RandomGuids, "class A { object g = Guid.CreateVersion7(); }"));

    [Theory]
    [InlineData("class A { ChangeSet M(ProjectWorkspace w, OperationContext c) => w.Commit(c); }")]
    [InlineData("class A { System.Func<OperationContext, ChangeSet> M(ProjectWorkspace w) => w.Commit; }")]
    [InlineData("class A { void M(ProjectWorkspace w, OperationContext c) { var commit = w.Commit; commit(c); } }")]
    public void A_commit_called_or_taken_as_a_method_group_is_detected(string sample) => Assert.True(Detects(SemanticDetectors.Commits, sample));

    [Theory]
    [InlineData("class A { IVersionedRecord M(Lane l) => l.Stamp(2, null, null); }")]
    [InlineData("class A { System.Func<int, DateTime?, Guid?, IVersionedRecord> M(IVersionedRecord r) => r.Stamp; }")]
    public void A_stamp_called_or_taken_as_a_method_group_is_detected(string sample) => Assert.True(Detects(SemanticDetectors.Stamps, sample));

    [Fact]
    public void Putting_a_record_is_neither_a_commit_nor_a_stamp()
    {
        const string sample = "class A { void M(ProjectWorkspace w, Lane l) => w.Put(l with { Name = \"x\" }); }";
        Assert.False(Detects(SemanticDetectors.Commits, sample));
        Assert.False(Detects(SemanticDetectors.Stamps, sample));
    }

    [Theory]
    [InlineData("class A { object M(Lane l) => l with { Version = l.Version + 1 }; }")]
    [InlineData("class A { object M(Lane l) => l with { UpdatedAt = DateTime.UtcNow }; }")]
    [InlineData("class A { object M(StepRaci r) => r with { UpdatedBy = Guid.Empty }; }")]
    [InlineData("class A { object r = new StepRaci(Guid.Empty, Guid.Empty, RaciRole.C, default, Guid.Empty) { Version = 3 }; }")]
    [InlineData("class A { object M(ProcessMap m) => new ProcessMap(m.Id, m.ProjectId, null, \"m\", \"fr\", default, Guid.Empty, null, null, null, null, m.Version); }")]
    [InlineData("class A { object m = new ProcessMap(Guid.Empty, Guid.Empty, null, \"m\", \"fr\", default, Guid.Empty, null, null, null, null, 4); }")]
    public void A_version_or_an_update_audit_written_by_hand_is_detected(string sample) => Assert.True(Detects(SemanticDetectors.VersionWrites, sample));

    [Theory]
    [InlineData("class A { object M(Lane l) => l with { Name = \"x\" }; }")]
    [InlineData("class A { object M(StepRaci r) => r with { LaneId = Guid.Empty, Version = 1 }; }")]
    [InlineData("class A { object m = new ProcessMap(Guid.Empty, Guid.Empty, null, \"m\", \"fr\", default, Guid.Empty, null, null, null, null, 1); }")]
    public void A_change_of_content_or_a_creation_at_version_one_is_not_a_version_written_by_hand(string sample) =>
        Assert.False(Detects(SemanticDetectors.VersionWrites, sample));

    [Theory]
    [InlineData("class A { RecordKey K(Guid id) => RecordKey.Of(\"step\", id); }")]
    [InlineData("using static Topogenia.Domain.Model.RecordKey; class A { RecordKey K(Guid id) => Of(\"raci\", id, id); }")]
    [InlineData("using K = Topogenia.Domain.Model.RecordKey; class A { System.Func<string, Guid, K> f = K.Of; }")]
    [InlineData("class A { RecordKey k = new RecordKey(\"use\", \"x\"); }")]
    [InlineData("class A { RecordKey k = new(\"translation\", \"x\"); }")]
    [InlineData("class A { RecordKey M(Step s) => s.Key with { Type = \"document\" }; }")]
    public void A_record_key_built_outside_the_model_in_any_way_is_detected(string sample) =>
        Assert.True(Detects(SemanticDetectors.RecordKeyBuilds, sample));

    [Theory]
    [InlineData("class A { RecordKey K(Guid id) => Step.KeyOf(id); }")]
    [InlineData("class A { RecordKey K(Guid a, Guid b) => DocumentUse.KeyOf(a, b); }")]
    [InlineData("class A { RecordKey K(Step s) => s.Key; }")]
    public void A_key_given_by_its_record_is_not_a_key_built_by_hand(string sample) =>
        Assert.False(Detects(SemanticDetectors.RecordKeyBuilds, sample));

    [Theory]
    [InlineData("class A { object M(OperationContext c, Step s) => Versioning.Update(c, s, s with { Label = \"x\" }); }")]
    [InlineData("using static Topogenia.Domain.Workspace.Versioning; class A { object M(OperationContext c, AppUser u) => Update(c, u, u); }")]
    [InlineData("class A { System.Func<OperationContext, Lane, Lane, RecordUpdate<Lane>> f = Versioning.Update; }")]
    public void A_versioning_update_called_or_taken_as_a_method_group_is_detected(string sample)
    {
        // The samples call the versioning of the domain: they compile against its sources.
        CoreSources.Require();
        Assert.True(Detects(SemanticDetectors.VersioningUpdates, sample));
    }

    [Fact]
    public void The_folder_graph_sees_partial_names_using_directives_using_static_and_aliases()
    {
        const string root = SemanticSources.DomainFolder;
        var files = SemanticSources.Samples(
            (root + "Alpha/A.cs", "using Topogenia.Domain.Epsilon; namespace Topogenia.Domain.Alpha; public static class A { public static int Value() => Beta.B.Of(); }"),
            (root + "Beta/B.cs", "using Topogenia.Domain.Gamma; namespace Topogenia.Domain.Beta; public static class B { public static int Of() => C.Of(); }"),
            (root + "Gamma/C.cs", "using static Topogenia.Domain.Alpha.A; namespace Topogenia.Domain.Gamma; public static class C { public static int Of() => 1; public static int Back() => Value(); }"),
            (root + "Delta/D.cs", "using Al = Topogenia.Domain.Alpha.A; namespace Topogenia.Domain.Delta; public static class D { public static int Of() => Al.Value(); }"),
            (root + "Epsilon/E.cs", "namespace Topogenia.Domain.Epsilon; public static class E { }"));
        var graph = SemanticDetectors.FolderGraph(files);

        Assert.Equal(["Beta", "Epsilon"], graph["Alpha"].Order(StringComparer.Ordinal));
        Assert.Equal(["Gamma"], graph["Beta"]);
        Assert.Equal(["Alpha"], graph["Gamma"]);
        Assert.Equal(["Alpha"], graph["Delta"]);
        Assert.Equal(["Alpha -> Beta -> Gamma -> Alpha"], DomainModelRulesTests.Cycles(graph));
    }

    [Fact]
    public void The_folder_graph_of_layers_has_no_cycle()
    {
        const string root = SemanticSources.DomainFolder;
        var files = SemanticSources.Samples(
            (root + "Alpha/A.cs", "namespace Topogenia.Domain.Alpha; public static class A { public static int Value() => Beta.B.Of(); }"),
            (root + "Beta/B.cs", "namespace Topogenia.Domain.Beta; public static class B { public static int Of() => 1; }"));
        Assert.Empty(DomainModelRulesTests.Cycles(SemanticDetectors.FolderGraph(files)));
    }

    private static bool Foreign(string sample, string path = SemanticSources.DomainFolder + "Rules/Sample.cs") =>
        DomainFrameworkDetectors.ForeignUses(SemanticSources.Sample(Usings + sample, path)).Any();

    [Theory]
    [InlineData("class A { static string R() => System.Xml.Linq.XDocument.Load(\"c:/x.xml\").ToString(); }")]
    [InlineData("class A { static bool R() => System.Xml.XmlReader.Create(\"http://example.invalid/x.xml\").Read(); }")]
    [InlineData("partial class A { [System.Runtime.InteropServices.DllImport(\"kernel32.dll\")] private static extern void GetSystemTimeAsFileTime(out long time); }")]
    [InlineData("partial class A { private static extern void GetSystemTimeAsFileTime(out long time); }")]
    [InlineData("partial class A { [System.Runtime.InteropServices.LibraryImport(\"kernel32.dll\")] private static partial void GetSystemTimeAsFileTime(out long time); }")]
    [InlineData("class A { static object? R() => Type.GetType(\"System.IO.File, System.Runtime\")!.GetMethod(\"ReadAllText\", [typeof(string)])!.Invoke(null, [\"c:/x.txt\"]); }")]
    [InlineData("class A { static object? R() => AppContext.GetData(\"x\"); }")]
    [InlineData("class A { static object? R() => Activator.CreateInstance(Type.GetType(\"System.Random\")!); }")]
    [InlineData("class A { static int R(string t) => HashCode.Combine(t); }")]
    [InlineData("class A { static int R(string t) => t.GetHashCode(); }")]
    [InlineData("class A { static object? R() => typeof(DateTime).GetProperty(\"UtcNow\")!.GetValue(null); }")]
    [InlineData("class A { static string? R() => Environment.GetEnvironmentVariable(\"X\"); }")]
    [InlineData("class A { static int R(ref int x) => Interlocked.Increment(ref x); }")]
    [InlineData("class A { static object R() => new System.Net.Http.HttpClient(); }")]
    [InlineData("class A { static string R() => File.ReadAllText(\"x\"); }")]
    [InlineData("class A { static void R() => Console.WriteLine(\"x\"); }")]
    [InlineData("class A { static void R() => GC.Collect(); }")]
    [InlineData("class A { static object R() => TimeZoneInfo.Local; }")]
    [InlineData("class A { static int R(object o) { var t = o.GetType(); return t.Name.Length; } }")]
    [InlineData("class A { static int R(dynamic d) => d.Length; }")]
    [InlineData("unsafe class A { static int* P; }")]
    [InlineData("class A { static string R() => System.Text.Json.JsonSerializer.Serialize(1); }")]
    [InlineData("class A { static object R() => Task.Run(() => 1); }")]
    public void Every_input_output_reflection_interop_or_process_use_in_the_domain_is_detected(string sample) => Assert.True(Foreign(sample));

    [Theory]
    [InlineData("class A { static object R(string[] t) => t.Where(x => x.Length > 1).OrderBy(x => x, StringComparer.Ordinal).ToList(); }")]
    [InlineData("class A { static bool R(string t) => System.Text.RegularExpressions.Regex.IsMatch(t, \"^a$\") && t.ToString(System.Globalization.CultureInfo.InvariantCulture).Length > 0; }")]
    [InlineData("class A { static string R() => new System.Text.StringBuilder().Append(Math.Round(1.5m, MidpointRounding.AwayFromZero)).ToString(); }")]
    [InlineData("sealed class A { public string X { get; } = \"\"; public override bool Equals(object? o) => o is A a && a.X == X; public override int GetHashCode() => HashCode.Combine(X, X.GetHashCode()); }")]
    [InlineData("class A { static Dictionary<Type, int> R(object o) => new() { [typeof(A)] = 1, [o.GetType()] = 2 }; }")]
    [InlineData("class A { static bool R(string t) => Uri.TryCreate(t, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps; }")]
    [InlineData("class A { static (int Rank, Guid Id) R(Step s) => (1, s.Id); }")]
    public void Pure_computation_on_values_texts_and_collections_is_allowed(string sample) => Assert.False(Foreign(sample));

    [Fact]
    public void The_exceptions_of_the_allow_list_hold_only_in_their_file_and_only_for_their_members()
    {
        const string domain = SemanticSources.DomainFolder;
        const string constants = "class A { const string X = \"x\"; static object R() => typeof(A).GetFields().Where(f => f.IsLiteral).Select(f => f.GetRawConstantValue()).ToList(); }";
        // The domain reads no type, ErrorCodes.cs included: its lists are read by the tests (porte P7 round 1).
        Assert.True(Foreign(constants, domain + "Errors/ErrorCodes.cs"));
        Assert.True(Foreign(constants));
        Assert.True(Foreign("class A { static object R() => typeof(A).GetMethods(); }", domain + "Errors/ErrorCodes.cs"));
        const string json = "class A { static int R() { using var d = System.Text.Json.JsonDocument.Parse(\"{}\"); return d.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object ? 1 : 0; } }";
        Assert.False(Foreign(json, domain + "Sheets/SheetJson.cs"));
        Assert.True(Foreign(json));
        Assert.True(Foreign("class A { static string R() => System.Text.Json.JsonSerializer.Serialize(1); }", domain + "Sheets/SheetJson.cs"));
    }

    [Theory]
    [InlineData("Blocks/B.cs", "namespace Topogenia.Domain.Blocks; static class B { static object W(Topogenia.Domain.Workspace.ProjectWorkspace w) => w; }", "Blocks -> Workspace")]
    [InlineData("Blocks/B.cs", "namespace Topogenia.Domain.Blocks; static class B { static object W() => typeof(Topogenia.Domain.Structure.GridRules); }", "Blocks -> Structure")]
    [InlineData("Model/M.cs", "namespace Topogenia.Domain.Model; static class M { static object W(Topogenia.Domain.Workspace.ProjectWorkspace w) => w; }", "Model -> Workspace")]
    [InlineData("Workspace/W.cs", "namespace Topogenia.Domain.Workspace; static class W { static object S() => typeof(Topogenia.Domain.Structure.StructureEdits); }", "Workspace -> Structure")]
    [InlineData("Values/V.cs", "namespace Topogenia.Domain.Values; static class V { static object S(Topogenia.Domain.Model.Step s) => s; }", "Values -> Model")]
    [InlineData("Structure/S.cs", "namespace Topogenia.Domain.Structure; static class S { static object B() => typeof(Topogenia.Domain.Blocks.BlockEnds); }", "Structure -> Blocks")]
    [InlineData("Dissolutions/D.cs", "namespace Topogenia.Domain.Dissolutions; static class D { static object S(Topogenia.Domain.Selection.FlowEnds e) => e; }", "Dissolutions -> Selection")]
    public void A_reference_against_the_order_of_the_layers_is_detected(string path, string text, string breach)
    {
        var breaches = DomainModelRulesTests.LayerBreaches(SemanticSources.Samples((SemanticSources.DomainFolder + path, text)));
        Assert.Contains(breaches, found => found.StartsWith(breach + " at ", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Structure/S.cs", "namespace Topogenia.Domain.Structure; static class S { static object W(Topogenia.Domain.Workspace.ProjectWorkspace w) => w; }")]
    [InlineData("Layout/L.cs", "namespace Topogenia.Domain.Layout; static class L { static object F() => typeof(Topogenia.Domain.Dissolutions.Frames); }")]
    [InlineData("Selection/S.cs", "namespace Topogenia.Domain.Selection; static class S { static object B() => typeof(Topogenia.Domain.Blocks.BlockEnds); }")]
    [InlineData("Blocks/B.cs", "namespace Topogenia.Domain.Blocks; static class B { static object F(Topogenia.Domain.Model.Flow f) => f.Id; }")]
    [InlineData("Values/V.cs", "namespace Topogenia.Domain.Values; static class V { static string C() => Topogenia.Domain.Errors.ErrorCodes.InvalidValue; }")]
    public void A_reference_down_the_layers_is_allowed(string path, string text) =>
        Assert.Empty(DomainModelRulesTests.LayerBreaches(SemanticSources.Samples((SemanticSources.DomainFolder + path, text))));
}
