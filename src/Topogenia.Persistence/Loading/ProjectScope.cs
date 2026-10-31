// ProjectScope.cs
// The queries of the records of one project, composed by joins on the project and never by a list of identifiers (spec 4.3-2).

using Microsoft.EntityFrameworkCore;
using Topogenia.Domain.Model;
using Topogenia.Domain.Values;

namespace Topogenia.Persistence.Loading;

/// <summary>
/// Every table of a project reached from the project itself: the maps by their project, the lanes, steps, flows and
/// frames by their map, the roles, sheets and documents by their step, and so on. Each query is a join (an `EXISTS` on
/// the parent query), so no list of identifiers is ever sent and no limit of parameters applies, whatever the size of
/// the project (spec 4.3-2). Nothing is tracked: the records go into a workspace.
/// </summary>
internal sealed class ProjectScope
{
    private readonly AppDbContext context;
    private readonly Guid projectId;

    public ProjectScope(AppDbContext context, Guid projectId)
    {
        this.context = context;
        this.projectId = projectId;
    }

    public IQueryable<Project> Project => Of<Project>().Where(project => project.Id == projectId);

    public IQueryable<ProjectCounter> Counters => Of<ProjectCounter>().Where(counter => counter.ProjectId == projectId);

    public IQueryable<DeletionBatch> Batches => Of<DeletionBatch>().Where(batch => batch.ProjectId == projectId);

    public IQueryable<ProcessMap> Maps => Of<ProcessMap>().Where(map => map.ProjectId == projectId);

    public IQueryable<Lane> Lanes => Of<Lane>().Where(lane => Maps.Any(map => map.Id == lane.MapId));

    public IQueryable<Step> Steps => Of<Step>().Where(step => Maps.Any(map => map.Id == step.MapId));

    public IQueryable<Flow> Flows => Of<Flow>().Where(flow => Maps.Any(map => map.Id == flow.MapId));

    public IQueryable<StepRaci> Roles => Of<StepRaci>().Where(role => Steps.Any(step => step.Id == role.StepId));

    public IQueryable<Dissolution> Dissolutions => Of<Dissolution>().Where(dissolution => Maps.Any(map => map.Id == dissolution.MapId));

    public IQueryable<DissolutionStep> DissolutionSteps =>
        Of<DissolutionStep>().Where(step => Dissolutions.Any(dissolution => dissolution.Id == step.DissolutionId));

    public IQueryable<DissolutionObject> DissolutionObjects =>
        Of<DissolutionObject>().Where(item => Dissolutions.Any(dissolution => dissolution.Id == item.DissolutionId));

    public IQueryable<StepSheet> Sheets => Of<StepSheet>().Where(sheet => Steps.Any(step => step.Id == sheet.StepId));

    public IQueryable<SheetOperation> SheetOperations => Of<SheetOperation>().Where(operation => Sheets.Any(sheet => sheet.Id == operation.SheetId));

    public IQueryable<StepSheetVersion> SheetVersions => Of<StepSheetVersion>().Where(version => Sheets.Any(sheet => sheet.Id == version.SheetId));

    public IQueryable<Document> Documents => Of<Document>().Where(document => Steps.Any(step => step.Id == document.StepId));

    public IQueryable<DocumentUse> Uses => Of<DocumentUse>().Where(use => Documents.Any(document => document.Id == use.DocumentId));

    public IQueryable<PainPoint> PainPoints => Of<PainPoint>().Where(painPoint => painPoint.ProjectId == projectId);

    public IQueryable<PainPointStatusChange> PainPointHistory =>
        Of<PainPointStatusChange>().Where(change => PainPoints.Any(painPoint => painPoint.Id == change.PainPointId));

    /// <summary>The translations of the project, of its maps, lanes, steps and flows (the objects of 1.x `translation`).</summary>
    public IQueryable<Translation> Translations => Of<Translation>().Where(translation =>
        (translation.ObjectType == TranslationObject.Project && translation.ObjectId == projectId)
        || (translation.ObjectType == TranslationObject.Map && Maps.Any(map => map.Id == translation.ObjectId))
        || (translation.ObjectType == TranslationObject.Lane && Lanes.Any(lane => lane.Id == translation.ObjectId))
        || (translation.ObjectType == TranslationObject.Step && Steps.Any(step => step.Id == translation.ObjectId))
        || (translation.ObjectType == TranslationObject.Flow && Flows.Any(flow => flow.Id == translation.ObjectId)));

    public IQueryable<ProjectMember> Members => Of<ProjectMember>().Where(member => member.ProjectId == projectId);

    /// <summary>The users that the members of the project name.</summary>
    public IQueryable<AppUser> MemberUsers => Of<AppUser>().Where(user => Members.Any(member => member.UserId == user.Id));

    /// <summary>The entries of the operation log of the project.</summary>
    public IQueryable<OperationLogEntry> OperationLog => Of<OperationLogEntry>().Where(entry => entry.ProjectId == projectId);

    /// <summary>The audit events about the project.</summary>
    public IQueryable<AuditEvent> Audit => Of<AuditEvent>().Where(item => item.ProjectId == projectId);

    /// <summary>
    /// The records without version of type <typeparamref name="T"/> that belong to the project, or null for a type that
    /// has none: what a unit may write once it holds the lock of the project (<see cref="Writing.UnversionedCheck"/>, ADR 030).
    /// </summary>
    public IQueryable<T>? Unversioned<T>()
        where T : class, IRecord =>
        new IQueryable[] { Counters, Batches, DissolutionSteps, DissolutionObjects, SheetOperations, SheetVersions, PainPointHistory, OperationLog, Audit }
            .OfType<IQueryable<T>>().FirstOrDefault();

    private IQueryable<T> Of<T>()
        where T : class => context.Set<T>().AsNoTracking();
}
