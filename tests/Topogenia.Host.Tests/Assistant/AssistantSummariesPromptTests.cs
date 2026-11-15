// AssistantSummariesPromptTests.cs
// The prompt of the summaries of the maps (plan 3.1, lot R): its instruction, the maps listed by their reference with the summary they replace, the form and the field of the reply in its schema.

using Topogenia.Application.Assistant;
using Topogenia.Assistant;

namespace Topogenia.Host.Tests.Assistant;

public sealed class AssistantSummariesPromptTests
{
    [Fact]
    public void The_prompt_of_the_summaries_lists_the_maps_by_their_reference_with_the_summary_they_replace()
    {
        var maps = AssistantInstructions.MapsOf([new(Guid.NewGuid(), "VIS", "Visites", null, 0, false),
            new(Guid.NewGuid(), "VIS-E004", "Traiter\nla commande", "Ancienne phrase.", 3, true)]);
        Assert.Equal("<maps>\nVIS | Visites\nVIS-E004 | Traiter la commande | its summary today, to write again: Ancienne phrase.\n</maps>", maps);
        Assert.Contains("summaries", ReplySchema.Forms);
        Assert.Contains("\"summaries\"", ReplySchema.For([AssistantForm.Summaries], false), StringComparison.Ordinal);
        Assert.Contains("<maps>", AssistantInstructions.PracticeOf(AssistantIntent.Summaries), StringComparison.Ordinal);
    }
}
