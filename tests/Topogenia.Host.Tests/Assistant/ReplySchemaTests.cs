// ReplySchemaTests.cs
// The schema of the reply of an exchange: the fields of the forms the exchange allows and no other, for each list of forms the application sends; a reply of those fields alone read; the instruction of a practice naming no other field; each text sent pinned by its SHA-256.

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Topogenia.Application.Assistant;
using Topogenia.Assistant;
using Topogenia.Domain.Values;

namespace Topogenia.Host.Tests.Assistant;

public sealed class ReplySchemaTests
{
    private static AssistantForm[] FormsOf(string forms) => forms.Split(',').Select(code => Codes.Parse<AssistantForm>(code, "form")).ToArray();

    private static IEnumerable<string> Names(JsonElement list) => list.EnumerateArray().Select(name => name.GetString()!);

    private static JsonElement Properties(JsonDocument schema) => schema.RootElement.GetProperty("properties");

    /// <summary>Whether a property admits null beside its value: an `anyOf` of the value and of null.</summary>
    private static bool AdmitsNull(JsonElement property) =>
        property.TryGetProperty("anyOf", out var either) && either.EnumerateArray().Any(one => one.TryGetProperty("type", out var type) && type.GetString() == "null");

    /// <summary>No recursion, and every object closed with all its properties required, as the structured outputs ask.</summary>
    private static void Closed(JsonElement node)
    {
        if (node.ValueKind == JsonValueKind.Object)
        {
            Assert.False(node.TryGetProperty("$ref", out _));
            if (node.TryGetProperty("type", out var type) && type.GetString() == "object")
            {
                Assert.False(node.GetProperty("additionalProperties").GetBoolean());
                Assert.Equal(node.GetProperty("properties").EnumerateObject().Select(property => property.Name), Names(node.GetProperty("required")));
            }
            foreach (var property in node.EnumerateObject())
                Closed(property.Value);
        }
        else if (node.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in node.EnumerateArray())
                Closed(item);
        }
    }

    /// <summary>
    /// The lists of forms the application sends (`AssistantExchanges`): a member who may propose, a reader, a translation
    /// with comments of presentation and without, the report, the summaries, a project yet to create.
    /// </summary>
    [Theory]
    [InlineData("answer,questions,findings,proposal", false, "form,message,cited,questions,findings,pmap", "answer,questions,findings,proposal", "pmap")]
    [InlineData("answer,questions,findings", false, "form,message,cited,questions,findings", "answer,questions,findings", "")]
    [InlineData("proposal,answer", true, "form,message,cited,pmap,comments", "answer,proposal", "pmap,comments")]
    [InlineData("proposal,answer", false, "form,message,cited,pmap", "answer,proposal", "pmap")]
    [InlineData("report", false, "form,message,cited,report", "report", "")]
    [InlineData("summaries", false, "form,message,cited,summaries", "summaries", "")]
    [InlineData("questions,proposal,answer", false, "form,message,cited,questions,pmap", "answer,questions,proposal", "pmap")]
    public void The_schema_of_an_exchange_holds_the_fields_of_its_forms_alone_all_required_in_closed_objects(string forms, bool comments, string fields, string allowed,
        string nullable)
    {
        using var schema = JsonDocument.Parse(ReplySchema.For(FormsOf(forms), comments));
        var properties = Properties(schema);
        Assert.Equal(fields.Split(','), properties.EnumerateObject().Select(property => property.Name));
        Assert.Equal(fields.Split(','), Names(schema.RootElement.GetProperty("required")));
        Assert.Equal(allowed.Split(','), Names(properties.GetProperty("form").GetProperty("enum")));
        Assert.Equal(nullable.Split(',', StringSplitOptions.RemoveEmptyEntries), properties.EnumerateObject().Where(property => AdmitsNull(property.Value)).Select(property => property.Name));
        // A field that does not admit null is its value itself: the report its object, a list its array.
        Assert.All(properties.EnumerateObject().Where(property => !AdmitsNull(property.Value)), property =>
            Assert.Equal(property.Name is "form" or "message" ? "string" : property.Name == "report" ? "object" : "array", property.Value.GetProperty("type").GetString()));
        Closed(schema.RootElement);
    }

    [Fact]
    public void The_report_admits_null_beside_another_form_the_comments_come_with_a_proposal_only_and_the_text_of_the_same_forms_is_built_once()
    {
        using var beside = JsonDocument.Parse(ReplySchema.For([AssistantForm.Report, AssistantForm.Answer], false));
        Assert.True(AdmitsNull(Properties(beside).GetProperty("report")));
        Assert.Equal(["answer", "report"], Names(Properties(beside).GetProperty("form").GetProperty("enum")));
        using var reader = JsonDocument.Parse(ReplySchema.For([AssistantForm.Answer, AssistantForm.Questions, AssistantForm.Findings], true));
        Assert.False(Properties(reader).TryGetProperty("comments", out _));
        // The same exchange always sends the same text, whatever the order its forms are given in.
        Assert.Same(ReplySchema.For([AssistantForm.Proposal, AssistantForm.Answer], true), ReplySchema.For([AssistantForm.Answer, AssistantForm.Proposal, AssistantForm.Answer], true));
        Assert.NotEqual(ReplySchema.For([AssistantForm.Proposal, AssistantForm.Answer], true), ReplySchema.For([AssistantForm.Proposal, AssistantForm.Answer], false));
        Assert.Throws<ArgumentException>(() => ReplySchema.For([], false));
    }

    [Fact]
    public void The_words_of_the_schema_are_those_the_application_reads()
    {
        Assert.Equal(Codes.All<FindingKind>(), ReplySchema.FindingKinds);
        Assert.Equal(Codes.All<AssistantForm>(), ReplySchema.Forms);
        Assert.Equal(AssistantReportReader.Sources, ReplySchema.Sources);
        Assert.Equal(AssistantReportReader.Waves, ReplySchema.Waves);
    }

    [Theory]
    [InlineData("answer,questions,findings", false, "cited:1", """{"form":"answer","message":"Deux étapes.","cited":["FAC-E001"],"questions":[],"findings":[]}""")]
    [InlineData("answer,questions,findings", false, "findings:1",
        """{"form":"findings","message":"Un constat.","cited":[],"questions":[],"findings":[{"kind":"wait","title":"Attente","detail":"Deux jours.","objects":["FAC-E002"],"suggestion":"Relancer."}]}""")]
    [InlineData("questions,proposal,answer", false, "questions:1", """{"form":"questions","message":"Une question.","cited":[],"questions":[{"question":"Qui valide ?","choices":[]}],"pmap":null}""")]
    [InlineData("proposal,answer", false, "pmap", """{"form":"proposal","message":"Traduit.","cited":[],"pmap":"{}"}""")]
    [InlineData("proposal,answer", true, "pmap,comments:1", """{"form":"proposal","message":"Traduit.","cited":[],"pmap":"{}","comments":[{"step":"FAC-E001","text":"The invoice is checked."}]}""")]
    [InlineData("report", false, "report",
        """{"form":"report","message":"Le rapport.","cited":[],"report":{"summary":"La facture part de la commande.","actors":"","chapters":[],"journey":[],"problems":[],"actions":[],"conclusion":""}}""")]
    [InlineData("summaries", false, "summaries:1", """{"form":"summaries","message":"Une carte.","cited":[],"summaries":[{"map":"FAC","text":"La comptabilité facture la commande."}]}""")]
    public void A_reply_that_holds_the_fields_of_its_schema_alone_is_read(string forms, bool comments, string brought, string text)
    {
        using var schema = JsonDocument.Parse(ReplySchema.For(FormsOf(forms), comments));
        using var written = JsonDocument.Parse(text);
        // The reply is one its schema admits at the top: every field of the schema, and no other.
        Assert.Equal(Names(schema.RootElement.GetProperty("required")), written.RootElement.EnumerateObject().Select(member => member.Name));
        var reply = AssistantReplyReader.Read(text, out var reason) ?? throw new InvalidOperationException(reason);
        Assert.Equal(written.RootElement.GetProperty("form").GetString(), Codes.Of(reply.Form));
        Assert.Equal(written.RootElement.GetProperty("message").GetString(), reply.Message);
        var read = new List<string>();
        if (reply.Cited.Count > 0)
            read.Add($"cited:{reply.Cited.Count}");
        if (reply.Questions.Count > 0)
            read.Add($"questions:{reply.Questions.Count}");
        if (reply.Findings.Count > 0)
            read.Add($"findings:{reply.Findings.Count}");
        if (reply.Pmap is not null)
            read.Add("pmap");
        if (reply.Report is { Summary.Length: > 0 })
            read.Add("report");
        if (reply.Summaries is { } summaries)
            read.Add($"summaries:{summaries.Count}");
        if (reply.Comments is { } translated)
            read.Add($"comments:{translated.Count}");
        Assert.Equal(brought, string.Join(',', read));
    }

    /// <summary>
    /// The practices whose instruction says which fields to fill: a field named there that the schema of the exchange
    /// does not hold could not be written (a translation is given its widest schema, the one with the comments).
    /// </summary>
    [Theory]
    [InlineData(AssistantIntent.Report, "report", false)]
    [InlineData(AssistantIntent.Summaries, "summaries", false)]
    [InlineData(AssistantIntent.Translate, "proposal,answer", true)]
    public void The_instruction_of_a_practice_names_no_field_that_the_schema_of_its_exchange_does_not_hold(AssistantIntent intent, string forms, bool comments)
    {
        using var schema = JsonDocument.Parse(ReplySchema.For(FormsOf(forms), comments));
        using var whole = JsonDocument.Parse(ReplySchema.For(Enum.GetValues<AssistantForm>(), true));
        var held = Properties(schema).EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal);
        var absent = Properties(whole).EnumerateObject().Select(property => property.Name).Where(name => !held.Contains(name)).ToList();
        Assert.NotEmpty(absent);
        Assert.All(absent, field => Assert.DoesNotContain($"`{field}`", AssistantInstructions.PracticeOf(intent)!, StringComparison.Ordinal));
    }

    /// <summary>
    /// Each text the application sends, pinned: a provider compiles the schema into a grammar and refuses one too
    /// large, by an internal limit it does not quantify, and no automated test reaches the real service.
    /// </summary>
    [Theory]
    [InlineData("answer,questions,findings,proposal", false, "e089d8143952831b7bdb59c9b78c9c2f6f7f92029051551304b90149e284e6d4")]
    [InlineData("answer,questions,findings", false, "494f9afb859f9e09a5bf2dfa394575338a36fc77abc60778de0335f410ba8def")]
    [InlineData("proposal,answer", true, "afa44c54fe2b5e884ceebda43fb2d8f22dc4168695e9d11aea14551214673360")]
    [InlineData("proposal,answer", false, "3051d0a51b7df284c853434e9bac8095c38ee0c35aee2c6b8cd46c29bde180d1")]
    [InlineData("report", false, "9e0639b5eb6aee255810b8c5a3d11caa3eac65d1486eed127ccf9c7ee613f71c")]
    [InlineData("summaries", false, "c6f90c13d63797f53527451b0ebabffc26eac7007f22518b7a822d7c7ab68d8f")]
    [InlineData("questions,proposal,answer", false, "feb5a590c45717a2f42d11bc67318499d7915a9d030d736f1212a8ce539a07af")]
    public void A_schema_sent_to_the_providers_changes_only_with_a_new_trial_against_the_real_service(string forms, bool comments, string sha256)
    {
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(ReplySchema.For(FormsOf(forms), comments))));
        if (hash != sha256)
            Assert.Fail($"A schema of the reply sent to the providers changed (forms {forms}, comments {comments}): its SHA-256 is now {hash}. "
                + "The limit of a provider on the grammar it compiles from a schema is not quantified in its documentation, and no automated test reaches the real service: "
                + "play the real trial against the provider again with this schema, and only then update the value pinned here.");
    }
}
