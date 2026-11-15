// FlowPresentationCommentsTests.cs
// The comments of presentation of the flows (ADR 099, lot J10): a line per language, the original in the language of origin of the source step of the flow and its translations, written by a facilitator, versioned line by line, removed by an empty text, read with a map by its readers, kept with a flow in the trash.

using Topogenia.Application.Presentation;
using Topogenia.Application.Tests.Support;
using Topogenia.Application.Tests.Users;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;

namespace Topogenia.Application.Tests.Presentation;

public sealed class FlowPresentationCommentsTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static Task<IReadOnlyList<FlowPresentationCommentView>> SetAsync(UsersFixture fixture, Guid flowId, string? text, string? language = "fr", int? version = null) =>
        fixture.Runner.RunAsync(SetFlowPresentationCommentRequest.WithVersion(flowId, text, language, version), Token);

    private static Task<IReadOnlyList<FlowPresentationCommentView>> ReadAsync(UsersFixture fixture, Guid mapId) =>
        fixture.Runner.RunAsync(new ReadFlowPresentationCommentsQuery(mapId), Token);

    /// <summary>A flow between two new steps of the root map, in the columns given.</summary>
    private static Guid NewFlow(UsersFixture fixture, int column = 0) => fixture.Project.Flow(fixture.Project.Step(column), fixture.Project.Step(column + 1));

    /// <summary>A line put in the database as the assistant or a workbook would have written it.</summary>
    private static void Given(UsersFixture fixture, Guid flow, string language, string text, CommentSource source, int version = 1) =>
        fixture.Database.AddFlowComment(new FlowPresentationComment(flow, text, language, source, version, TestProject.Now, fixture.Project.Facilitator.Id));

    private static void PutInTrash(UsersFixture fixture, Guid flowId) =>
        fixture.Database.Add(fixture.Database.Get<Flow>(Flow.KeyOf(flowId)) with { DeletedAt = TestProject.Now, DeletionBatchId = fixture.Project.NextId() });

    [Fact]
    public async Task A_facilitator_writes_the_original_of_a_flow_trimmed_with_its_language_its_source_its_author_and_version_1()
    {
        var fixture = new UsersFixture();
        var flow = NewFlow(fixture);
        var lines = await SetAsync(fixture, flow, "  Le dossier passe à la comptabilité.  ", "fr", 0);
        Assert.Equal([new FlowPresentationCommentView(flow, "Le dossier passe à la comptabilité.", "fr", "manual", 1)], lines);
        var kept = fixture.Database.FlowComments[(flow, "fr")];
        Assert.Equal((CommentSource.Manual, 1, TestProject.Now, fixture.Project.Facilitator.Id), (kept.Source, kept.Version, kept.UpdatedAt, kept.UpdatedBy));
        Assert.Equal(1, fixture.Database.Commits);
        // The comment of a flow is no comment of a step: the steps of the flow have none.
        Assert.Empty(fixture.Database.Comments);
    }

    [Fact]
    public async Task A_line_in_another_language_is_a_translation_typed_and_leaves_the_original_as_it_is()
    {
        var fixture = new UsersFixture();
        var flow = NewFlow(fixture);
        await SetAsync(fixture, flow, "Le dossier passe au contrôle.", "fr", 0);
        var lines = await SetAsync(fixture, flow, "The file goes to the check.", "en", 0);
        Assert.Equal([new FlowPresentationCommentView(flow, "The file goes to the check.", "en", "manual", 1),
            new FlowPresentationCommentView(flow, "Le dossier passe au contrôle.", "fr", "manual", 1)], lines);
        Assert.Equal(1, fixture.Database.FlowComments[(flow, "fr")].Version);
    }

    /// <summary>A flow has no language of its own: the original of its comment is in the language of origin of its source step, whatever its target.</summary>
    [Fact]
    public async Task The_original_of_the_comment_of_a_flow_is_the_line_in_the_language_of_origin_of_its_source_step()
    {
        var fixture = new UsersFixture();
        var source = fixture.Project.Step(0);
        var target = fixture.Project.Step(1);
        fixture.Database.Add(fixture.Database.Get<Step>(Step.KeyOf(source)) with { SourceLanguage = "en" });
        var flow = fixture.Project.Flow(source, target);
        await SetAsync(fixture, flow, "The file changes hands.", "en", 0);
        Given(fixture, flow, "fr", "Le dossier change de mains.", CommentSource.Assistant);
        // The line in the language of the target step is a translation: empty, it goes alone.
        Assert.Equal(["en"], (await SetAsync(fixture, flow, "", "fr", 1)).Select(line => line.Language));
        Given(fixture, flow, "fr", "Le dossier change de mains.", CommentSource.Workbook);
        // The line in the language of the source step is the original: changed, its automatic translation falls; empty, the comment goes.
        Assert.Equal(["en"], (await SetAsync(fixture, flow, "The file changes hands here.", "en", 1)).Select(line => line.Language));
        Given(fixture, flow, "fr", "Le dossier change de mains ici.", CommentSource.Manual);
        Assert.Empty(await SetAsync(fixture, flow, null, "en", 2));
        Assert.Empty(fixture.Database.FlowComments);
    }

    [Fact]
    public async Task A_line_written_again_moves_on_by_one_version_and_the_same_text_typed_writes_nothing()
    {
        var fixture = new UsersFixture();
        var flow = NewFlow(fixture);
        await SetAsync(fixture, flow, "Première phrase.");
        var changed = await SetAsync(fixture, flow, "Seconde phrase.", "fr", 1);
        Assert.Equal(("Seconde phrase.", 2), (changed.Single().Text, changed.Single().Version));
        var writes = fixture.FlowComments.Writes;
        var same = await SetAsync(fixture, flow, " Seconde phrase. ", "fr", 2);
        Assert.Equal(2, same.Single().Version);
        Assert.Equal(writes, fixture.FlowComments.Writes);
    }

    [Fact]
    public async Task An_original_really_changed_drops_its_automatic_translations_and_keeps_the_typed_ones()
    {
        var fixture = new UsersFixture(UsersFixture.Languages with { Languages = ["fr", "en", "de", "es"] });
        var flow = NewFlow(fixture);
        await SetAsync(fixture, flow, "Le dossier part au service achats.");
        Given(fixture, flow, "en", "The file goes to purchasing.", CommentSource.Assistant);
        Given(fixture, flow, "de", "Die Akte geht an den Einkauf.", CommentSource.Workbook);
        await SetAsync(fixture, flow, "El expediente pasa a compras.", "es", 0);
        // The same original, its spaces aside: nothing falls.
        await SetAsync(fixture, flow, "  Le dossier part au service achats.  ", "fr", 1);
        Assert.Equal(["de", "en", "es", "fr"], fixture.Database.FlowCommentsOf(flow).Keys.Order());
        var lines = await SetAsync(fixture, flow, "Le dossier part au service achats par courriel.", "fr", 1);
        Assert.Equal([("es", "manual"), ("fr", "manual")], lines.Select(line => (line.Language, line.Source)));
        Assert.Equal(["es", "fr"], fixture.Database.FlowCommentsOf(flow).Keys.Order());
    }

    [Fact]
    public async Task A_first_original_typed_drops_an_automatic_translation_left_without_original_and_keeps_a_typed_one()
    {
        var fixture = new UsersFixture(UsersFixture.Languages with { Languages = ["fr", "en", "es"] });
        var flow = NewFlow(fixture);
        Given(fixture, flow, "en", "The file leaves here.", CommentSource.Assistant);
        Given(fixture, flow, "es", "El expediente sale de aqui.", CommentSource.Manual);
        var lines = await SetAsync(fixture, flow, "Le dossier part d'ici.", "fr", 0);
        Assert.Equal([("es", "manual"), ("fr", "manual")], lines.Select(line => (line.Language, line.Source)));
        Assert.Equal(["es", "fr"], fixture.Database.FlowCommentsOf(flow).Keys.Order());
    }

    [Fact]
    public async Task A_translation_typed_over_an_automatic_one_becomes_typed_even_with_the_same_text()
    {
        var fixture = new UsersFixture();
        var flow = NewFlow(fixture);
        await SetAsync(fixture, flow, "Le dossier part au contrôle.");
        Given(fixture, flow, "en", "The file goes to the check.", CommentSource.Assistant);
        var lines = await SetAsync(fixture, flow, "The file goes to the check.", "en", 1);
        Assert.Equal(("manual", 2), (lines.Single(line => line.Language == "en").Source, lines.Single(line => line.Language == "en").Version));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task An_empty_original_removes_the_comment_and_all_its_translations(string? text)
    {
        var fixture = new UsersFixture();
        var flow = NewFlow(fixture);
        var other = NewFlow(fixture, 2);
        await SetAsync(fixture, flow, "À dire pendant le passage.");
        await SetAsync(fixture, flow, "To say along the flow.", "en", 0);
        await SetAsync(fixture, other, "Autre flèche.");
        Assert.Empty(await SetAsync(fixture, flow, text, "fr", 1));
        Assert.Equal([(other, "fr")], fixture.Database.FlowComments.Keys);
        // Removing a comment that is not there writes nothing.
        var writes = fixture.FlowComments.Writes;
        Assert.Empty(await SetAsync(fixture, flow, text, "fr", 0));
        Assert.Equal(writes, fixture.FlowComments.Writes);
    }

    [Fact]
    public async Task An_empty_translation_removes_this_translation_only()
    {
        var fixture = new UsersFixture();
        var flow = NewFlow(fixture);
        await SetAsync(fixture, flow, "À dire pendant le passage.");
        await SetAsync(fixture, flow, "To say along the flow.", "en", 0);
        var lines = await SetAsync(fixture, flow, " ", "en", 1);
        Assert.Equal(["fr"], lines.Select(line => line.Language));
        Assert.Equal([(flow, "fr")], fixture.Database.FlowComments.Keys);
    }

    [Fact]
    public async Task A_translation_without_an_original_is_kept_and_an_empty_original_then_removes_nothing()
    {
        var fixture = new UsersFixture();
        var flow = NewFlow(fixture);
        var lines = await SetAsync(fixture, flow, "Only in English.", "en", 0);
        Assert.Equal([new FlowPresentationCommentView(flow, "Only in English.", "en", "manual", 1)], lines);
        // No original: there is nothing an empty text replaces, the translation typed stays.
        Assert.Equal(lines, await SetAsync(fixture, flow, "", "fr", 0));
        Assert.Equal([(flow, "en")], fixture.Database.FlowComments.Keys);
    }

    [Fact]
    public async Task A_line_changed_since_the_version_read_is_refused_and_kept_as_it_was()
    {
        var fixture = new UsersFixture();
        var flow = NewFlow(fixture);
        await SetAsync(fixture, flow, "Version de Anne.");
        var refusal = await Assert.ThrowsAsync<DomainException>(() => SetAsync(fixture, flow, "Version lue avant.", "fr", 0));
        Assert.Equal((ErrorCodes.VersionConflict, "flow_presentation_comment", (object?)fixture.Project.Facilitator.Id, (object?)TestProject.Now),
            (refusal.Code, refusal.Details["object"], refusal.Details["updated_by"], refusal.Details["updated_at"]));
        Assert.Equal(("Version de Anne.", 1), (fixture.Database.FlowComments[(flow, "fr")].Text, fixture.Database.FlowComments[(flow, "fr")].Version));
    }

    [Fact]
    public async Task The_version_read_is_the_one_of_the_line_of_the_language_written()
    {
        var fixture = new UsersFixture();
        var flow = NewFlow(fixture);
        await SetAsync(fixture, flow, "Original.");
        await SetAsync(fixture, flow, "Original, relu.", "fr", 1);
        // The original is at its version 2: the translation, which does not exist yet, is at 0.
        Assert.Equal(ErrorCodes.VersionConflict, (await Assert.ThrowsAsync<DomainException>(() => SetAsync(fixture, flow, "Translation.", "en", 2))).Code);
        Assert.Equal(2, (await SetAsync(fixture, flow, "Translation.", "en", 0)).Count);
    }

    [Fact]
    public async Task A_comment_without_the_version_its_mode_requires_is_refused_428_and_writes_nothing()
    {
        var fixture = new UsersFixture();
        var flow = NewFlow(fixture);
        fixture.Project.Versions.IsMissing = true;
        var refusal = await Assert.ThrowsAsync<DomainException>(() => SetAsync(fixture, flow, "Sans version."));
        Assert.Equal((ErrorCodes.VersionRequired, RefusalKind.PreconditionRequired), (refusal.Code, refusal.Kind));
        Assert.Empty(fixture.Database.FlowComments);
    }

    [Theory]
    [InlineData("<b>gras</b>", "fr", ErrorCodes.HtmlForbidden)]
    [InlineData("Une phrase.", "de", ErrorCodes.InvalidLanguage)]
    [InlineData("Une phrase.", null, ErrorCodes.InvalidLanguage)]
    [InlineData("", null, ErrorCodes.InvalidLanguage)]
    public async Task An_invalid_comment_is_refused_and_writes_nothing(string text, string? language, string code)
    {
        var fixture = new UsersFixture();
        var flow = NewFlow(fixture);
        var refusal = await Assert.ThrowsAsync<DomainException>(() => SetAsync(fixture, flow, text, language));
        Assert.Equal(code, refusal.Code);
        Assert.Empty(fixture.Database.FlowComments);
    }

    [Fact]
    public async Task A_comment_of_500_characters_is_kept_and_one_of_501_is_refused()
    {
        var fixture = new UsersFixture();
        var flow = NewFlow(fixture);
        Assert.Equal(PresentationCommentRules.TextMax, (await SetAsync(fixture, flow, new string('a', 500))).Single().Text.Length);
        var refusal = await Assert.ThrowsAsync<DomainException>(() => SetAsync(fixture, flow, new string('b', 501), "fr", 1));
        Assert.Equal((ErrorCodes.TextTooLong, (object?)500), (refusal.Code, refusal.Details["max"]));
    }

    [Fact]
    public async Task The_lines_of_a_comment_keep_their_breaks_whatever_the_line_ends_given()
    {
        var fixture = new UsersFixture();
        var flow = NewFlow(fixture);
        var lines = await SetAsync(fixture, flow, "Première phrase.\r\nSeconde phrase.\rTroisième.");
        Assert.Equal("Première phrase.\nSeconde phrase.\nTroisième.", lines.Single().Text);
    }

    [Fact]
    public async Task Only_a_facilitator_writes_a_comment_a_reader_reads_them_and_a_stranger_finds_nothing()
    {
        var fixture = new UsersFixture();
        var flow = NewFlow(fixture);
        await SetAsync(fixture, flow, "Commentaire.");
        foreach (var member in new[] { fixture.Project.Contributor, fixture.Project.Reader })
        {
            fixture.ActingAs(member);
            var refused = await Assert.ThrowsAsync<DomainException>(() => SetAsync(fixture, flow, "Autre.", "fr", 1));
            Assert.Equal(ErrorCodes.AccessDenied, refused.Code);
        }
        Assert.Equal([new FlowPresentationCommentView(flow, "Commentaire.", "fr", "manual", 1)], await ReadAsync(fixture, fixture.Project.RootMapId));
        fixture.ActingAs(fixture.Project.Stranger);
        Assert.Equal(ErrorCodes.NotFound, (await Assert.ThrowsAsync<DomainException>(() => ReadAsync(fixture, fixture.Project.RootMapId))).Code);
        Assert.Equal(ErrorCodes.NotFound, (await Assert.ThrowsAsync<DomainException>(() => SetAsync(fixture, flow, "Autre.", "fr", 1))).Code);
    }

    [Fact]
    public async Task A_flow_that_does_not_exist_takes_no_comment()
    {
        var fixture = new UsersFixture();
        var step = fixture.Project.Step(0);
        // The identifier of a step is no flow: the comment of a step has its own route.
        Assert.Equal(ErrorCodes.NotFound, (await Assert.ThrowsAsync<DomainException>(() => SetAsync(fixture, step, "Sur une étape.", "fr", 0))).Code);
        Assert.Equal(ErrorCodes.NotFound, (await Assert.ThrowsAsync<DomainException>(() => SetAsync(fixture, fixture.Project.NextId(), "Nulle part.", "fr", 0))).Code);
        Assert.Empty(fixture.Database.FlowComments);
    }

    [Fact]
    public async Task A_map_reads_every_line_of_the_comments_of_its_live_flows_only_in_a_stable_order()
    {
        var fixture = new UsersFixture();
        var (first, second, third) = (fixture.Project.Step(0), fixture.Project.Step(1), fixture.Project.Step(2));
        var kept = fixture.Project.Flow(first, second);
        var deleted = fixture.Project.Flow(second, third);
        var (subMap, subLane) = fixture.Project.SubMap(first);
        var below = fixture.Project.Flow(fixture.Project.Step(0, laneId: subLane, mapId: subMap), fixture.Project.Step(1, laneId: subLane, mapId: subMap), subMap);
        await SetAsync(fixture, kept, "Sur la carte.");
        await SetAsync(fixture, kept, "On the map.", "en", 0);
        await SetAsync(fixture, deleted, "Flèche supprimée ensuite.");
        await SetAsync(fixture, below, "Dans le détail.");
        PutInTrash(fixture, deleted);
        Assert.Equal([(kept, "en"), (kept, "fr")], (await ReadAsync(fixture, fixture.Project.RootMapId)).Select(comment => (comment.FlowId, comment.Language)));
        Assert.Equal([below], (await ReadAsync(fixture, subMap)).Select(comment => comment.FlowId));
    }

    [Fact]
    public async Task A_flow_in_the_trash_keeps_its_comment_out_of_reach_and_finds_it_again_when_it_is_restored()
    {
        var fixture = new UsersFixture();
        var flow = NewFlow(fixture);
        await SetAsync(fixture, flow, "Le dossier change de service.");
        await SetAsync(fixture, flow, "The file changes department.", "en", 0);
        var live = fixture.Database.Get<Flow>(Flow.KeyOf(flow));
        PutInTrash(fixture, flow);
        // In the trash: its comment is neither read nor written, and stays as it was.
        Assert.Empty(await ReadAsync(fixture, fixture.Project.RootMapId));
        Assert.Equal(ErrorCodes.NotFound, (await Assert.ThrowsAsync<DomainException>(() => SetAsync(fixture, flow, "Autre.", "fr", 1))).Code);
        Assert.Equal(ErrorCodes.NotFound, (await Assert.ThrowsAsync<DomainException>(() => SetAsync(fixture, flow, "", "fr", 1))).Code);
        Assert.Equal(["en", "fr"], fixture.Database.FlowCommentsOf(flow).Keys.Order());
        // Restored, the flow has its comment again, at the versions it had.
        fixture.Database.Add(live);
        Assert.Equal([("en", "The file changes department.", 1), ("fr", "Le dossier change de service.", 1)],
            (await ReadAsync(fixture, fixture.Project.RootMapId)).Select(comment => (comment.Language, comment.Text, comment.Version)));
        Assert.Equal(2, (await SetAsync(fixture, flow, "Le dossier change de service, relu.", "fr", 1)).Single(line => line.Language == "fr").Version);
    }

    [Fact]
    public async Task A_comment_follows_its_flow_when_the_flow_goes_to_another_map()
    {
        var fixture = new UsersFixture();
        var parent = fixture.Project.Step(0);
        var (source, target) = (fixture.Project.Step(1), fixture.Project.Step(2));
        var flow = fixture.Project.Flow(source, target);
        var (subMap, subLane) = fixture.Project.SubMap(parent);
        await SetAsync(fixture, flow, "Suit la flèche.");
        foreach (var step in new[] { source, target })
            fixture.Database.Add(fixture.Database.Get<Step>(Step.KeyOf(step)) with { MapId = subMap, LaneId = subLane });
        fixture.Database.Add(fixture.Database.Get<Flow>(Flow.KeyOf(flow)) with { MapId = subMap });
        Assert.Empty(await ReadAsync(fixture, fixture.Project.RootMapId));
        Assert.Equal([flow], (await ReadAsync(fixture, subMap)).Select(comment => comment.FlowId));
    }

    [Fact]
    public async Task The_comments_of_the_steps_and_of_the_flows_of_a_map_are_kept_and_read_apart()
    {
        var fixture = new UsersFixture();
        var (source, target) = (fixture.Project.Step(0), fixture.Project.Step(1));
        var flow = fixture.Project.Flow(source, target);
        await fixture.Runner.RunAsync(SetPresentationCommentRequest.WithVersion(source, "Sur l'étape.", "fr", 0), Token);
        await SetAsync(fixture, flow, "Sur la flèche.", "fr", 0);
        Assert.Equal([(flow, "Sur la flèche.")], (await ReadAsync(fixture, fixture.Project.RootMapId)).Select(comment => (comment.FlowId, comment.Text)));
        var steps = await fixture.Runner.RunAsync(new ReadPresentationCommentsQuery(fixture.Project.RootMapId), Token);
        Assert.Equal([(source, "Sur l'étape.")], steps.Select(comment => (comment.StepId, comment.Text)));
        // The empty original of the step removes the comment of the step, never the one of the flow that leaves it.
        await fixture.Runner.RunAsync(SetPresentationCommentRequest.WithVersion(source, "", "fr", 1), Token);
        Assert.Equal([(flow, "fr")], fixture.Database.FlowComments.Keys);
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
