// AssistantSummariesTests.cs
// The summaries of the maps written with the model (plan 3.1, lot R): the maps that may be asked and the ones asked, the sentences of the reply read, bounded on one line and matched to the maps by their reference.

using Topogenia.Application.Assistant;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Structure;

namespace Topogenia.Application.Tests.Assistant;

public sealed class AssistantSummariesTests
{
    private static readonly Guid Root = Guid.Parse("00000000-0000-0000-0000-000000000a01");
    private static readonly Guid Detail = Guid.Parse("00000000-0000-0000-0000-000000000d01");
    private static readonly Guid Other = Guid.Parse("00000000-0000-0000-0000-000000000e01");

    private static readonly AssistantSummaryTarget[] Targets =
    [
        new(Root, "VIS", "Visites", null, 0, false),
        new(Detail, "VIS-E004", "Traiter la commande", "Ancienne phrase.", 3, true),
        new(Other, "VIS-E007", "Livrer", "Le magasin livre.", 2, false),
    ];

    private static AssistantReply Read(string text) => AssistantReplyReader.Read(text, out var reason) ?? throw new InvalidOperationException(reason);

    [Fact]
    public void The_maps_asked_are_those_without_a_summary_or_stale_or_the_ones_named_and_none_left_is_refused()
    {
        Assert.Equal([Root, Detail], AssistantSummaryTargets.Asked(Targets, null).Select(target => target.MapId));
        Assert.Equal([Other], AssistantSummaryTargets.Asked(Targets, [Other, Other]).Select(target => target.MapId));
        Assert.Equal(ErrorCodes.NotFound, Assert.Throws<DomainException>(() => AssistantSummaryTargets.Asked(Targets, [Guid.NewGuid()])).Code);
        Assert.Equal(ErrorCodes.SummariesUpToDate, Assert.Throws<DomainException>(() => AssistantSummaryTargets.Asked([Targets[2]], [])).Code);
        var many = Enumerable.Range(0, AssistantSummaryTargets.MapsMax + 5).Select(index => new AssistantSummaryTarget(Guid.NewGuid(), $"R{index}", "n", null, 0, false)).ToList();
        Assert.Equal(AssistantSummaryTargets.MapsMax, AssistantSummaryTargets.Asked(many, null).Count);
    }

    [Fact]
    public void The_sentences_are_read_matched_to_the_maps_by_reference_each_map_once_an_unknown_one_left_aside()
    {
        var reply = Read("""
            {"form":"summaries","message":"Trois cartes.","cited":[],"questions":[],"findings":[],"pmap":null,"report":null,
             "summaries":[{"map":"VIS-E004","text":"  La comptabilité\nfacture la commande.  "},{"map":"VIS","text":"Le commercial vend."},
                          {"map":"VIS-E004","text":"Une seconde phrase."},{"map":"VIS-E999","text":"Inconnue."},{"map":"VIS-E007","text":"   "},{"text":"Sans carte."}]}
            """);
        Assert.Equal(Domain.Values.Codes.Of(AssistantForm.Summaries), Domain.Values.Codes.Of(reply.Form));
        var views = AssistantSummariesReader.Views(reply.Summaries!, Targets);
        Assert.Equal([(Detail, "La comptabilité facture la commande.", "Ancienne phrase.", 3), (Root, "Le commercial vend.", (string?)null, 0)],
            views.Select(view => (view.MapId, view.Text, view.Current, view.Version)));
        Assert.Equal(("VIS-E004", "Traiter la commande"), (views[0].Reference, views[0].Name));
    }

    [Fact]
    public void A_sentence_too_long_is_cut_at_a_word_a_tag_is_spaced_and_a_form_without_a_sentence_is_refused()
    {
        var words = string.Join(' ', Enumerable.Repeat("processus", 40));
        var cut = AssistantSummariesReader.Bounded(words)!;
        Assert.InRange(cut.Length, StructureEdits.MapSummaryMax / 2, StructureEdits.MapSummaryMax);
        Assert.EndsWith("processus…", cut, StringComparison.Ordinal);
        Assert.Equal("Le < b>gras< /b> et 3 <5 jours", AssistantSummariesReader.Bounded("Le <b>gras</b> et 3 <5 jours"));
        Assert.Null(AssistantSummariesReader.Bounded(" \t "));
        Assert.Null(AssistantReplyReader.Read("""{"form":"summaries","message":"x","cited":[],"questions":[],"findings":[],"pmap":null,"report":null,"summaries":[]}""", out var reason));
        Assert.Equal("summaries without summary", reason);
    }
}
