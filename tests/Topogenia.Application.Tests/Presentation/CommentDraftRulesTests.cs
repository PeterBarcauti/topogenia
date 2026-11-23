// CommentDraftRulesTests.cs
// The rule of a comment written by the assistant (3.3, lot 3, F-130), the same for a step and for a flow: its original written with the source of the assistant, the automatic translations of a changed original dropped and the typed ones kept, an original typed by hand kept unless its replacement is accepted.

using Topogenia.Application.Presentation;
using Topogenia.Domain.Model;
using Topogenia.Domain.Workspace;

namespace Topogenia.Application.Tests.Presentation;

public sealed class CommentDraftRulesTests
{
    private static readonly Guid Subject = Guid.Parse("0190a1b2-0000-7000-8000-00000000c001");

    private static readonly Guid Other = Guid.Parse("0190a1b2-0000-7000-8000-00000000c002");

    private static readonly DateTime Now = new(2026, 11, 17, 2, 0, 0, DateTimeKind.Utc);

    private static readonly OperationContext Context = new(Guid.Parse("0190a1b2-0000-7000-8000-00000000a009"), Now, "fr", AppSettings.Default);

    private static CommentLine Line(Guid subject, string language, string text, CommentSource source, int version = 1) =>
        new(subject, text, language, source, version, Now.AddDays(-1), Guid.Empty);

    private static List<(string Language, string? Text, CommentSource? Source, int VersionRead)> Shown(IReadOnlyList<CommentLineWrite> writes) =>
        [.. writes.Select(write => (write.Language, write.Line?.Text, write.Line?.Source, write.VersionRead))];

    [Fact]
    public void A_comment_written_for_a_subject_without_one_is_its_original_from_the_assistant()
    {
        var writes = CommentLineRules.Drafted(new CommentSubject(Subject, "fr"), [Line(Other, "fr", "Ailleurs.", CommentSource.Manual)], "Le salarié saisit.", false, Context);

        Assert.Equal([("fr", "Le salarié saisit.", CommentSource.Assistant, 0)], Shown(writes));
        var line = writes[0].Line!;
        Assert.Equal((Subject, 1, Now, Context.ActorId), (line.SubjectId, line.Version, line.UpdatedAt, line.UpdatedBy));
    }

    [Fact]
    public void A_changed_original_drops_its_automatic_translations_and_keeps_the_typed_ones()
    {
        var kept = new[]
        {
            Line(Subject, "fr", "Ancien.", CommentSource.Assistant, 3), Line(Subject, "en", "Old.", CommentSource.Assistant, 2),
            Line(Subject, "pt", "Antigo.", CommentSource.Workbook), Line(Subject, "de", "Von Hand.", CommentSource.Manual),
        };

        var writes = CommentLineRules.Drafted(new CommentSubject(Subject, "fr"), kept, "Nouveau.", false, Context);

        Assert.Equal([("fr", "Nouveau.", CommentSource.Assistant, 3), ("en", null, null, 2), ("pt", null, null, 1)], Shown(writes));
        // The same text once trimmed changes the line, not its translations; the very same text writes nothing.
        Assert.Equal([("fr", "Ancien.\n", CommentSource.Assistant, 3)], Shown(CommentLineRules.Drafted(new CommentSubject(Subject, "fr"), kept, "Ancien.\n", false, Context)));
        Assert.Empty(CommentLineRules.Drafted(new CommentSubject(Subject, "fr"), kept, "Ancien.", false, Context));
    }

    [Fact]
    public void An_original_typed_by_hand_stays_unless_its_replacement_is_accepted()
    {
        var kept = new[] { Line(Subject, "fr", "Tapé.", CommentSource.Manual, 4), Line(Subject, "en", "Typed.", CommentSource.Manual) };
        var subject = new CommentSubject(Subject, "fr");

        Assert.Empty(CommentLineRules.Drafted(subject, kept, "Proposé.", false, Context));
        Assert.Equal([("fr", "Proposé.", CommentSource.Assistant, 4)], Shown(CommentLineRules.Drafted(subject, kept, "Proposé.", true, Context)));
        // The language of origin of a flow is the one of its source step: a line in another language is a translation.
        Assert.Equal([("en", "Said.", CommentSource.Assistant, 1)], Shown(CommentLineRules.Drafted(new CommentSubject(Subject, "en"), kept, "Said.", true, Context)));
    }
}
