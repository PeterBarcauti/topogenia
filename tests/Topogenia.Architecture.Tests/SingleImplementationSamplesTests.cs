// SingleImplementationSamplesTests.cs
// Proves that each single implementation detector catches the copies written another way (loop, recursion, names, literals) and accepts correct code.

namespace Topogenia.Architecture.Tests;

public sealed class SingleImplementationSamplesTests
{
    private const string Usings =
        "using Topogenia.Domain.Errors; using Topogenia.Domain.Model; using Topogenia.Domain.Selection; using Topogenia.Domain.Workspace;\n";

    private static bool Detects(Func<SourceFile, IEnumerable<string>> detector, string sample) =>
        detector(SemanticSources.Sample(Usings + sample)).Any();

    [Theory]
    [InlineData("class A { static int D(ProjectWorkspace w, Guid id) { var n = 1; var map = w.Map(id); while (map?.ParentStepId is { } p && w.Step(p) is { } s) { n++; map = w.Map(s.MapId); } return n; } }")]
    [InlineData("class A { static int D(ProjectWorkspace w, Guid id) => w.Map(id) is { ParentStepId: { } p } && w.Step(p) is { } s ? 1 + D(w, s.MapId) : 1; }")]
    [InlineData("class A { static int D(ProjectWorkspace w, Guid id) { int Up(Guid m) => w.Map(m)?.ParentStepId is { } p ? 1 + Up(w.Step(p)!.MapId) : 1; return Up(id); } }")]
    [InlineData("class A { static int D(ProjectWorkspace w, Guid id) { var n = 1; for (var m = w.Map(id); m?.ParentStepId is { } p; m = w.Map(w.Step(p)!.MapId)) n++; return n; } }")]
    [InlineData("class A { static void D(ProjectWorkspace w, Guid id) { var all = new Stack<Guid>([id]); while (all.TryPop(out var m)) if (w.Map(m)?.ParentStepId is { } p && w.Step(p) is { } s) all.Push(s.MapId); } }")]
    public void A_walk_up_the_sub_maps_by_loop_recursion_or_work_list_is_detected(string sample) =>
        Assert.True(Detects(SingleImplementationDetectors.WalkUp, sample));

    [Theory]
    [InlineData("class A { static Guid? Up(ProjectWorkspace w, Guid id) => w.Map(id)?.ParentStepId is { } p ? w.Step(p)?.MapId : null; }")]
    [InlineData("class A { static int D(ProjectWorkspace w, List<ProcessMap> maps) { var n = 0; while (maps.Count > 0) { var parents = maps.Select(m => m.ParentStepId is { } p ? w.Step(p)?.MapId : null).ToHashSet(); n++; maps.RemoveAll(m => !parents.Contains(m.Id)); } return n; } }")]
    public void One_level_up_or_a_peeling_of_leaves_is_not_a_walk_up(string sample) =>
        Assert.False(Detects(SingleImplementationDetectors.WalkUp, sample));

    [Theory]
    [InlineData("class A { static List<Guid> D(ProjectWorkspace w, Guid id) { var all = new List<Guid> { id }; foreach (var s in w.StepsOf(id)) if (w.SubMapOf(s.Id) is { } sub) all.AddRange(D(w, sub.Id)); return all; } }")]
    [InlineData("class A { static void D(ProjectWorkspace w, Guid id) { var q = new Queue<Guid>([id]); while (q.TryDequeue(out var m)) foreach (var s in w.StepsOf(m)) if (w.SubMapOf(s.Id) is { } sub) q.Enqueue(sub.Id); } }")]
    [InlineData("class A { static object D(ProjectWorkspace w, Guid[] ids) => ids.Select(w.SubMapOf).OfType<ProcessMap>().SelectMany(m => w.Subtree(m.Id)).ToList(); }")]
    public void A_walk_down_the_sub_maps_by_hand_is_detected(string sample) =>
        Assert.True(Detects(SingleImplementationDetectors.WalkDown, sample));

    [Fact]
    public void The_sub_maps_of_a_list_of_steps_one_level_down_are_not_a_walk_down() =>
        Assert.False(Detects(SingleImplementationDetectors.WalkDown,
            "class A { static int D(ProjectWorkspace w, Guid[] ids) { var n = 0; foreach (var id in ids) if (w.SubMapOf(id) is not null) n++; return n; } }"));

    [Theory]
    [InlineData("class A { static object E(Guid[] steps, Flow[] flows) => steps.Where(s => !flows.Any(f => f.TargetStepId == s)).ToList(); }")]
    [InlineData("class A { static object E(Guid[] steps, List<(Guid From, Guid To)> links) => steps.Where(s => !links.Exists(l => l.To == s)).ToList(); }")]
    [InlineData("class A { static object E(Guid[] steps, Flow[] flows) => steps.Where(s => flows.All(f => f.SourceStepId != s)).ToList(); }")]
    [InlineData("class A { static object E(Guid[] steps, FlowEnds[] flows) => steps.Where(s => !flows.Any(f => s == f.Target)).ToList(); }")]
    public void The_entries_or_exits_of_a_block_written_again_are_detected(string sample) =>
        Assert.True(Detects(SingleImplementationDetectors.BlockEnds, sample));

    [Theory]
    [InlineData("class A { static object I(Guid[] steps, Flow[] flows) => steps.Where(s => !flows.Any(f => f.SourceStepId == s || f.TargetStepId == s)).ToList(); }")]
    [InlineData("class A { static bool R(Guid s, Flow[] flows) => flows.Any(f => f.TargetStepId == s); }")]
    public void An_isolated_step_or_a_step_that_receives_a_flow_is_not_an_end_of_a_block(string sample) =>
        Assert.False(Detects(SingleImplementationDetectors.BlockEnds, sample));

    [Theory]
    [InlineData("class A { static object O(Lane[] lanes) => lanes.OrderBy(x => x.SortOrder).ToList(); }")]
    [InlineData("class A { static object O(Lane[] lanes) => lanes.OrderByDescending(c => c.SortOrder).ThenBy(c => c.Id).ToList(); }")]
    [InlineData("class A { static object O(ProjectWorkspace w, Step[] steps) => steps.OrderBy(s => s.Column).ThenBy(s => w.Lane(s.LaneId)!.SortOrder).ToList(); }")]
    [InlineData("class A { static void O(List<Lane> lanes) => lanes.Sort((a, b) => a.SortOrder.CompareTo(b.SortOrder)); }")]
    [InlineData("class A { static object O(Lane[] lanes) => lanes.MinBy(l => l.SortOrder)!; }")]
    [InlineData("class A { static object O() => Comparer<Lane>.Create((a, b) => a.SortOrder - b.SortOrder); }")]
    public void An_order_of_lanes_written_again_is_detected_whatever_the_variable(string sample) =>
        Assert.True(Detects(SingleImplementationDetectors.LaneOrder, sample));

    [Theory]
    [InlineData("class A { static int O(Lane[] lanes) => lanes.Max(l => l.SortOrder) + 1; }")]
    [InlineData("class A { static object O(Lane[] lanes) => lanes.Where(l => l.SortOrder >= 2).ToList(); }")]
    [InlineData("class A { static object O(Lane[] lanes) => ProjectWorkspace.InLaneOrder(lanes).ToList(); }")]
    public void A_bound_a_filter_or_the_single_lane_order_is_not_an_order_written_again(string sample) =>
        Assert.False(Detects(SingleImplementationDetectors.LaneOrder, sample));

    [Theory]
    [InlineData("class A { static object O(Document[] documents) => documents.OrderBy(d => d.CreatedAt).ToList(); }")]
    [InlineData("class A { static object O((int Column, Document Document)[] pairs) => pairs.OrderBy(p => p.Column).ThenBy(p => p.Document.CreatedAt).ToList(); }")]
    public void An_entry_order_of_documents_written_again_is_detected(string sample) =>
        Assert.True(Detects(SingleImplementationDetectors.DocumentEntryOrder, sample));

    [Theory]
    [InlineData("class A { static RecordKey K(Guid id) => RecordKey.Of(\"document\", id); }")]
    [InlineData("class A { const string Kind = \"docu\" + \"ment\"; static RecordKey K(Guid id) => RecordKey.Of(Kind, id); }")]
    public void A_key_of_a_document_written_again_is_detected(string sample) => Assert.True(Detects(SingleImplementationDetectors.DocumentKey, sample));

    [Fact]
    public void The_key_of_another_record_is_not_a_key_of_a_document() =>
        Assert.False(Detects(SingleImplementationDetectors.DocumentKey, "class A { static RecordKey K(Guid id) => RecordKey.Of(\"step\", id); }"));

    [Theory]
    [InlineData("class A { static object S(ProjectWorkspace w, Guid id) => w.All<StepSheet>().FirstOrDefault(s => s.StepId == id)!; }")]
    [InlineData("class A { static object S(ProjectWorkspace w, Guid id) => w.All<StepSheet>().Where(s => s.StepId == id).SingleOrDefault()!; }")]
    [InlineData("class A { static object S(ProjectWorkspace w, Guid id) => Enumerable.First(w.All<StepSheet>(), s => s.StepId == id); }")]
    public void A_lookup_of_the_sheet_of_a_step_written_again_is_detected(string sample) =>
        Assert.True(Detects(SingleImplementationDetectors.SheetLookup, sample));

    [Fact]
    public void The_sheets_of_several_steps_are_not_the_lookup_of_one_sheet() =>
        Assert.False(Detects(SingleImplementationDetectors.SheetLookup,
            "class A { static object S(ProjectWorkspace w, Guid[] ids) => w.All<StepSheet>().Where(s => ids.Contains(s.StepId)).ToList(); }"));

    [Theory]
    [InlineData("class A { static string C() => ErrorCodes.MacroChoiceRequired; }")]
    [InlineData("using static Topogenia.Domain.Errors.ErrorCodes; class A { static string C() => MacroChoiceRequired; }")]
    public void The_refusal_of_rg_c09_used_elsewhere_is_detected(string sample) =>
        Assert.True(Detects(SingleImplementationDetectors.MacroChoiceRefusal, sample));

    [Theory]
    [InlineData("class A { static string C() => \"MACRO_CHOIX_REQUIS\"; }")]
    [InlineData("class A { const string C = \"MACRO_CHOIX\" + \"_REQUIS\"; }")]
    [InlineData("class A { static bool C(string code) => code == \"VALEUR_INVALIDE\"; }")]
    [InlineData("class A { static bool C(string code) => code is \"TEXTE_TROP_LONG\"; }")]
    [InlineData("class A { const string Prefix = \"LIEN\"; const string C = $\"{Prefix}_DOUBLON\"; }")]
    [InlineData("class A { static string W() => \"PROFONDEUR_DEPASSEE\"; }")]
    public void An_error_or_warning_code_written_as_a_literal_is_detected(string sample) =>
        Assert.True(Detects(SingleImplementationDetectors.CodeLiterals, sample));

    [Fact]
    public void A_code_read_from_its_constant_and_an_ordinary_text_are_not_literals_of_codes() =>
        Assert.False(Detects(SingleImplementationDetectors.CodeLiterals,
            "class A { static string C() => ErrorCodes.MacroChoiceRequired; const string Field = \"label\"; }"));

    [Theory]
    [InlineData("class A { static object B(ProjectWorkspace w, Flow[] flows) => flows.Where(f => w.Step(f.SourceStepId) is { DeletedAt: null } s && s.MapId == f.MapId).ToList(); }")]
    [InlineData("class A { static bool O(Step s, Flow f) => s.MapId != f.MapId; }")]
    [InlineData("class A { static bool O(ProjectWorkspace w, Flow f, Guid map) { var target = w.LiveStep(f.TargetStepId); return target.MapId == map; } }")]
    [InlineData("class A { static string O() => ErrorCodes.FlowOutsideMap; }")]
    public void Rg_c05_written_again_is_detected(string sample) => Assert.True(Detects(SingleImplementationDetectors.FlowLinkRule, sample));

    [Theory]
    [InlineData("class A { static bool L(ProjectWorkspace w, Flow f) => FlowLinks.LinksLiveStepsOfItsMap(w, f); }")]
    [InlineData("class A { static string? L(ProjectWorkspace w, Flow f) => w.Step(f.SourceStepId)?.SourceLanguage; }")]
    public void A_call_of_flow_links_or_another_read_of_the_ends_is_not_rg_c05_written_again(string sample) =>
        Assert.False(Detects(SingleImplementationDetectors.FlowLinkRule, sample));

    [Theory]
    [InlineData("class A { static bool S(string language, Step step) => string.Equals(language, step.SourceLanguage, StringComparison.Ordinal); }")]
    [InlineData("class A { static bool S(string lang, Translation t) => t.Language.Equals(lang, StringComparison.Ordinal); }")]
    [InlineData("class A { static object S(string[] codes, Lane lane) => codes.Where(code => code != lane.SourceLanguage).ToList(); }")]
    public void The_language_of_origin_compared_again_is_detected_whatever_the_other_name(string sample) =>
        Assert.True(Detects(SingleImplementationDetectors.OriginLanguage, sample));

    [Theory]
    [InlineData("class A { static bool B(ProcessMap map, Step step) => string.Equals(map.Name, step.Label, StringComparison.Ordinal); }")]
    [InlineData("class A { static bool B(ProcessMap m, string label) => label == m.Name; }")]
    public void A_map_that_bears_a_text_compared_again_is_detected(string sample) =>
        Assert.True(Detects(SingleImplementationDetectors.MapBearsText, sample));

    [Fact]
    public void A_length_of_a_map_name_is_not_a_map_that_bears_a_text() =>
        Assert.False(Detects(SingleImplementationDetectors.MapBearsText, "class A { static bool B(ProcessMap m) => m.Name.Length > 3; }"));

    private static IReadOnlyList<MemberPlace> Places(Func<SourceFile, IEnumerable<MemberPlace>> detector, string sample) =>
        detector(SemanticSources.Sample(Usings + sample)).ToList();

    [Theory]
    [InlineData("class A { static Guid? Root(ProjectWorkspace w, Guid id) => Enumerable.Range(0, 50).Aggregate((Guid?)id, (m, _) => m is { } x && w.Map(x)?.ParentStepId is { } p ? w.Step(p)?.MapId ?? m : m); }")]
    [InlineData("class A { static int Root(ProjectWorkspace w, Guid id) { var c = new[] { id }; var n = 1; while (n < 50 && w.Map(c[0])?.ParentStepId is { } p && w.Step(p) is { } s) { c[0] = s.MapId; n++; } return n; } }")]
    [InlineData("class A { static int Root(ProjectWorkspace w, Guid id) => w.Map(id)?.ParentStepId is { } p ? 1 + B.Up(w, p) : 1; } static class B { public static int Up(ProjectWorkspace w, Guid s) => w.Step(s) is { } x ? A2.Root(w, x.MapId) : 0; } static class A2 { public static int Root(ProjectWorkspace w, Guid id) => 0; }")]
    [InlineData("class A { static object Root(ProjectWorkspace w, Guid id) => w.All<ProcessMap>().Where(m => m.ParentStepId is { } p && w.Step(p) is { } s && s.MapId == id).ToList(); }")]
    [InlineData("class A { static bool Root(ProcessMap m) => m is { ParentStepId: null }; }")]
    public void Any_read_of_the_parent_step_of_a_map_is_found_with_its_member(string sample)
    {
        var places = Places(SingleReadDetectors.ParentStepReads, sample);
        Assert.NotEmpty(places);
        Assert.Contains(places, place => place.Owner == "Rules/Sample.cs:Root");
    }

    [Fact]
    public void A_write_of_the_parent_step_of_a_map_is_not_a_read() =>
        Assert.Empty(Places(SingleReadDetectors.ParentStepReads, "class A { static ProcessMap Root(ProcessMap m, Guid s) => m with { ParentStepId = s }; }"));

    [Theory]
    [InlineData("sealed class C : IComparer<Lane> { public int Compare(Lane? x, Lane? y) => Math.Sign((x?.SortOrder ?? 0) - (y?.SortOrder ?? 0)); }")]
    [InlineData("class A { static object O(Lane[] lanes) => lanes.GroupBy(l => l.SortOrder).OrderBy(g => g.Key).SelectMany(g => g).ToList(); }")]
    [InlineData("class A { static Lane? O(Lane[] lanes, Lane lane) => lanes.FirstOrDefault(other => other.SortOrder == lane.SortOrder + 1); }")]
    [InlineData("class A { static int O(Lane lane) => lane is { SortOrder: var n } ? n : 0; }")]
    public void Any_read_of_the_number_of_a_lane_is_found(string sample) => Assert.NotEmpty(Places(SingleReadDetectors.LaneSortOrderReads, sample));

    [Fact]
    public void A_renumbering_write_of_a_lane_is_not_a_read() =>
        Assert.Empty(Places(SingleReadDetectors.LaneSortOrderReads, "class A { static Lane O(Lane l, int i) => l with { SortOrder = i }; }"));

    [Theory]
    [InlineData("class A { static object S(ProjectWorkspace w, Guid id) => w.All<StepSheet>().Where(s => s.StepId == id).ElementAtOrDefault(0)!; }")]
    [InlineData("class A { static StepSheet? S(ProjectWorkspace w, Guid id) { foreach (var s in w.All<StepSheet>()) { if (s.StepId == id) return s; } return null; } }")]
    [InlineData("class A { static StepSheet S(List<StepSheet> sheets) => sheets[0]; }")]
    [InlineData("class A { static StepSheet S(StepSheet[] sheets) => sheets[^1]; }")]
    public void Any_sequence_of_sheets_is_found(string sample) => Assert.NotEmpty(Places(SingleReadDetectors.SheetSequences, sample));

    [Fact]
    public void The_sheet_of_a_step_through_its_query_is_not_a_sequence_of_sheets() =>
        Assert.Empty(Places(SingleReadDetectors.SheetSequences,
            "class A { static StepSheet? S(ProjectWorkspace w, Guid id) => Topogenia.Domain.Sheets.SheetQueries.SheetOf(w, id); }"));

    [Theory]
    [InlineData("class A { static object E(Guid[] steps, List<Flow> flows) => steps.Where(s => flows.Count(f => f.TargetStepId == s) == 0).ToList(); }")]
    [InlineData("class A { static object E(Guid[] steps, List<Flow> flows) => steps.Where(s => !flows.Select(f => f.SourceStepId).Contains(s)).ToList(); }")]
    [InlineData("class A { static object E(Guid[] steps, List<Flow> flows) { var reached = flows.Select(f => f.TargetStepId).ToHashSet(); return steps.Where(s => !reached.Contains(s)).ToList(); } }")]
    [InlineData("class A { static object E(Guid[] steps, List<FlowEnds> flows) => steps.Except(flows.Select(f => f.Source)).ToList(); }")]
    public void The_ends_of_a_block_by_a_count_a_contains_or_an_except_are_detected(string sample) =>
        Assert.True(Detects(SingleImplementationDetectors.BlockEnds, sample));

    [Theory]
    [InlineData("class A { static object E(Guid[] steps, List<Flow> flows) => steps.Where(s => flows.Count(f => f.TargetStepId == s) > 1).ToList(); }")]
    [InlineData("class A { static object E(Guid[] steps, List<Flow> flows) => steps.Where(s => flows.Select(f => f.SourceStepId).Contains(s)).ToList(); }")]
    [InlineData("class A { static object E(Guid[] steps, List<Flow> flows) => steps.Except(flows.SelectMany(f => new[] { f.SourceStepId, f.TargetStepId })).ToList(); }")]
    public void A_count_of_several_a_positive_contains_or_both_ends_are_not_ends_of_a_block(string sample) =>
        Assert.False(Detects(SingleImplementationDetectors.BlockEnds, sample));

    [Theory]
    [InlineData("class A { static bool O(ProjectWorkspace w, FlowEnds ends, Guid map) => w.Step(ends.Source)?.MapId == map; }")]
    [InlineData("class A { static bool O(ProjectWorkspace w, Flow f) => w.All<Step>().Single(x => x.Id == f.SourceStepId).MapId == w.All<Step>().Single(x => x.Id == f.TargetStepId).MapId; }")]
    [InlineData("class A { static bool O(ProjectWorkspace w, Flow f, Guid map) => w.Step(f.TargetStepId) is { MapId: var m } && m == map; }")]
    [InlineData("class A { static Guid O(ProjectWorkspace w, Flow f) { var id = f.SourceStepId; var step = w.LiveStep(id); return step.MapId; } }")]
    public void Rg_c05_through_the_ends_of_a_selection_or_a_lookup_is_detected(string sample) =>
        Assert.True(Detects(SingleImplementationDetectors.FlowLinkRule, sample));

    [Theory]
    [InlineData("class A { static bool S(Lane l, string code) => StringComparer.Ordinal.Equals(l.SourceLanguage, code); }")]
    [InlineData("class A { static bool S(Lane l, string code) { var origin = l.SourceLanguage; return code != origin; } }")]
    [InlineData("class A { static bool S(Lane[] lanes, string code) => lanes.Select(x => x.SourceLanguage).Contains(code); }")]
    [InlineData("class A { static bool S(Step s, string code) => string.Compare(code, s.SourceLanguage, StringComparison.Ordinal) == 0; }")]
    [InlineData("class A { static bool S(Topogenia.Domain.Values.LocalizedText text, string other) { var v = text.Language; return StringComparer.Ordinal.Equals(v, other); } }")]
    [InlineData("class A { static bool S(Project p) => p.SourceLanguage is \"fr\"; }")]
    public void The_language_of_origin_compared_through_a_comparer_a_local_or_contains_is_detected(string sample) =>
        Assert.True(Detects(SingleImplementationDetectors.OriginLanguage, sample));

    [Theory]
    [InlineData("class A { static bool S(IReadOnlyList<string> languages, string code) => languages.Contains(code, StringComparer.Ordinal); }")]
    [InlineData("class A { static bool S(Dictionary<string, string> t, Topogenia.Domain.Translations.TranslatableText x, string o) { var original = t.GetValueOrDefault(x.Language); return string.Equals(original, o, StringComparison.Ordinal); } }")]
    public void A_supported_language_or_a_text_found_by_its_language_is_not_the_language_of_origin(string sample) =>
        Assert.False(Detects(SingleImplementationDetectors.OriginLanguage, sample));

    [Theory]
    [InlineData("class A { static bool B(IEnumerable<ProcessMap> maps, string label) => maps.Select(m => m.Name).Contains(label); }")]
    [InlineData("class A { static bool B(ProcessMap map, string label) { var n = map.Name; return n == label; } }")]
    [InlineData("class A { static bool B(ProcessMap map, string label) => StringComparer.Ordinal.Equals(map.Name, label); }")]
    public void A_map_that_bears_a_text_through_contains_a_local_or_a_comparer_is_detected(string sample) =>
        Assert.True(Detects(SingleImplementationDetectors.MapBearsText, sample));

    [Theory]
    [InlineData("class A { static string C() => string.Concat(\"INTRO\", \"UVABLE\"); }")]
    [InlineData("class A { static string C() => string.Join(\"_\", \"LIEN\", \"DOUBLON\"); }")]
    public void A_code_assembled_from_constants_by_concat_or_join_is_detected(string sample) =>
        Assert.True(Detects(SingleImplementationDetectors.CodeLiterals, sample));

    [Fact]
    public void A_concatenation_of_a_constant_and_a_variable_is_not_a_constant_code() =>
        Assert.False(Detects(SingleImplementationDetectors.CodeLiterals, "class A { static string C(string x) => string.Concat(\"INTRO\", x); }"));
}
