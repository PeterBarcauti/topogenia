// PresentationVoiceDoubles.cs
// The voices of the projects in memory (ADR 103): every voice read for a project, one line written in a unit only, the version read of it checked as the persistence does.

using Topogenia.Application.Presentation;
using Topogenia.Domain.Errors;

namespace Topogenia.Application.Tests.Support;

/// <summary>
/// <see cref="IPresentationVoiceStore"/> on the <see cref="InMemoryDatabase"/>, as
/// <see cref="InMemoryPresentationCommentStore"/> keeps the comments of the steps: rolled back with its unit; a version
/// read that is no longer the one kept is CONFLIT_VERSION, as the concurrency token of the table refuses it, and the line
/// is then left as it was; an import adds only lines that are not there.
/// </summary>
public sealed class InMemoryPresentationVoiceStore(InMemoryDatabase database) : IPresentationVoiceStore
{
    /// <summary>The number of calls that wrote something.</summary>
    public int Writes { get; private set; }

    public Task<IReadOnlyList<PresentationVoice>> OfProjectAsync(Guid projectId, CancellationToken cancellationToken)
    {
        IReadOnlyList<PresentationVoice> found = [.. database.Voices.Values.Where(voice => voice.ProjectId == projectId)];
        return Task.FromResult(found);
    }

    public Task WriteAsync(VoiceWrite write, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(write);
        if (!database.InTransaction)
            throw new InvalidOperationException("The voice of a project is written in a unit of work.");
        if (write.Line is { } line && (line.ProjectId, line.Language) != (write.ProjectId, write.Language))
            throw new ArgumentException("A voice is written on its own project and language.", nameof(write));
        var kept = database.Voices.GetValueOrDefault((write.ProjectId, write.Language));
        if ((kept?.Version ?? 0) != write.VersionRead)
        {
            throw DomainException.Conflict(ErrorCodes.VersionConflict, new Dictionary<string, object?>
            {
                ["object"] = "presentation_voice", ["updated_by"] = kept?.UpdatedBy, ["updated_at"] = kept?.UpdatedAt,
            });
        }
        database.PutVoice(write.ProjectId, write.Language, write.Line);
        Writes++;
        return Task.CompletedTask;
    }

    public Task AddAsync(IReadOnlyList<PresentationVoice> voices, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(voices);
        if (!database.InTransaction)
            throw new InvalidOperationException("The voices of an import are added in its unit of work.");
        foreach (var voice in voices)
        {
            if (database.Voices.ContainsKey((voice.ProjectId, voice.Language)))
                throw new InvalidOperationException("A project keeps one voice per language at most.");
            database.PutVoice(voice.ProjectId, voice.Language, voice);
        }
        if (voices.Count > 0)
            Writes++;
        return Task.CompletedTask;
    }
}
