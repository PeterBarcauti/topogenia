// DeterminismTests.cs
// Deterministic writing of the package (spec 7.2): two exports of the same state give the same entries, byte for byte.

using System.Text;
using System.Text.Json.Nodes;
using Topogenia.Domain.Model;
using Topogenia.Interchange.Json;
using Topogenia.Interchange.Packages;
using Topogenia.Interchange.Tests.Support;

namespace Topogenia.Interchange.Tests.Packages;

public sealed class DeterminismTests
{
    private static IReadOnlyList<PackageEntry> Entries(SampleProject sample, bool shuffled = false) =>
        PackageWriter.Entries(shuffled ? sample.ShuffledWorkspace() : sample.Workspace(), sample.Logins, new PackageOptions(WithoutPeople: false));

    [Fact]
    public void Two_exports_of_the_same_state_give_the_same_entries_byte_for_byte()
    {
        var sample = new SampleProject();

        var first = Entries(sample);
        var second = Entries(sample);

        Assert.Equal(first.Select(entry => entry.Path), second.Select(entry => entry.Path));
        Assert.All(first.Zip(second), pair => Assert.Equal(pair.First.Content, pair.Second.Content));
    }

    [Fact]
    public void The_order_in_which_the_records_come_never_changes_the_entries()
    {
        var sample = new SampleProject();

        Assert.All(Entries(sample).Zip(Entries(sample, shuffled: true)), pair => Assert.Equal(pair.First.Content, pair.Second.Content));
    }

    [Fact]
    public void Two_documents_of_the_same_step_and_time_are_ordered_by_their_content_whatever_their_order()
    {
        var sample = new SampleProject();
        var step = sample.Steps["E006"];
        foreach (var name in new[] { "Bulletin B", "Bulletin A" })
            sample.Add(new Document(Guid.NewGuid(), step, Domain.Values.DocumentDirection.Output, name, null, null, null, false, "fr",
                SampleProject.At(99), sample.Bruno, null, null, 1));

        var documents = JsonNode.Parse(Entries(sample).Single(entry => entry.Path == "documents.json").Content)!["documents"]!.AsArray();

        Assert.Equal(["Bulletin A", "Bulletin B"], documents.Where(document => (string?)document!["step"] == "CONGES-E006").Select(document => (string?)document!["name"]));
        Assert.All(Entries(sample).Zip(Entries(sample, shuffled: true)), pair => Assert.Equal(pair.First.Content, pair.Second.Content));
    }

    [Fact]
    public void The_scale_kept_by_an_engine_never_changes_the_bytes()
    {
        var plain = Entries(new SampleProject());
        var scaled = Entries(new SampleProject().With(record => record is StepSheet sheet && sheet.DurationMinutes == 15.50m
            ? sheet with { DurationMinutes = 15.5000m }
            : record));

        Assert.All(plain.Zip(scaled), pair => Assert.Equal(pair.First.Content, pair.Second.Content));
        Assert.Contains("\"durationMinutes\": 15.5,", Encoding.UTF8.GetString(plain.Single(entry => entry.Path == "sheets.json").Content), StringComparison.Ordinal);
    }

    [Fact]
    public void Every_object_of_every_entry_has_its_keys_in_ordinal_order()
    {
        foreach (var entry in Entries(new SampleProject()))
            AssertSorted(JsonNode.Parse(entry.Content)!, entry.Path);
    }

    [Fact]
    public void The_entries_are_utf8_without_byte_order_mark_indented_by_two_spaces_with_lf_line_ends()
    {
        foreach (var entry in Entries(new SampleProject()))
        {
            Assert.False(entry.Content.AsSpan().StartsWith(Encoding.UTF8.Preamble), entry.Path);
            var text = Encoding.UTF8.GetString(entry.Content);
            Assert.DoesNotContain('\r', text);
            Assert.EndsWith("}\n", text, StringComparison.Ordinal);
            Assert.StartsWith("{\n  \"", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void The_texts_are_written_as_they_are_with_only_the_quote_the_backslash_and_the_control_characters_escaped()
    {
        var bytes = CanonicalJson.Write(new JsonObject { ["t"] = "Équipe <RH> & \"paie\"\\\n" });

        Assert.Equal("{\n  \"t\": \"Équipe <RH> & \\\"paie\\\"\\\\\\n\"\n}\n", Encoding.UTF8.GetString(bytes));
    }

    [Fact]
    public void A_local_date_is_refused_by_the_writer()
    {
        Assert.Throws<ArgumentException>(() => CanonicalJson.Date(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Local)));
        Assert.Equal("2026-01-01T00:00:00.0000001Z", CanonicalJson.Date(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddTicks(1)));
    }

    private static void AssertSorted(JsonNode node, string place)
    {
        switch (node)
        {
            case JsonObject value:
                var keys = value.Select(property => property.Key).ToList();
                Assert.Equal(keys.Order(StringComparer.Ordinal), keys);
                foreach (var (key, child) in value)
                {
                    if (child is not null)
                        AssertSorted(child, place + "/" + key);
                }
                break;
            case JsonArray items:
                foreach (var item in items.OfType<JsonNode>())
                    AssertSorted(item, place);
                break;
        }
    }
}
