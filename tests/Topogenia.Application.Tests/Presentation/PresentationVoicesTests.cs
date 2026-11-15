// PresentationVoicesTests.cs
// The voice of a project (ADR 103): one line per language, kept by a facilitator with the version read of its line, removed by an empty name, read by the readers of the project, out of the history and of the audit.

using Topogenia.Application.Presentation;
using Topogenia.Application.Tests.Support;
using Topogenia.Application.Tests.Users;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;
using Topogenia.Domain.Values;

namespace Topogenia.Application.Tests.Presentation;

public sealed class PresentationVoicesTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private const string Hortense = "Hortense - voix du poste";

    private static Task<IReadOnlyList<PresentationVoiceView>> SetAsync(UsersFixture fixture, string? language, string? name, bool? local = true, int? version = null,
        Guid? projectId = null) =>
        fixture.Runner.RunAsync(SetPresentationVoiceRequest.WithVersion(projectId ?? fixture.Project.ProjectId, language, name, local, version), Token);

    private static Task<IReadOnlyList<PresentationVoiceView>> ReadAsync(UsersFixture fixture, Guid? projectId = null) =>
        fixture.Runner.RunAsync(new ReadPresentationVoicesQuery(projectId ?? fixture.Project.ProjectId), Token);

    [Fact]
    public async Task A_facilitator_keeps_the_voice_of_a_language_trimmed_with_its_author_and_version_1()
    {
        var fixture = new UsersFixture();
        var voices = await SetAsync(fixture, "fr", $"  {Hortense}  ", true, 0);
        Assert.Equal([new PresentationVoiceView("fr", Hortense, true, 1)], voices);
        var kept = fixture.Database.Voices[(fixture.Project.ProjectId, "fr")];
        Assert.Equal((Hortense, true, 1, TestProject.Now, fixture.Project.Facilitator.Id), (kept.Name, kept.Local, kept.Version, kept.UpdatedAt, kept.UpdatedBy));
        Assert.Equal(1, fixture.Database.Commits);
    }

    [Fact]
    public async Task Each_language_keeps_its_own_voice_and_the_reply_gives_every_voice_of_the_project_by_language()
    {
        var fixture = new UsersFixture();
        await SetAsync(fixture, "fr", Hortense, true, 0);
        var voices = await SetAsync(fixture, "en", "Julie", false, 0);
        Assert.Equal([new PresentationVoiceView("en", "Julie", false, 1), new PresentationVoiceView("fr", Hortense, true, 1)], voices);
        Assert.Equal(voices, await ReadAsync(fixture));
    }

    [Fact]
    public async Task A_voice_chosen_again_moves_on_by_one_version_and_the_same_voice_already_kept_writes_nothing()
    {
        var fixture = new UsersFixture();
        await SetAsync(fixture, "fr", Hortense, true, 0);
        var changed = await SetAsync(fixture, "fr", "Julie", true, 1);
        Assert.Equal(new PresentationVoiceView("fr", "Julie", true, 2), changed.Single());
        var (writes, kept) = (fixture.Voices.Writes, fixture.Database.Voices[(fixture.Project.ProjectId, "fr")]);
        Assert.Equal(changed, await SetAsync(fixture, "fr", " Julie ", true, 2));
        Assert.Equal((writes, kept), (fixture.Voices.Writes, fixture.Database.Voices[(fixture.Project.ProjectId, "fr")]));
        // The same name read online is another voice.
        Assert.Equal(new PresentationVoiceView("fr", "Julie", false, 3), (await SetAsync(fixture, "fr", "Julie", false, 2)).Single());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task An_empty_or_absent_name_removes_the_voice_of_the_language_only_and_nothing_when_it_has_none(string? name)
    {
        var fixture = new UsersFixture();
        await SetAsync(fixture, "fr", Hortense, true, 0);
        await SetAsync(fixture, "en", "Julie", false, 0);
        // The removal says nothing of where the voice is read: `local` is not asked.
        Assert.Equal([new PresentationVoiceView("en", "Julie", false, 1)], await SetAsync(fixture, "fr", name, null, 1));
        Assert.Equal([(fixture.Project.ProjectId, "en")], fixture.Database.Voices.Keys);
        var writes = fixture.Voices.Writes;
        Assert.Equal([new PresentationVoiceView("en", "Julie", false, 1)], await SetAsync(fixture, "fr", name, null, 0));
        Assert.Equal(writes, fixture.Voices.Writes);
    }

    [Fact]
    public async Task A_voice_changed_since_the_version_read_is_refused_and_kept_as_it_was()
    {
        var fixture = new UsersFixture();
        await SetAsync(fixture, "fr", Hortense, true, 0);
        var refusal = await Assert.ThrowsAsync<DomainException>(() => SetAsync(fixture, "fr", "Julie", true, 0));
        Assert.Equal((ErrorCodes.VersionConflict, RefusalKind.Conflict, "presentation_voice", (object?)fixture.Project.Facilitator.Id, (object?)TestProject.Now),
            (refusal.Code, refusal.Kind, refusal.Details["object"], refusal.Details["updated_by"], refusal.Details["updated_at"]));
        // A removal with a version that is no longer kept is refused too, and so is a write on a voice removed meanwhile.
        Assert.Equal(ErrorCodes.VersionConflict, (await Assert.ThrowsAsync<DomainException>(() => SetAsync(fixture, "fr", null, null, 2))).Code);
        Assert.Equal((Hortense, 1), (fixture.Database.Voices[(fixture.Project.ProjectId, "fr")].Name, fixture.Database.Voices[(fixture.Project.ProjectId, "fr")].Version));
        await SetAsync(fixture, "fr", null, null, 1);
        var gone = await Assert.ThrowsAsync<DomainException>(() => SetAsync(fixture, "fr", "Julie", true, 1));
        Assert.Equal((ErrorCodes.VersionConflict, (object?)null), (gone.Code, gone.Details["updated_by"]));
        Assert.Empty(fixture.Database.Voices);
    }

    [Fact]
    public async Task The_version_read_is_the_one_of_the_voice_of_the_language_written()
    {
        var fixture = new UsersFixture();
        await SetAsync(fixture, "fr", Hortense, true, 0);
        await SetAsync(fixture, "fr", "Julie", true, 1);
        // The French voice is at its version 2: the English one, which does not exist yet, is at 0.
        Assert.Equal(ErrorCodes.VersionConflict, (await Assert.ThrowsAsync<DomainException>(() => SetAsync(fixture, "en", "Julie", false, 2))).Code);
        Assert.Equal(2, (await SetAsync(fixture, "en", "Julie", false, 0)).Count);
    }

    [Fact]
    public async Task A_voice_without_the_version_its_mode_requires_is_refused_428_and_writes_nothing()
    {
        var fixture = new UsersFixture();
        fixture.Project.Versions.IsMissing = true;
        var refusal = await Assert.ThrowsAsync<DomainException>(() => SetAsync(fixture, "fr", Hortense));
        Assert.Equal((ErrorCodes.VersionRequired, RefusalKind.PreconditionRequired), (refusal.Code, refusal.Kind));
        Assert.Empty(fixture.Database.Voices);
        // Where the mode does not require it, a write without a version read is written over whatever is kept.
        fixture.Project.Versions.IsMissing = false;
        await SetAsync(fixture, "fr", Hortense);
        Assert.Equal(2, (await SetAsync(fixture, "fr", "Julie")).Single().Version);
    }

    [Theory]
    [InlineData("de", "Julie", true, ErrorCodes.InvalidLanguage, "language")]
    [InlineData(null, "Julie", true, ErrorCodes.InvalidLanguage, "language")]
    [InlineData("FR", "Julie", true, ErrorCodes.InvalidLanguage, "language")]
    [InlineData("de", "", null, ErrorCodes.InvalidLanguage, "language")]
    [InlineData("fr", "<b>Julie</b>", true, ErrorCodes.HtmlForbidden, "name")]
    [InlineData("fr", "Julie\nHortense", true, ErrorCodes.InvalidValue, "name")]
    [InlineData("fr", "Julie\tHortense", true, ErrorCodes.InvalidValue, "name")]
    [InlineData("fr", "Julie\u0007", true, ErrorCodes.InvalidValue, "name")]
    [InlineData("fr", "Julie", null, ErrorCodes.RequiredField, "local")]
    public async Task An_invalid_voice_is_refused_on_its_field_and_writes_nothing(string? language, string name, bool? local, string code, string field)
    {
        var fixture = new UsersFixture();
        var refusal = await Assert.ThrowsAsync<DomainException>(() => SetAsync(fixture, language, name, local, 0));
        Assert.Equal((code, (object?)field), (refusal.Code, refusal.Details["field"]));
        Assert.Empty(fixture.Database.Voices);
    }

    [Fact]
    public async Task A_name_of_200_characters_is_kept_and_one_of_201_is_refused()
    {
        var fixture = new UsersFixture();
        Assert.Equal(PresentationVoiceRules.NameMax, (await SetAsync(fixture, "fr", new string('v', 200), true, 0)).Single().Name.Length);
        var refusal = await Assert.ThrowsAsync<DomainException>(() => SetAsync(fixture, "fr", new string('w', 201), true, 1));
        Assert.Equal((ErrorCodes.TextTooLong, (object?)"name", (object?)200), (refusal.Code, refusal.Details["field"], refusal.Details["max"]));
    }

    [Fact]
    public async Task Only_a_facilitator_keeps_a_voice_every_member_reads_them_and_a_stranger_finds_nothing()
    {
        var fixture = new UsersFixture();
        await SetAsync(fixture, "fr", Hortense, true, 0);
        foreach (var member in new[] { fixture.Project.Contributor, fixture.Project.Reader })
        {
            fixture.ActingAs(member);
            Assert.Equal(ErrorCodes.AccessDenied, (await Assert.ThrowsAsync<DomainException>(() => SetAsync(fixture, "fr", "Julie", true, 1))).Code);
            Assert.Equal([new PresentationVoiceView("fr", Hortense, true, 1)], await ReadAsync(fixture));
        }
        fixture.ActingAs(fixture.Project.Stranger);
        Assert.Equal(ErrorCodes.NotFound, (await Assert.ThrowsAsync<DomainException>(() => ReadAsync(fixture))).Code);
        Assert.Equal(ErrorCodes.NotFound, (await Assert.ThrowsAsync<DomainException>(() => SetAsync(fixture, "fr", "Julie", true, 1))).Code);
        // An administrator who is no member acts as a facilitator, as everywhere.
        fixture.ActingAs(fixture.Project.Admin);
        Assert.Equal(2, (await SetAsync(fixture, "fr", "Julie", true, 1)).Single().Version);
    }

    [Fact]
    public async Task A_project_in_the_trash_or_unknown_gives_and_takes_no_voice_and_keeps_those_it_had()
    {
        var fixture = new UsersFixture();
        await SetAsync(fixture, "fr", Hortense, true, 0);
        fixture.Project.PutInTrash();
        Assert.Equal(ErrorCodes.NotFound, (await Assert.ThrowsAsync<DomainException>(() => ReadAsync(fixture))).Code);
        Assert.Equal(ErrorCodes.NotFound, (await Assert.ThrowsAsync<DomainException>(() => SetAsync(fixture, "fr", "Julie", true, 1))).Code);
        Assert.Equal(ErrorCodes.NotFound, (await Assert.ThrowsAsync<DomainException>(() => SetAsync(fixture, "fr", null, null, 1))).Code);
        Assert.Equal(Hortense, fixture.Database.Voices[(fixture.Project.ProjectId, "fr")].Name);
        var unknown = fixture.Project.NextId();
        Assert.Equal(ErrorCodes.NotFound, (await Assert.ThrowsAsync<DomainException>(() => ReadAsync(fixture, unknown))).Code);
        Assert.Equal(ErrorCodes.NotFound, (await Assert.ThrowsAsync<DomainException>(() => SetAsync(fixture, "fr", "Julie", true, 0, unknown))).Code);
    }

    [Fact]
    public async Task The_voices_of_a_project_are_not_those_of_another()
    {
        var fixture = new UsersFixture();
        var other = fixture.OtherProject("AUT", fixture.Project.Facilitator, MemberRole.Facilitator);
        await SetAsync(fixture, "fr", Hortense, true, 0);
        await SetAsync(fixture, "fr", "Julie", false, 0, other);
        Assert.Equal([new PresentationVoiceView("fr", Hortense, true, 1)], await ReadAsync(fixture));
        Assert.Equal([new PresentationVoiceView("fr", "Julie", false, 1)], await ReadAsync(fixture, other));
        // Removing the voice of the one leaves the voice of the other.
        Assert.Empty(await SetAsync(fixture, "fr", "", null, 1));
        Assert.Equal([(other, "fr")], fixture.Database.Voices.Keys);
    }

    /// <summary>A voice is no operation of a map nor an event of the audit: Ctrl+Z does not undo it, the project and its maps are left as they were.</summary>
    [Fact]
    public async Task A_voice_kept_goes_under_the_lock_of_the_project_and_leaves_the_history_the_audit_and_the_records_as_they_were()
    {
        var fixture = new UsersFixture();
        var before = fixture.Project.Project;
        await SetAsync(fixture, "fr", Hortense, true, 0);
        Assert.Single(fixture.Database.Voices);
        await SetAsync(fixture, "fr", null, null, 1);
        Assert.Empty(fixture.Database.Voices);
        Assert.Equal(["access", "lock", "access", "lock"], fixture.Database.Calls);
        Assert.Empty(fixture.Database.Entries);
        Assert.Empty(fixture.Database.All<AuditEvent>());
        // The lock alone moved the project on: its version and its author are those of before.
        Assert.Equal(before with { StructureVersion = before.StructureVersion + 2 }, fixture.Project.Project);
    }
}
