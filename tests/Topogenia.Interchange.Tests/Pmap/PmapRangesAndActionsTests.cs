// PmapRangesAndActionsTests.cs
// The bounds of the duration of a sheet and the action plan of a use in a .pmap 1.4 (Makigami M5, M7, ADR 095): read, built, copied in 1.4 and only then; unknown in 1.3; refused out of order, without a pain point, or with a due date that is not a day.

using Topogenia.Domain.Documents;
using Topogenia.Domain.Model;
using Topogenia.Domain.Sheets;
using Topogenia.Interchange.Pmap;
using static Topogenia.Interchange.Tests.Pmap.PmapBuildTests;
using static Topogenia.Interchange.Tests.Pmap.PmapReaderTests;

namespace Topogenia.Interchange.Tests.Pmap;

public sealed class PmapRangesAndActionsTests
{
    private static readonly DateTime At = new(2026, 11, 8, 8, 0, 0, DateTimeKind.Utc);

    /// <summary>The copy as the one who fills the sheets sees it: the drafts the file opened as (the bounds are in the draft).</summary>
    private static readonly PmapOutlineOptions Filler = new(true, _ => new SheetViewer(true, false));

    /// <summary>
    /// A start, a step that produces an extraction, a step that uses it with a pain point and its plan, its sheet with the
    /// bounds of its duration; of the version given, with the values given.
    /// </summary>
    private static string Planned(string version = "1.4", string bounds = "\"duration_min_minutes\": 10, \"duration_max_minutes\": 30",
        string plan = "\"pain_point\": \"Saisie en double\", \"action\": \"Lire l'extraction\", \"owner\": \"Service paie\", \"due_date\": \"2026-12-15\"") => $$"""
        {
          "format": "topogenia",
          "version": "{{version}}",
          "project": { "name": "Paie", "code": "PAI" },
          "map": {
            "lanes": [{{TwoLanes}}],
            "steps": [
              { "id": "e0", "lane": "c1", "label": "Début", "type": "start" },
              { "id": "e1", "lane": "c1", "label": "Extraire", "type": "task", "sheet": { "outputs": [{ "id": "x1", "name": "Extraction" }] } },
              { "id": "e2", "lane": "c2", "label": "Saisir", "type": "task",
                "sheet": { "duration_minutes": 15, {{bounds}}, "uses": [{ "document": "x1", {{plan}} }] } }
            ],
            "flows": [ { "source": "e0", "target": "e1" }, { "source": "e1", "target": "e2" } ]
          }
        }
        """;

    [Fact]
    public void The_bounds_and_the_plan_of_a_file_1_4_are_read_and_built()
    {
        var sheet = Read(Planned()).Map.Steps.Single(candidate => candidate.Id == "e2").Sheet!;
        Assert.Equal((10m, 30m), (sheet.DurationMinMinutes!.Value, sheet.DurationMaxMinutes!.Value));
        var use = sheet.Uses.Single();
        Assert.Equal(("Lire l'extraction", "Service paie", (DateOnly?)new DateOnly(2026, 12, 15)), (use.Action, use.Owner, use.DueDate));
        var (_, workspace) = Build(Read(Planned()));
        var built = workspace.All<StepSheet>().Single(candidate => candidate.DurationMinMinutes is not null);
        Assert.Equal((10m, 15m, 30m), (built.DurationMinMinutes!.Value, built.DurationMinutes!.Value, built.DurationMaxMinutes!.Value));
        var painPoint = workspace.All<PainPoint>().Single();
        Assert.Equal(("Lire l'extraction", "Service paie", (DateOnly?)new DateOnly(2026, 12, 15)), (painPoint.Action, painPoint.Owner, painPoint.DueDate));
    }

    [Fact]
    public void A_copy_carries_them_in_1_4_each_alone_and_a_map_without_them_stays_older()
    {
        var (_, workspace) = Build(Read(Planned()));
        string Copy() => PmapWriter.Write(PmapOutline.Of(workspace, Filler).Document, "Test", At);
        var copy = Copy();
        Assert.Contains("\"version\": \"1.4\"", copy, StringComparison.Ordinal);
        Assert.Contains("\"duration_min_minutes\": 10", copy, StringComparison.Ordinal);
        Assert.Contains("\"due_date\": \"2026-12-15\"", copy, StringComparison.Ordinal);
        var (_, again) = Build(Read(copy));
        Assert.Equal("Service paie", again.All<PainPoint>().Single().Owner);
        var painPoints = workspace.All<PainPoint>().ToList();
        foreach (var painPoint in painPoints)
            workspace.Put(painPoint with { Action = null, Owner = null, DueDate = null });
        Assert.Contains("\"version\": \"1.4\"", Copy(), StringComparison.Ordinal);
        foreach (var sheet in workspace.All<StepSheet>().ToList())
            workspace.Put(sheet with { DurationMinMinutes = null, DurationMaxMinutes = null });
        foreach (var painPoint in painPoints)
            workspace.Put(painPoint);
        Assert.Contains("\"version\": \"1.4\"", Copy(), StringComparison.Ordinal);
        foreach (var painPoint in painPoints)
            workspace.Put(painPoint with { Action = null, Owner = null, DueDate = null });
        var plain = Copy();
        Assert.DoesNotContain("\"version\": \"1.4\"", plain, StringComparison.Ordinal);
        Assert.DoesNotContain("duration_min_minutes", plain, StringComparison.Ordinal);
        Assert.DoesNotContain("\"owner\"", plain, StringComparison.Ordinal);
    }

    [Fact]
    public void They_are_unknown_in_a_file_1_3_and_refused_out_of_order_without_a_pain_point_or_with_a_due_date_that_is_not_a_day()
    {
        Assert.Contains(Faults(Planned("1.3")), fault => fault.Path.EndsWith("duration_min_minutes", StringComparison.Ordinal));
        Assert.Contains(Faults(Planned("1.3", bounds: "\"tools\": \"ERP\"")), fault => fault.Path.EndsWith(DocumentRules.ActionField, StringComparison.Ordinal));
        Assert.Contains(Faults(Planned(bounds: "\"duration_min_minutes\": 20")), fault => fault.Path.EndsWith("sheet", StringComparison.Ordinal));
        Assert.Contains(Faults(Planned(plan: "\"owner\": \"Paie\"")), fault => fault.Path.Contains("uses[0]", StringComparison.Ordinal));
        Assert.Contains(Faults(Planned(plan: "\"pain_point\": \"Relance\", \"due_date\": \"15/12/2026\"")),
            fault => fault.Path.EndsWith(DocumentRules.DueDateField, StringComparison.Ordinal));
    }
}
