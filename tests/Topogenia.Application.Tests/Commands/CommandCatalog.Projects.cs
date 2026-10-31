// CommandCatalog.Projects.cs
// Rights of the commands and queries of the projects, members, trash, profile and exchange, and of the user requests (annex A 1, spec 3.0 section 4).

using Topogenia.Application.Assistant;
using Topogenia.Application.Audit;
using Topogenia.Application.Exchange;
using Topogenia.Application.Members;
using Topogenia.Application.Profile;
using Topogenia.Application.Projects;
using Topogenia.Application.Trash;

namespace Topogenia.Application.Tests.Commands;

internal static partial class CommandCatalog
{
    internal static readonly Dictionary<Type, string> ProjectsAndMembers = new()
    {
        [typeof(GetProjectQuery)] = "reader",
        [typeof(ReplaceMembersCommand)] = "facilitator",
        [typeof(GetParticipantsQuery)] = "reader",
        [typeof(ChangeSharingGroupLabelCommand)] = "facilitator",
        [typeof(ChangeClassificationCommand)] = "facilitator",
        [typeof(ChangeWorkTimeCommand)] = "facilitator",
        [typeof(ChangeTranslationGlossaryCommand)] = "facilitator",
        [typeof(MoveToTrashCommand)] = "facilitator",
        [typeof(RestoreProjectCommand)] = "facilitator",
        [typeof(ExportPackageQuery)] = "facilitator",
        [typeof(PmapCopyQuery)] = "reader",
        // The context of an exchange of the panel (3.0, lot 2): any member asks and analyses; a proposal needs the facilitator, checked by the exchange.
        [typeof(AssistantContextQuery)] = "reader",
        // A proposal of the assistant on the map open (3.0, lot 3): its preview and its application change the structure.
        [typeof(PreviewProposalQuery)] = "facilitator",
        [typeof(ApplyProposalCommand)] = "facilitator",
    };

    /// <summary>
    /// The requests run by `UserRunner` (no existing project, or one acted on outside the command template), with the right
    /// of their route; read by `Users.UserCatalogTests` (a read-only dictionary, so that `CommandCatalogTests` keeps to the
    /// commands and queries of a project).
    /// </summary>
    internal static readonly IReadOnlyDictionary<Type, string> UserRequests = new Dictionary<Type, string>
    {
        [typeof(CreateProjectCommand)] = "admin",
        [typeof(ListProjectsQuery)] = "authenticated",
        [typeof(ListTrashQuery)] = "authenticated",
        [typeof(PurgeProjectCommand)] = "admin",
        [typeof(GetProfileQuery)] = "authenticated",
        [typeof(ChangeProfileCommand)] = "authenticated",
        [typeof(RememberOpenedProjectCommand)] = "reader",
        [typeof(ReadAuditQuery)] = "admin",
        [typeof(PreviewImportQuery)] = "admin",
        [typeof(ImportProjectCommand)] = "admin",
        [typeof(PreviewReplacementQuery)] = "facilitator",
        [typeof(ReplaceProjectCommand)] = "facilitator",
        [typeof(OpenPmapCommand)] = "admin",
        [typeof(DuplicateProjectCommand)] = "admin",
        [typeof(ReadAssistantSettingsQuery)] = "admin",
        [typeof(ChangeAssistantSettingsCommand)] = "admin",
        [typeof(TestAssistantConnectionCommand)] = "admin",
    };
}
