// IWorkspaceStore.cs
// Port that loads the workspace of a project and writes the change set of an operation (ADR 015, spec 3.4).

using Topogenia.Domain.Workspace;

namespace Topogenia.Application.Ports;

/// <summary>The groups of records that an operation needs in its workspace besides the structure (ADR 015).</summary>
[Flags]
public enum WorkspaceContent
{
    /// <summary>
    /// The structure only, always loaded: project, counters, deletion batches, maps, lanes, steps, flows, RACI roles and
    /// dissolutions.
    /// </summary>
    None = 0,

    /// <summary>Sheets, their procedure and their validated versions.</summary>
    Sheets = 1,

    /// <summary>Documents, uses, pain points and their status history.</summary>
    Documents = 2,

    /// <summary>Translations of the texts of the project.</summary>
    Translations = 4,

    /// <summary>Members of the project and the users they name.</summary>
    Members = 8,

    All = Sheets | Documents | Translations | Members,
}

/// <summary>
/// Loads the records of one project into a <see cref="ProjectWorkspace"/> and writes back the <see cref="ChangeSet"/> of
/// an operation, inside the transaction of the current <see cref="IUnitOfWork"/>. The persistence implements it (P3-T04
/// for the writing, with the two-step parking of the steps whose cell changes, ADR 016; P3-T05 for the loading).
/// </summary>
public interface IWorkspaceStore
{
    /// <summary>
    /// The workspace of a project with the structure, every counter of the project (architecture.md 3) and the asked
    /// groups; null when the project does not exist.
    /// </summary>
    Task<ProjectWorkspace?> LoadAsync(Guid projectId, WorkspaceContent content, CancellationToken cancellationToken);

    /// <summary>
    /// Writes the changes of an operation: creations, modifications (checked against the version of the record before,
    /// D06) and physical removals. A version that changed since the load is refused as a conflict.
    /// </summary>
    Task SaveAsync(ChangeSet changes, CancellationToken cancellationToken);
}
