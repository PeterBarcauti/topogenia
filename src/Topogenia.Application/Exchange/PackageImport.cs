// PackageImport.cs
// The import of an exchange package: preview and import as a new project, preview and replacement of a project (spec 7.3, ADR 052).

using System.Text.Json;
using Topogenia.Application.Audit;
using Topogenia.Application.Authorization;
using Topogenia.Application.Commands;
using Topogenia.Application.Ports;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Projects;
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

/// <summary>Runs the four requests of the import, each one reading the package itself, once its right is granted.</summary>
public sealed class PackageImport(UserRunner runner, IPackageImporter importer)
{
    public Task<ImportPreview> PreviewAsync(byte[] content, string? code, bool withMembers, CancellationToken cancellationToken) =>
        runner.RunAsync(new PreviewImportQuery(new PackageSource(importer, content), code, withMembers), cancellationToken);

    public Task<ImportedProject> ImportAsync(byte[] content, string? code, bool withMembers, CancellationToken cancellationToken) =>
        runner.RunAsync(new ImportProjectCommand(new PackageSource(importer, content), code, withMembers), cancellationToken);

    public Task<ImportPreview> PreviewReplacementAsync(Guid projectId, byte[] content, bool withMembers, CancellationToken cancellationToken) =>
        runner.RunAsync(new PreviewReplacementQuery(projectId, new PackageSource(importer, content), withMembers), cancellationToken);

    public Task<ImportedProject> ReplaceAsync(Guid projectId, byte[] content, bool withMembers, int? version, CancellationToken cancellationToken) =>
        runner.RunAsync(new ReplaceProjectCommand(projectId, new PackageSource(importer, content), withMembers, version), cancellationToken);
}

/// <summary>
/// The bytes of a received package and their reading, done once, after the right of the request is granted (a refused
/// user never makes the server open an archive), and kept across the attempts of a unit of work.
/// </summary>
public sealed class PackageSource(IPackageImporter importer, byte[] content)
{
    private IReadPackage? read;

    public IPackageImporter Importer { get; } = importer;

    public IReadPackage Read() => read ??= Importer.Read(content);
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
        await PackageImportReplies.WriteAsync(scope, prepared, "new", withMembers, cancellationToken);
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
/// are written in one unit of work, the replaced project first so that its codes are free, with their audit events. The
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
        ProjectReplacement.Retire(replaced, scope.Operation, trashCode);
        await scope.Ports.Store.SaveAsync(replaced.Commit(scope.Operation), cancellationToken);
        await scope.Ports.Store.SaveAsync(new ChangeSet([new RecordChange(ChangeKind.Created, null,
            AuditTrail.Event(scope.Operation, AuditKinds.ProjectTrashed, scope.User.Id, replaced.ProjectId))]), cancellationToken);
        await PackageImportReplies.WriteAsync(scope, prepared, "replace", withMembers, cancellationToken);
        return PackageImportReplies.Imported(prepared, trashCode);
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

    /// <summary>The new users first (the records name them), then the project and its records, then its audit event.</summary>
    public static Task WriteAsync(UserScope scope, PreparedImport prepared, string mode, bool withMembers, CancellationToken cancellationToken)
    {
        var details = JsonSerializer.Serialize(new { mode, with_members = withMembers });
        var audit = AuditTrail.Event(scope.Operation, AuditKinds.ProjectImported, scope.User.Id, prepared.Project.Id, details);
        var changes = prepared.NewUsers.Select(user => new RecordChange(ChangeKind.Created, null, user))
            .Concat(prepared.Changes)
            .Append(new RecordChange(ChangeKind.Created, null, audit));
        return scope.Ports.Store.SaveAsync(new ChangeSet([.. changes]), cancellationToken);
    }

    public static ImportedProject Imported(PreparedImport prepared, string? trashCode) =>
        new(prepared.Project.Id, prepared.Project.Code, prepared.RootMapId, trashCode, [.. prepared.NewUsers.Select(user => user.Id)]);
}
