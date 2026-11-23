// PmapBuild.Extras.cs
// What a whole copy adds to a new project (1.5, ADR 105): the glossary set by the rule of the domain, and the lines of the comments of presentation of its new steps and flows, which the application writes beside the core.

using Topogenia.Application.Presentation;
using Topogenia.Domain.Model;
using Topogenia.Domain.Projects;
using Topogenia.Domain.Workspace;

namespace Topogenia.Interchange.Pmap;

public static partial class PmapBuild
{
    /// <summary>The project with the glossary of the file (1.5), set by the rule of the setting (a fault of the file at `project` otherwise).</summary>
    private static Project WithGlossary(Project project, PmapProject file) =>
        file.Glossary is not { } glossary ? project
            : Builder.Rule("project", () => ProjectSettings.ChangeTranslationGlossary(new ProjectWorkspace(project.Id, [project]), glossary));

    private sealed partial class Builder
    {
        /// <summary>The flows of the file with the identifier each one received, for their comments.</summary>
        private readonly List<(PmapFlow Flow, Guid Id)> builtFlows = [];

        /// <summary>The lines of the comments of the new steps.</summary>
        public IReadOnlyList<PresentationComment> StepComments() =>
            [.. built.Where(item => item.Step.Comment is not null).SelectMany(item => Lines(item.Id, item.Step.Comment!)).Select(PresentationComment.Of)];

        /// <summary>The lines of the comments of the new flows.</summary>
        public IReadOnlyList<FlowPresentationComment> FlowComments() =>
            [.. builtFlows.Where(item => item.Flow.Comment is not null).SelectMany(item => Lines(item.Id, item.Flow.Comment!)).Select(FlowPresentationComment.Of)];

        /// <summary>
        /// The lines of a comment on a new step or flow, typed by the person who opens the file (as the translations of the
        /// core a file brings): its text in the language of the file, the language of origin of every step it builds, and
        /// its translations; a translation in that language gives way to the text.
        /// </summary>
        private IEnumerable<CommentLine> Lines(Guid subject, PmapComment comment)
        {
            var lines = comment.Translations.Where(pair => comment.Text is null || pair.Key != language).Select(pair => (Language: pair.Key, Text: pair.Value));
            if (comment.Text is { } text)
                lines = lines.Prepend((language, text));
            return lines.Select(line => new CommentLine(subject, line.Text, line.Language, CommentSource.Manual, 1, context.Now, context.ActorId));
        }
    }
}
