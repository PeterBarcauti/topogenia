// PresentationCommentsTests.cs
// The comments of presentation of the steps (ADR 099 revised): a line per language, the original in the language of origin of the step and its translations, written by a facilitator, versioned line by line, removed by an empty text, read with a map by its readers.

using Topogenia.Application.Presentation;
using Topogenia.Application.Tests.Support;
using Topogenia.Application.Tests.Users;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;
using Topogenia.Domain.Values;

namespace Topogenia.Application.Tests.Presentation;

public sealed class PresentationCommentsTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static Task<IReadOnlyList<PresentationCommentView>> SetAsync(UsersFixture fixture, Guid stepId, string? text, string? language = "fr", int? version = null) =>
        fixture.Runner.RunAsync(SetPresentationCommentRequest.WithVersion(stepId, text, language, version), Token);

    private static Task<IReadOnlyList<PresentationCommentView>> ReadAsync(UsersFixture fixture, Guid mapId) =>
        fixture.Runner.RunAsync(new ReadPresentationCommentsQuery(mapId), Token);

    /// <summary>A line put in the database as the assistant or a workbook would have written it.</summary>
    private static void Given(UsersFixture fixture, Guid step, string language, string text, CommentSource source, int version = 1) =>
        fixture.Database.AddComment(new PresentationComment(step, text, language, source, version, TestProject.Now, fixture.Project.Facilitator.Id));

    [Fact]
    public async Task A_facilitator_writes_the_original_of_a_step_trimmed_with_its_language_its_source_its_author_and_version_1()
    {
        var fixture = new UsersFixture();
        var step = fixture.Project.Step(0);
        var lines = await SetAsync(fixture, step, "  Ici la commande part chez le fournisseur.  ", "fr", 0);
        Assert.Equal([new PresentationCommentView(step, "Ici la commande part chez le fournisseur.", "fr", "manual", 1)], lines);
        var kept = fixture.Database.Comments[(step, "fr")];
        Assert.Equal((CommentSource.Manual, 1, TestProject.Now, fixture.Project.Facilitator.Id), (kept.Source, kept.Version, kept.UpdatedAt, kept.UpdatedBy));
        Assert.Equal(1, fixture.Database.Commits);
    }

    [Fact]
    public async Task A_line_in_another_language_is_a_translation_typed_and_leaves_the_original_as_it_is()
    {
        var fixture = new UsersFixture();
        var step = fixture.Project.Step(0);
        await SetAsync(fixture, step, "La demande arrive.", "fr", 0);
        var lines = await SetAsync(fixture, step, "The request comes in.", "en", 0);
        Assert.Equal([new PresentationCommentView(step, "The request comes in.", "en", "manual", 1), new PresentationCommentView(step, "La demande arrive.", "fr", "manual", 1)],
            lines);
        Assert.Equal(1, fixture.Database.Comments[(step, "fr")].Version);
    }

    [Fact]
    public async Task A_line_written_again_moves_on_by_one_version_and_the_same_text_typed_writes_nothing()
    {
        var fixture = new UsersFixture();
        var step = fixture.Project.Step(0);
        await SetAsync(fixture, step, "Première phrase.");
        var changed = await SetAsync(fixture, step, "Seconde phrase.", "fr", 1);
        Assert.Equal(("Seconde phrase.", 2), (changed.Single().Text, changed.Single().Version));
        var writes = fixture.Comments.Writes;
        var same = await SetAsync(fixture, step, " Seconde phrase. ", "fr", 2);
        Assert.Equal(2, same.Single().Version);
        Assert.Equal(writes, fixture.Comments.Writes);
    }

    [Fact]
    public async Task An_original_really_changed_drops_its_automatic_translations_and_keeps_the_typed_ones()
    {
        var fixture = new UsersFixture(UsersFixture.Languages with { Languages = ["fr", "en", "de", "es"] });
        var step = fixture.Project.Step(0);
        await SetAsync(fixture, step, "La demande arrive.");
        Given(fixture, step, "en", "The request comes in.", CommentSource.Assistant);
        Given(fixture, step, "de", "Die Anfrage kommt an.", CommentSource.Workbook);
        await SetAsync(fixture, step, "La solicitud llega.", "es", 0);
        // The same original, its spaces aside: nothing falls.
        await SetAsync(fixture, step, "  La demande arrive.  ", "fr", 1);
        Assert.Equal(["de", "en", "es", "fr"], fixture.Database.CommentsOf(step).Keys.Order());
        var lines = await SetAsync(fixture, step, "La demande arrive par courriel.", "fr", 1);
        Assert.Equal([("es", "manual"), ("fr", "manual")], lines.Select(line => (line.Language, line.Source)));
        Assert.Equal(["es", "fr"], fixture.Database.CommentsOf(step).Keys.Order());
    }

    [Fact]
    public async Task A_first_original_typed_drops_an_automatic_translation_left_without_original_and_keeps_a_typed_one()
    {
        // A package 2.4 admits a translation without its original (audit 3.2.0, ROB-CRIT-01): the first original typed is
        // a change of the original, so the automatic lines fall and the typed ones stay.
        var fixture = new UsersFixture(UsersFixture.Languages with { Languages = ["fr", "en", "es"] });
        var step = fixture.Project.Step(0);
        Given(fixture, step, "en", "The visit starts here.", CommentSource.Assistant);
        Given(fixture, step, "es", "La visita empieza aqui.", CommentSource.Manual);
        var lines = await SetAsync(fixture, step, "La visite commence ici.", "fr", 0);
        Assert.Equal([("es", "manual"), ("fr", "manual")], lines.Select(line => (line.Language, line.Source)));
        Assert.Equal(["es", "fr"], fixture.Database.CommentsOf(step).Keys.Order());
    }

    [Fact]
    public async Task A_translation_typed_over_an_automatic_one_becomes_typed_even_with_the_same_text()
    {
        var fixture = new UsersFixture();
        var step = fixture.Project.Step(0);
        await SetAsync(fixture, step, "La demande arrive.");
        Given(fixture, step, "en", "The request comes in.", CommentSource.Assistant);
        var lines = await SetAsync(fixture, step, "The request comes in.", "en", 1);
        Assert.Equal(("manual", 2), (lines.Single(line => line.Language == "en").Source, lines.Single(line => line.Language == "en").Version));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task An_empty_original_removes_the_comment_and_all_its_translations(string? text)
    {
        var fixture = new UsersFixture();
        var step = fixture.Project.Step(0);
        var other = fixture.Project.Step(1);
        await SetAsync(fixture, step, "À dire en présentation.");
        await SetAsync(fixture, step, "To say while presenting.", "en", 0);
        await SetAsync(fixture, other, "Autre étape.");
        Assert.Empty(await SetAsync(fixture, step, text, "fr", 1));
        Assert.Equal([(other, "fr")], fixture.Database.Comments.Keys);
        // Removing a comment that is not there writes nothing.
        var writes = fixture.Comments.Writes;
        Assert.Empty(await SetAsync(fixture, step, text, "fr", 0));
        Assert.Equal(writes, fixture.Comments.Writes);
    }

    [Fact]
    public async Task An_empty_translation_removes_this_translation_only()
    {
        var fixture = new UsersFixture();
        var step = fixture.Project.Step(0);
        await SetAsync(fixture, step, "À dire en présentation.");
        await SetAsync(fixture, step, "To say while presenting.", "en", 0);
        var lines = await SetAsync(fixture, step, " ", "en", 1);
        Assert.Equal(["fr"], lines.Select(line => line.Language));
        Assert.Equal([(step, "fr")], fixture.Database.Comments.Keys);
    }

    [Fact]
    public async Task A_translation_without_an_original_is_kept()
    {
        var fixture = new UsersFixture();
        var step = fixture.Project.Step(0);
        var lines = await SetAsync(fixture, step, "Only in English.", "en", 0);
        Assert.Equal([new PresentationCommentView(step, "Only in English.", "en", "manual", 1)], lines);
    }

    [Fact]
    public async Task A_line_changed_since_the_version_read_is_refused_and_kept_as_it_was()
    {
        var fixture = new UsersFixture();
        var step = fixture.Project.Step(0);
        await SetAsync(fixture, step, "Version de Anne.");
        var refusal = await Assert.ThrowsAsync<DomainException>(() => SetAsync(fixture, step, "Version lue avant.", "fr", 0));
        Assert.Equal((ErrorCodes.VersionConflict, "presentation_comment", (object?)fixture.Project.Facilitator.Id),
            (refusal.Code, refusal.Details["object"], refusal.Details["updated_by"]));
        Assert.Equal(("Version de Anne.", 1), (fixture.Database.Comments[(step, "fr")].Text, fixture.Database.Comments[(step, "fr")].Version));
    }

    [Fact]
    public async Task The_version_read_is_the_one_of_the_line_of_the_language_written()
    {
        var fixture = new UsersFixture();
        var step = fixture.Project.Step(0);
        await SetAsync(fixture, step, "Original.");
        await SetAsync(fixture, step, "Original, relu.", "fr", 1);
        // The original is at its version 2: the translation, which does not exist yet, is at 0.
        Assert.Equal(ErrorCodes.VersionConflict, (await Assert.ThrowsAsync<DomainException>(() => SetAsync(fixture, step, "Translation.", "en", 2))).Code);
        Assert.Equal(2, (await SetAsync(fixture, step, "Translation.", "en", 0)).Count);
    }

    [Fact]
    public async Task A_comment_without_the_version_its_mode_requires_is_refused_428_and_writes_nothing()
    {
        var fixture = new UsersFixture();
        var step = fixture.Project.Step(0);
        fixture.Project.Versions.IsMissing = true;
        var refusal = await Assert.ThrowsAsync<DomainException>(() => SetAsync(fixture, step, "Sans version."));
        Assert.Equal((ErrorCodes.VersionRequired, RefusalKind.PreconditionRequired), (refusal.Code, refusal.Kind));
        Assert.Empty(fixture.Database.Comments);
    }

    [Theory]
    [InlineData("<b>gras</b>", "fr", ErrorCodes.HtmlForbidden)]
    [InlineData("Une phrase.", "de", ErrorCodes.InvalidLanguage)]
    [InlineData("Une phrase.", null, ErrorCodes.InvalidLanguage)]
    [InlineData("", null, ErrorCodes.InvalidLanguage)]
    public async Task An_invalid_comment_is_refused_and_writes_nothing(string text, string? language, string code)
    {
        var fixture = new UsersFixture();
        var step = fixture.Project.Step(0);
        var refusal = await Assert.ThrowsAsync<DomainException>(() => SetAsync(fixture, step, text, language));
        Assert.Equal(code, refusal.Code);
        Assert.Empty(fixture.Database.Comments);
    }

    [Fact]
    public async Task A_comment_of_500_characters_is_kept_and_one_of_501_is_refused()
    {
        var fixture = new UsersFixture();
        var step = fixture.Project.Step(0);
        Assert.Equal(PresentationCommentRules.TextMax, (await SetAsync(fixture, step, new string('a', 500))).Single().Text.Length);
        var refusal = await Assert.ThrowsAsync<DomainException>(() => SetAsync(fixture, step, new string('b', 501), "fr", 1));
        Assert.Equal((ErrorCodes.TextTooLong, (object?)500), (refusal.Code, refusal.Details["max"]));
    }

    [Fact]
    public async Task The_lines_of_a_comment_keep_their_breaks_whatever_the_line_ends_given()
    {
        var fixture = new UsersFixture();
        var step = fixture.Project.Step(0);
        var lines = await SetAsync(fixture, step, "Première phrase.\r\nSeconde phrase.\rTroisième.");
        Assert.Equal("Première phrase.\nSeconde phrase.\nTroisième.", lines.Single().Text);
    }

    [Fact]
    public async Task Only_a_facilitator_writes_a_comment_a_reader_reads_them_and_a_stranger_finds_nothing()
    {
        var fixture = new UsersFixture();
        var step = fixture.Project.Step(0);
        await SetAsync(fixture, step, "Commentaire.");
        foreach (var member in new[] { fixture.Project.Contributor, fixture.Project.Reader })
        {
            fixture.ActingAs(member);
            var refused = await Assert.ThrowsAsync<DomainException>(() => SetAsync(fixture, step, "Autre.", "fr", 1));
            Assert.Equal(ErrorCodes.AccessDenied, refused.Code);
        }
        Assert.Equal([new PresentationCommentView(step, "Commentaire.", "fr", "manual", 1)], await ReadAsync(fixture, fixture.Project.RootMapId));
        fixture.ActingAs(fixture.Project.Stranger);
        Assert.Equal(ErrorCodes.NotFound, (await Assert.ThrowsAsync<DomainException>(() => ReadAsync(fixture, fixture.Project.RootMapId))).Code);
        Assert.Equal(ErrorCodes.NotFound, (await Assert.ThrowsAsync<DomainException>(() => SetAsync(fixture, step, "Autre.", "fr", 1))).Code);
    }

    [Theory]
    [InlineData(StepType.Task)]
    [InlineData(StepType.Decision)]
    [InlineData(StepType.Parallel)]
    [InlineData(StepType.Inclusive)]
    [InlineData(StepType.Wait)]
    [InlineData(StepType.Start)]
    [InlineData(StepType.End)]
    public async Task A_step_of_any_type_takes_a_comment_a_gateway_a_start_and_an_end_too(StepType type)
    {
        // Asked by the project owner (lot J10): every object a presentation stops on may say a text of its own.
        var fixture = new UsersFixture();
        var id = fixture.Project.Step(0);
        fixture.Database.Add(fixture.Database.Get<Step>(Step.KeyOf(id)) with { StepType = type });
        var lines = await SetAsync(fixture, id, "Ici le dossier change de mains.", "fr", 0);
        Assert.Equal([new PresentationCommentView(id, "Ici le dossier change de mains.", "fr", "manual", 1)], lines);
        Assert.Equal(lines, await ReadAsync(fixture, fixture.Project.RootMapId));
    }

    [Fact]
    public async Task A_map_reads_every_line_of_the_comments_of_its_live_steps_only_and_a_deleted_step_takes_none()
    {
        var fixture = new UsersFixture();
        var first = fixture.Project.Step(0);
        var deleted = fixture.Project.Step(1);
        var (subMap, subLane) = fixture.Project.SubMap(first);
        var below = fixture.Project.Step(0, laneId: subLane, mapId: subMap);
        await SetAsync(fixture, first, "Sur la carte.");
        await SetAsync(fixture, first, "On the map.", "en", 0);
        await SetAsync(fixture, deleted, "Étape supprimée ensuite.");
        await SetAsync(fixture, below, "Dans le détail.");
        var step = fixture.Database.Get<Step>(Step.KeyOf(deleted));
        fixture.Database.Add(step with { DeletedAt = TestProject.Now, DeletionBatchId = fixture.Project.NextId() });
        Assert.Equal([(first, "en"), (first, "fr")], (await ReadAsync(fixture, fixture.Project.RootMapId)).Select(comment => (comment.StepId, comment.Language)));
        Assert.Equal([below], (await ReadAsync(fixture, subMap)).Select(comment => comment.StepId));
        // The comment of a deleted step stays for its restoration, but cannot be written.
        Assert.True(fixture.Database.Comments.ContainsKey((deleted, "fr")));
        Assert.Equal(ErrorCodes.NotFound, (await Assert.ThrowsAsync<DomainException>(() => SetAsync(fixture, deleted, "Autre.", "fr", 1))).Code);
    }

    [Fact]
    public async Task A_comment_follows_its_step_when_the_step_goes_to_another_map()
    {
        var fixture = new UsersFixture();
        var step = fixture.Project.Step(0);
        var other = fixture.Project.Step(1);
        var (subMap, subLane) = fixture.Project.SubMap(other);
        await SetAsync(fixture, step, "Suit l'étape.");
        fixture.Database.Add(fixture.Database.Get<Step>(Step.KeyOf(step)) with { MapId = subMap, LaneId = subLane });
        Assert.Empty(await ReadAsync(fixture, fixture.Project.RootMapId));
        Assert.Equal([step], (await ReadAsync(fixture, subMap)).Select(comment => comment.StepId));
    }

    [Fact]
    public async Task A_map_that_is_not_live_has_no_comments_to_read()
    {
        var fixture = new UsersFixture();
        var parent = fixture.Project.Step(0);
        var (subMap, _) = fixture.Project.SubMap(parent);
        var map = fixture.Database.Get<ProcessMap>(ProcessMap.KeyOf(subMap));
        fixture.Database.Add(map with { DeletedAt = TestProject.Now, DeletionBatchId = fixture.Project.NextId() });
        Assert.Equal(ErrorCodes.NotFound, (await Assert.ThrowsAsync<DomainException>(() => ReadAsync(fixture, subMap))).Code);
    }
}
