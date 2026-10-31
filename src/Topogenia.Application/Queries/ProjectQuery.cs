// ProjectQuery.cs
// A read request on one project, run by the query template: what it names, needs and projects (spec 3.4).

using Topogenia.Application.Commands;
using Topogenia.Application.OperationLog;
using Topogenia.Application.Ports;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;

namespace Topogenia.Application.Queries;

/// <summary>
/// One read request on a project (a map, a sheet, the documents available...). It declares its right with one attribute
/// of <see cref="Authorization.AccessRequirementAttribute"/>, like a command, and projects the workspace into its reply;
/// <see cref="QueryRunner"/> applies the right and loads the workspace in a consistent read, without any lock or writing.
/// </summary>
/// <typeparam name="TResult">The reply, projected from the workspace.</typeparam>
public abstract class ProjectQuery<TResult>
{
    protected ProjectQuery(AccessTarget target)
    {
        Target = target;
    }

    public AccessTarget Target { get; }

    /// <summary>True for the reads of the trash, which reach a project in the trash.</summary>
    public virtual bool TargetsTrash => false;

    /// <summary>The groups of records the projection reads besides the structure.</summary>
    public virtual WorkspaceContent Content => WorkspaceContent.None;

    /// <summary>The step whose sheet the read concerns, for a read that requires the right to fill.</summary>
    public virtual Guid? SheetStepId => null;

    /// <summary>True when the projection builds a map view: the workspace then holds what the view reads.</summary>
    public virtual bool ReadsMapView => false;

    /// <summary>The map whose history of the reader the projection returns (annex B 2.6: it survives a reload), or null.</summary>
    public virtual Guid? HistoryMap => null;

    /// <summary>Projects the workspace into the reply.</summary>
    public abstract TResult Read(ProjectWorkspace workspace, QueryContext context);
}

/// <summary>
/// What a projection knows besides the workspace: the reader and the role he acts with, the builder of map views, and his
/// history on the map named by <see cref="ProjectQuery{TResult}.HistoryMap"/> (null otherwise).
/// </summary>
public sealed record QueryContext(Guid UserId, MemberRole Role, IMapViewBuilder Views, HistoryView? History)
{
    public MapViewer Viewer => new(UserId, Role);
}
