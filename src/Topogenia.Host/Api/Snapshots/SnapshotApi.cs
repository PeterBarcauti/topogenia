// SnapshotApi.cs
// The routes of the photos of the indicators of a project and of its target state turned into their requests (Makigami M6, M8, ADR 095).

using Topogenia.Application.Commands;
using Topogenia.Application.Exchange;
using Topogenia.Application.Ports;
using Topogenia.Application.Queries;
using Topogenia.Application.Snapshots;

namespace Topogenia.Host.Api.Snapshots;

/// <summary>`POST /api/projects/{id}/snapshots` and `POST /api/projects/{id}/baseline`: the label of the photo.</summary>
public sealed record SnapshotBody(string? Label);

/// <summary>`POST /api/projects/{id}/target`: the code and the name of the target state, the label of its reference photo.</summary>
public sealed record TargetStateBody(string? Code, string? Name, string? Label);

/// <summary>Reads the bodies of the routes of the photos and runs their requests; a write answers the photos read again.</summary>
public sealed class SnapshotApi(QueryRunner queries, CommandRunner commands, UserRunner users, IPackageExporter exporter, IPackageImporter importer)
{
    /// <summary>The photos, the comparison, and for a target state where its source stands for the reader.</summary>
    public async Task<SnapshotsView> ReadAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var view = await queries.RunAsync(new SnapshotsQuery(projectId), cancellationToken);
        return view.BaselineProjectId is null ? view : view with { Source = await users.RunAsync(new BaselineSourceQuery(projectId), cancellationToken) };
    }

    public async Task<SnapshotsView> TakeAsync(Guid projectId, SnapshotBody body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        await commands.RunAsync(new TakeSnapshotCommand(projectId, body.Label), cancellationToken);
        return await ReadAsync(projectId, cancellationToken);
    }

    public async Task<SnapshotsView> DeleteAsync(Guid projectId, Guid snapshotId, CancellationToken cancellationToken)
    {
        await commands.RunAsync(new DeleteSnapshotCommand(projectId, snapshotId), cancellationToken);
        return await ReadAsync(projectId, cancellationToken);
    }

    public Task<ImportedProject> CreateTargetAsync(Guid projectId, TargetStateBody body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        return users.RunAsync(new CreateTargetStateCommand(new TargetStatePorts(exporter, importer), projectId, body.Code, body.Name, body.Label), cancellationToken);
    }

    public async Task<SnapshotsView> RefreshAsync(Guid projectId, SnapshotBody body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        await users.RunAsync(new RefreshBaselineCommand(projectId, body.Label), cancellationToken);
        return await ReadAsync(projectId, cancellationToken);
    }
}
