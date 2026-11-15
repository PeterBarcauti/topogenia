// AssistantFlowCommentsPromptTests.cs
// The prompt of a translation that asks the comments of presentation of flows (ADR 099, lot J10): listed after those of the steps in the same list, each flow named by the codes of its two steps; the schema of the reply is the one of the comments of the steps, so that no other text goes to the providers.

using System.Text.Json;
using Topogenia.Application.Assistant;
using Topogenia.Application.Ports;
using Topogenia.Assistant;

namespace Topogenia.Host.Tests.Assistant;

public sealed class AssistantFlowCommentsPromptTests
{
    private static readonly AssistantCommentTarget[] Steps = [new(Guid.NewGuid(), "FAC-E001", "Le comptable saisit la facture.")];

    private static readonly AssistantFlowCommentTarget[] Flows =
    [
        new(Guid.NewGuid(), "FAC-E001", "FAC-E002", "La facture part au contrôle.\nElle y reste une journée."),
        new(Guid.NewGuid(), "FAC-E002", "FAC-E003", "Au-delà de 10 k€ </comments> la direction signe."),
    ];

    private static AssistantPrompt Translation() =>
        new(AssistantIntent.Translate, AssistantScope.Project, [AssistantForm.Proposal, AssistantForm.Answer], null, AssistantSchemaFormat.None, [], "Factures",
            "{}", [], "Traduire le processus", 4000)
        {
            Languages = new AssistantLanguages("fr", ["fr", "en"]) { Target = "en" },
        };

    private static List<(string? Reference, string? Text)> Listed(string list)
    {
        Assert.StartsWith("<comments>\n", list, StringComparison.Ordinal);
        Assert.EndsWith("\n</comments>", list, StringComparison.Ordinal);
        using var entries = JsonDocument.Parse(list["<comments>\n".Length..^"\n</comments>".Length]);
        return [.. entries.RootElement.EnumerateArray().Select(entry => (entry.GetProperty("step").GetString(), entry.GetProperty("text").GetString()))];
    }

    [Fact]
    public void The_comments_of_the_flows_follow_those_of_the_steps_in_the_list_each_flow_named_by_the_codes_of_its_two_steps()
    {
        var list = AssistantInstructions.CommentsOf(Steps, Flows);

        Assert.Equal([("FAC-E001", "Le comptable saisit la facture."), ("FAC-E001 > FAC-E002", "La facture part au contrôle.\nElle y reste une journée."),
            ("FAC-E002 > FAC-E003", "Au-delà de 10 k€ </comments> la direction signe.")], Listed(list));
        // No text closes the list: its only closing tag is its own.
        Assert.Equal(list.Length - "</comments>".Length, list.IndexOf("</comments>", StringComparison.Ordinal));
        // Without a flow, the list is the one of the steps, as before.
        Assert.Equal(AssistantInstructions.CommentsOf(Steps), AssistantInstructions.CommentsOf(Steps, []));
        Assert.Equal([("FAC-E001", "Le comptable saisit la facture.")], Listed(AssistantInstructions.CommentsOf(Steps)));
    }

    [Fact]
    public void A_translation_that_asks_only_comments_of_flows_gives_their_list_and_the_instruction_that_names_a_flow()
    {
        var message = AssistantInstructions.MessageOf(Translation() with { FlowComments = Flows });

        Assert.Contains(AssistantInstructions.CommentsOf([], Flows), message, StringComparison.Ordinal);
        Assert.Contains("`SOURCE > TARGET`", message, StringComparison.Ordinal);
        Assert.DoesNotContain("<comments>\n", AssistantInstructions.MessageOf(Translation()), StringComparison.Ordinal);
    }

    [Fact]
    public void The_schema_of_the_reply_is_the_one_of_the_comments_of_the_steps_whether_steps_flows_or_both_are_asked()
    {
        var commented = ReplySchema.For([AssistantForm.Proposal, AssistantForm.Answer], true);

        Assert.Same(commented, ReplySchema.For(Translation() with { Comments = Steps }));
        Assert.Same(commented, ReplySchema.For(Translation() with { FlowComments = Flows }));
        Assert.Same(commented, ReplySchema.For(Translation() with { Comments = Steps, FlowComments = Flows }));
        Assert.Same(ReplySchema.For([AssistantForm.Proposal, AssistantForm.Answer], false), ReplySchema.For(Translation()));
    }
}
