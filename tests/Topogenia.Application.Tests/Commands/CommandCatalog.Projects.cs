// CommandCatalog.Projects.cs
// Rights of the commands and queries of the projects, members, trash, profile and exchange, and of the user requests (annex A 1, spec 3.0 section 4).

using Topogenia.Application.Assistant;
using Topogenia.Application.Audit;
using Topogenia.Application.Exchange;
using Topogenia.Application.Members;
using Topogenia.Application.Profile;
using Topogenia.Application.Projects;
using Topogenia.Application.Snapshots;
using Topogenia.Application.Summaries;
using Topogenia.Application.Trash;

namespace Topogenia.Application.Tests.Commands;

internal static partial class CommandCatalog
{
    internal static readonly Dictionary<Type, string> ProjectsAndMembers = new()
    {
        [typeof(GetProjectQuery)] = "reader",
        // The search of a word in the whole project (3.4, lot 2, ADR 110): what the member reads.
        [typeof(Topogenia.Application.Search.ProjectSearchQuery)] = "reader",
        [typeof(ReplaceMembersCommand)] = "facilitator",
        [typeof(GetParticipantsQuery)] = "reader",
        [typeof(ChangeSharingGroupLabelCommand)] = "facilitator",
        [typeof(ChangeClassificationCommand)] = "facilitator",
        [typeof(ChangeWorkTimeCommand)] = "facilitator",
        [typeof(ChangeTranslationGlossaryCommand)] = "facilitator",
        [typeof(ChangeOriginLanguageCommand)] = "facilitator",
        [typeof(MoveToTrashCommand)] = "facilitator",
        [typeof(RestoreProjectCommand)] = "facilitator",
        [typeof(ExportPackageQuery)] = "facilitator",
        [typeof(PmapCopyQuery)] = "reader",
        // The copy of one map and of the maps below it that an image SVG carries (plan 3.1, L7).
        [typeof(MapPmapQuery)] = "reader",
        [typeof(WriteMapSummariesCommand)] = "facilitator",
        // The photos of the indicators (Makigami M8): read by every member, taken and deleted by a facilitator.
        [typeof(SnapshotsQuery)] = "reader",
        [typeof(TakeSnapshotCommand)] = "facilitator",
        [typeof(DeleteSnapshotCommand)] = "facilitator",
        // The context of an exchange of the panel (3.0, lot 2): any member asks and analyses; a proposal needs the facilitator, checked by the exchange.
        [typeof(AssistantContextQuery)] = "reader",
        // A proposal of the assistant on the map open (3.0, lot 3): its preview and its application change the structure.
        [typeof(PreviewProposalQuery)] = "facilitator",
        [typeof(ApplyProposalCommand)] = "facilitator",
        // A .pmap file inserted as a detailed step, or replacing the detail of a step (3.3, F-208, ADR 108): the structure changes.
        [typeof(PmapInsertionPreviewQuery)] = "facilitator",
        [typeof(InsertPmapCommand)] = "facilitator",
        [typeof(ReplaceDetailCommand)] = "facilitator",
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
        [typeof(ProposeProjectCodeQuery)] = "admin",
        [typeof(FreeProjectCodeQuery)] = "admin",
        [typeof(ListTrashQuery)] = "authenticated",
        [typeof(PurgeProjectCommand)] = "admin",
        [typeof(GetProfileQuery)] = "authenticated",
        [typeof(ChangeProfileCommand)] = "authenticated",
        [typeof(RememberOpenedProjectCommand)] = "reader",
        [typeof(ReadAuditQuery)] = "admin",
        [typeof(PreviewImportQuery)] = "admin",
        [typeof(ImportProjectCommand)] = "admin",
        // The target state of a project and its reference (Makigami M6): an import, as « Dupliquer » ; its reference read again
        // by a facilitator of the target (choice of the client, P3e), where its source stands read by every member.
        [typeof(CreateTargetStateCommand)] = "admin",
        [typeof(RefreshBaselineCommand)] = "facilitator",
        [typeof(BaselineSourceQuery)] = "reader",
        [typeof(PreviewReplacementQuery)] = "facilitator",
        [typeof(ReplaceProjectCommand)] = "facilitator",
        [typeof(OpenPmapCommand)] = "admin",
        // The preview of a template collection (3.4, lot 6, ADR 114): read only, as the opening of a file it precedes.
        [typeof(PreviewCollectionQuery)] = "admin",
        [typeof(DuplicateProjectCommand)] = "admin",
        [typeof(ReadAssistantSettingsQuery)] = "admin",
        [typeof(ChangeAssistantSettingsCommand)] = "admin",
        [typeof(TestAssistantConnectionCommand)] = "admin",
        // The comments of presentation, kept beside the closed core (ADR 099): read with the map, written by the facilitator.
        [typeof(Topogenia.Application.Presentation.ReadPresentationCommentsQuery)] = "reader",
        [typeof(Topogenia.Application.Presentation.SetPresentationCommentRequest)] = "facilitator",
        // The comments of presentation of the flows (ADR 099, lot J10): the rights of those of the steps.
        [typeof(Topogenia.Application.Presentation.ReadFlowPresentationCommentsQuery)] = "reader",
        [typeof(Topogenia.Application.Presentation.SetFlowPresentationCommentRequest)] = "facilitator",
        [typeof(Topogenia.Application.Presentation.WriteCommentDraftsRequest)] = "facilitator",
        // The voices a project keeps for its presentation (ADR 103): read with the project, chosen by the facilitator.
        [typeof(Topogenia.Application.Presentation.ReadPresentationVoicesQuery)] = "reader",
        [typeof(Topogenia.Application.Presentation.SetPresentationVoiceRequest)] = "facilitator",
    };
}
