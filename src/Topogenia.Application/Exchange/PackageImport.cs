// PackageImport.cs
// The import of an exchange package: preview and import as a new project, preview and replacement of a project, the right checked before the package is received (spec 7.3, ADR 052).

using System.Text.Json;
using Topogenia.Application.Audit;
using Topogenia.Application.Authorization;
using Topogenia.Application.Commands;
using Topogenia.Application.Ports;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Projects;
using Topogenia.Domain.Snapshots;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;

namespace Topogenia.Application.Exchange;

/// <summary>Whether the code a package would take is free, the code of a live project, or of a project in the trash.</summary>
public enum CodeState { Free, Taken, InTrash }

/// <summary>
/// The preview of an import (spec 7.3): the code of the package and the code the project would take, whether it is free,
/// whether the package holds the people, what it holds, the logins it names that nobody has here, the members the project
/// would have, what the import would settle and, for a replacement, the code the replaced project would take in the trash.
/// </summary>
public sealed record ImportPreview(
    string PackageCode,
    string ProjectCode,
    CodeState CodeState,
    bool WithPeople,
    PackageCounts Counts,
    IReadOnlyList<string> UnknownLogins,
    int Members,
    ImportSettlement Settlement,
    string? TrashCode);

/// <summary>A project imported: what the reply 201 and its `Location` name, the code given to a replaced project, the users created.</summary>
public sealed record ImportedProject(Guid Id, string Code, Guid RootMapId, string? TrashCode, IReadOnlyList<Guid> CreatedUserIds);

/// <summary>
/// Runs the four requests of the import, each one reading the package itself, once its right is granted. Given the reader
/// of the body that carries the package, the right is granted before the body is received (audit 3.2.0): a refused user
/// never makes the server receive a package, nor open an archive; the request checks its right again when it runs.
/// </summary>
public sealed class PackageImport(UserRunner runner, IPackageImporter importer)
{
    public Task<ImportPreview> PreviewAsync(byte[] content, string? code, bool withMembers, CancellationToken cancellationToken) =>
        PreviewAsync(Given(content), code, withMembers, cancellationToken);

    public Task<ImportPreview> PreviewAsync(Func<CancellationToken, Task<byte[]>> body, string? code, bool withMembers, CancellationToken cancellationToken) =>
        RunAsync(source => new PreviewImportQuery(source, code, withMembers), body, cancellationToken);

    public Task<ImportedProject> ImportAsync(byte[] content, string? code, bool withMembers, CancellationToken cancellationToken) =>
        ImportAsync(Given(content), code, withMembers, cancellationToken);

    public Task<ImportedProject> ImportAsync(Func<CancellationToken, Task<byte[]>> body, string? code, bool withMembers, CancellationToken cancellationToken) =>
        RunAsync(source => new ImportProjectCommand(source, code, withMembers), body, cancellationToken);

    public Task<ImportPreview> PreviewReplacementAsync(Guid projectId, byte[] content, bool withMembers, CancellationToken cancellationToken) =>
        PreviewReplacementAsync(projectId, Given(content), withMembers, cancellationToken);

    public Task<ImportPreview> PreviewReplacementAsync(Guid projectId, Func<CancellationToken, Task<byte[]>> body, bool withMembers,
        CancellationToken cancellationToken) =>
        RunAsync(source => new PreviewReplacementQuery(projectId, source, withMembers), body, cancellationToken);

    public Task<ImportedProject> ReplaceAsync(Guid projectId, byte[] content, bool withMembers, int? version, CancellationToken cancellationToken) =>
        ReplaceAsync(projectId, Given(content), withMembers, version, cancellationToken);

    public Task<ImportedProject> ReplaceAsync(Guid projectId, Func<CancellationToken, Task<byte[]>> body, bool withMembers, int? version,
        CancellationToken cancellationToken) =>
        RunAsync(source => new ReplaceProjectCommand(projectId, source, withMembers, version), body, cancellationToken);

    private static Func<CancellationToken, Task<byte[]>> Given(byte[] content) => _ => Task.FromResult(content);

    /// <summary>The right of the request first, then the body received, then the request run, its right checked again.</summary>
    private async Task<TResult> RunAsync<TResult>(Func<PackageSource, UserRequest<TResult>> request, Func<CancellationToken, Task<byte[]>> body,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        var source = new PackageSource(importer);
        var built = request(source);
        await runner.DemandAsync(built, cancellationToken);
        source.Receive(await body(cancellationToken));
        return await runner.RunAsync(built, cancellationToken);
    }
}

/// <summary>
/// The bytes of a received package and their reading, done once, after the right of the request is granted (a refused
/// user never makes the server open an archive), and kept across the attempts of a unit of work. The bytes are received
/// once the right is granted too.
/// </summary>
public sealed class PackageSource(IPackageImporter importer)
{
    private byte[]? content;
    private IReadPackage? read;

    public IPackageImporter Importer { get; } = importer;

    public void Receive(byte[] bytes) => content = bytes ?? throw new ArgumentNullException(nameof(bytes));

    public IReadPackage Read() => read ??= Importer.Read(content ?? throw new InvalidOperationException("The package is read once it is received."));
}

/// <summary>`POST /api/projects/import/preview` (admin): everything the import does, but the writing (AR-38).</summary>
[RequiresAdmin]
public sealed class PreviewImportQuery(PackageSource source, string? code, bool withMembers) : UserRequest<ImportPreview>
{
    public override async Task<ImportPreview> RunAsync(UserScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var package = source.Read();
        var target = ProjectCode.Parse(code ?? package.ProjectCode).Value;
        var existing = await scope.Ports.Projects.CodesAsync(target, cancellationToken);
        var prepared = await ImportPreparation.PrepareAsync(scope, source.Importer, package, target, withMembers, null, cancellationToken);
        var state = existing.Count == 0 ? CodeState.Free : existing.Any(project => project.InTrash) ? CodeState.InTrash : CodeState.Taken;
        return await PackageImportReplies.PreviewAsync(scope, package, target, state, prepared, null, cancellationToken);
    }
}

/// <summary>
/// `POST /api/projects/import` (admin, like the creation of a project): a new project, new identifiers, the codes of the
/// package or, when its code is taken, a free <c>code</c> that rewrites every readable code (spec 7.3, S-7.3-04, 05). The
/// users, the project, its records and its audit event are written in one unit of work, all or nothing (S-7.3-09).
/// </summary>
[RequiresAdmin]
public sealed class ImportProjectCommand(PackageSource source, string? code, bool withMembers) : UserRequest<ImportedProject>
{
    public override bool InUnitOfWork => true;

    public override async Task<ImportedProject> RunAsync(UserScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var package = source.Read();
        var target = ProjectCode.Parse(code ?? package.ProjectCode);
        ProjectCodeRules.EnsureFree(target, await scope.Ports.Projects.CodesAsync(target.Value, cancellationToken));
        var prepared = await ImportPreparation.PrepareAsync(scope, source.Importer, package, target.Value, withMembers, null, cancellationToken);
        await PackageImportReplies.WriteAsync(scope, prepared, "new", withMembers, 0, cancellationToken);
        return PackageImportReplies.Imported(prepared, null);
    }
}

/// <summary>`POST /api/projects/{id}/import/preview` (facilitator of the replaced project): the replacement, but the writing.</summary>
[RequiresRole(MemberRole.Facilitator)]
public sealed class PreviewReplacementQuery(Guid projectId, PackageSource source, bool withMembers) : UserRequest<ImportPreview>
{
    public override AccessTarget? Target => AccessTarget.Project(projectId);

    public override async Task<ImportPreview> RunAsync(UserScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var package = source.Read();
        var replaced = await PackageImportReplies.ReplacedAsync(scope, cancellationToken);
        var trashCode = await PackageImportReplies.TrashCodeAsync(scope, replaced.Project.Code, cancellationToken);
        var prepared = await ImportPreparation.PrepareAsync(scope, source.Importer, package, replaced.Project.Code, withMembers, replaced,
            cancellationToken);
        return await PackageImportReplies.PreviewAsync(scope, package, replaced.Project.Code, CodeState.Taken, prepared, trashCode, cancellationToken);
    }
}

/// <summary>
/// `POST /api/projects/{id}/import` (facilitator of the replaced project, S-7.3-06): under the lock of the project, it
/// takes a free code derived from its own and goes to the trash with every readable code rewritten
/// (`ProjectReplacement.Retire`, restorable 30 days, S-7.3-07); the package takes its place under its code (S-7.3-08), with the
/// classification of the replaced project (AR-117) and its members when it brings none (AR-115). Both
/// are written in one unit of work, the replaced project first so that its codes are free, with their audit events, and
/// the target states that named the replaced project as their source name the new one (F-108). The
/// If-Match of the request is the version of the replaced project, as for its move to the trash (spec 5.3).
/// </summary>
[RequiresRole(MemberRole.Facilitator)]
public sealed class ReplaceProjectCommand(Guid projectId, PackageSource source, bool withMembers, int? version) : UserRequest<ImportedProject>
{
    public override AccessTarget? Target => AccessTarget.Project(projectId);

    public override bool InUnitOfWork => true;

    public override bool LocksTarget => true;

    public override async Task<ImportedProject> RunAsync(UserScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var package = source.Read();
        var replaced = await PackageImportReplies.ReplacedAsync(scope, cancellationToken);
        RecordVersion.Ensure(replaced.Project, version);
        var code = replaced.Project.Code;
        var trashCode = await PackageImportReplies.TrashCodeAsync(scope, code, cancellationToken);
        var prepared = await ImportPreparation.PrepareAsync(scope, source.Importer, package, code, withMembers, replaced, cancellationToken);
        // The target states of the replaced project follow it (F-108): read before anything is written.
        var targets = await scope.Ports.Projects.TargetsOfAsync(replaced.ProjectId, cancellationToken);
        ProjectReplacement.Retire(replaced, scope.Operation, trashCode);
        await scope.Ports.Store.SaveAsync(replaced.Commit(scope.Operation), cancellationToken);
        await scope.Ports.Store.SaveAsync(new ChangeSet([new RecordChange(ChangeKind.Created, null,
            AuditTrail.Event(scope.Operation, AuditKinds.ProjectTrashed, scope.User.Id, replaced.ProjectId))]), cancellationToken);
        await PackageImportReplies.WriteAsync(scope, prepared, "replace", withMembers, targets.Count, cancellationToken);
        await FollowAsync(scope, targets, replaced.ProjectId, prepared.Project.Id, cancellationToken);
        return PackageImportReplies.Imported(prepared, trashCode);
    }

    /// <summary>
    /// Each target state of the replaced project, live or in the trash, names the project that takes its place (F-108,
    /// `SnapshotRules.FollowReplacement`), in the same unit of work: the replacement and the links are written all or nothing.
    /// </summary>
    private static async Task FollowAsync(UserScope scope, IReadOnlyList<Guid> targets, Guid replacedId, Guid replacementId,
        CancellationToken cancellationToken)
    {
        foreach (var id in targets)
        {
            var target = await scope.Ports.Store.LoadAsync(id, WorkspaceContent.None, cancellationToken) ?? throw DomainException.NotFound();
            SnapshotRules.FollowReplacement(target, replacedId, replacementId);
            await scope.Ports.Store.SaveAsync(target.Commit(scope.Operation), cancellationToken);
        }
    }
}

/// <summary>What the four requests share: the replaced project, its code in the trash, the writing and the replies.</summary>
internal static class PackageImportReplies
{
    public static async Task<ProjectWorkspace> ReplacedAsync(UserScope scope, CancellationToken cancellationToken) =>
        await scope.Ports.Store.LoadAsync(scope.Project, WorkspaceContent.All, cancellationToken) ?? throw DomainException.NotFound();

    public static async Task<string> TrashCodeAsync(UserScope scope, string code, CancellationToken cancellationToken) =>
        ProjectCodeRules.TrashCode(code, await scope.Ports.Projects.CodesStartingWithAsync(ProjectCodeRules.TrashCodePrefix(code), cancellationToken));

    /// <summary>The preview counts what the import writes: the translations of the package less those it leaves out (AR-119).</summary>
    public static async Task<ImportPreview> PreviewAsync(UserScope scope, IReadPackage package, string target, CodeState state, PreparedImport prepared,
        string? trashCode, CancellationToken cancellationToken)
    {
        var counts = package.Counts with { Translations = package.Counts.Translations - prepared.IgnoredTranslations };
        return new(package.ProjectCode, target, state, package.WithPeople, counts, await ImportPreparation.UnknownLoginsAsync(scope, package, cancellationToken),
            prepared.Members, prepared.Settlement, trashCode);
    }

    /// <summary>
    /// The new users first (the records name them), then the project and its records, then its audit event, which says how
    /// many target states follow a replaced source when there are some (F-108); last the comments of presentation of its
    /// steps and of its flows (ADR 099), which name them, and the voices it keeps (ADR 103), which name it.
    /// </summary>
    public static async Task WriteAsync(UserScope scope, PreparedImport prepared, string mode, bool withMembers, int targetsRelinked,
        CancellationToken cancellationToken)
    {
        var details = targetsRelinked == 0
            ? JsonSerializer.Serialize(new { mode, with_members = withMembers })
            : JsonSerializer.Serialize(new { mode, with_members = withMembers, targets_relinked = targetsRelinked });
        var audit = AuditTrail.Event(scope.Operation, AuditKinds.ProjectImported, scope.User.Id, prepared.Project.Id, details);
        var changes = prepared.NewUsers.Select(user => new RecordChange(ChangeKind.Created, null, user))
            .Concat(prepared.Changes)
            .Append(new RecordChange(ChangeKind.Created, null, audit));
        await scope.Ports.Store.SaveAsync(new ChangeSet([.. changes]), cancellationToken);
        await scope.Ports.Comments.AddAsync(prepared.Comments, cancellationToken);
        await scope.Ports.FlowComments.AddAsync(prepared.FlowComments, cancellationToken);
        await scope.Ports.Voices.AddAsync(prepared.Voices, cancellationToken);
    }

    public static ImportedProject Imported(PreparedImport prepared, string? trashCode) =>
        new(prepared.Project.Id, prepared.Project.Code, prepared.RootMapId, trashCode, [.. prepared.NewUsers.Select(user => user.Id)]);
}
