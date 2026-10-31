// StructureConfigurations.cs
// Tables of the map structure: maps, lanes, steps, flows, RACI roles and dissolutions (spec 4.1, 4.2, 1.x `db/001`, `db/007`, `db/008`).

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Topogenia.Domain.Model;
using Topogenia.Domain.Structure;

namespace Topogenia.Persistence.Model;

internal sealed class ProcessMapConfiguration : IEntityTypeConfiguration<ProcessMap>
{
    public void Configure(EntityTypeBuilder<ProcessMap> builder)
    {
        builder.ToTable(TableNames.ProcessMap);
        builder.KeyGivenByTheApplication(map => map.Id);
        builder.References<ProcessMap, Project>(map => map.ProjectId);
        // The map of a step is stored here only (circular with step.map_id, spec 4.2): the parent step is a foreign key.
        builder.References<ProcessMap, Step>(map => map.ParentStepId);
        builder.Text(map => map.Name, StructureEdits.MapNameMax);
        builder.Language(map => map.SourceLanguage);
        builder.AuditedBy(map => map.CreatedBy, map => map.UpdatedBy);
        builder.References<ProcessMap, DeletionBatch>(map => map.DeletionBatchId);
        builder.HasIndex(map => map.ParentStepId, IndexNames.LiveSubMapOfStep).IsUnique();
        builder.HasIndex(map => map.ProjectId, IndexNames.RootMapOfProject).IsUnique();
        builder.HasIndex(map => map.ProjectId, "IX_process_map_project_id");
        builder.HasIndex(map => map.DeletionBatchId);
        builder.Versioned();
    }
}

internal sealed class LaneConfiguration : IEntityTypeConfiguration<Lane>
{
    public void Configure(EntityTypeBuilder<Lane> builder)
    {
        builder.ToTable(TableNames.Lane);
        builder.KeyGivenByTheApplication(lane => lane.Id);
        // Target of the composite key of step (lane_id, map_id): a step and its lane are on the same map (RG-C02).
        builder.HasAlternateKey(lane => new { lane.Id, lane.MapId }).HasName("UQ_lane_id_map_id");
        builder.References<Lane, ProcessMap>(lane => lane.MapId);
        builder.Ascii(lane => lane.Code, ConfigurationExtensions.ReadableCodeLength);
        builder.HasIndex(lane => lane.Code).IsUnique().HasDatabaseName("UQ_lane_code");
        builder.Text(lane => lane.Name, StructureEdits.LaneNameMax);
        builder.Language(lane => lane.SourceLanguage);
        builder.Code(lane => lane.LaneType, 10);
        builder.References<Lane, AppUser>(lane => lane.ActorUserId);
        builder.Ascii(lane => lane.Color, 7).IsFixedLength();
        // The persons who hold the role of a lane of people (spec 14.2, S4).
        builder.Property(lane => lane.Headcount);
        // The lane copied (1.x `db/008`) is an identifier only: the original may be deleted or purged.
        builder.AuditedBy(lane => lane.CreatedBy, lane => lane.UpdatedBy);
        builder.References<Lane, DeletionBatch>(lane => lane.DeletionBatchId);
        builder.HasIndex(lane => lane.MapId);
        builder.HasIndex(lane => lane.DeletionBatchId);
        builder.Versioned();
    }
}

internal sealed class StepConfiguration : IEntityTypeConfiguration<Step>
{
    public void Configure(EntityTypeBuilder<Step> builder)
    {
        builder.ToTable(TableNames.Step);
        builder.KeyGivenByTheApplication(step => step.Id);
        builder.References<Step, ProcessMap>(step => step.MapId);
        builder.HasOne<Lane>().WithMany()
            .HasForeignKey(step => new { step.LaneId, step.MapId })
            .HasPrincipalKey(lane => new { lane.Id, lane.MapId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_step_lane");
        builder.Ascii(step => step.Code, ConfigurationExtensions.ReadableCodeLength);
        builder.HasIndex(step => step.Code).IsUnique().HasDatabaseName("UQ_step_code");
        builder.Text(step => step.Label, StructureEdits.StepLabelMax);
        builder.Language(step => step.SourceLanguage);
        builder.Code(step => step.StepType, 10);
        builder.Property(step => step.Column).HasColumnName("column_index");
        builder.Property(step => step.Row).HasColumnName("row_index");
        builder.References<Step, AppUser>(step => step.AssigneeUserId);
        builder.Code(step => step.AutomationStatus, 12);
        // The incoming volume of a start step (spec 14.2, S1).
        builder.Decimal(step => step.VolumeQuantity, 9, 2);
        builder.Code(step => step.VolumePeriod, 8);
        // The operation that created the step (spec 4.6) is an identifier only: the operation may be trimmed or purged.
        builder.AuditedBy(step => step.CreatedBy, step => step.UpdatedBy);
        builder.References<Step, DeletionBatch>(step => step.DeletionBatchId);
        builder.HasIndex(step => new { step.LaneId, step.Column, step.Row }, IndexNames.LiveStepOfCell).IsUnique();
        builder.HasIndex(step => step.MapId);
        builder.HasIndex(step => step.AssigneeUserId);
        builder.HasIndex(step => step.DeletionBatchId);
        builder.Versioned();
    }
}

internal sealed class FlowConfiguration : IEntityTypeConfiguration<Flow>
{
    public void Configure(EntityTypeBuilder<Flow> builder)
    {
        builder.ToTable(TableNames.Flow);
        builder.KeyGivenByTheApplication(flow => flow.Id);
        builder.References<Flow, ProcessMap>(flow => flow.MapId);
        builder.References<Flow, Step>(flow => flow.SourceStepId);
        builder.References<Flow, Step>(flow => flow.TargetStepId);
        builder.Text(flow => flow.Label, StructureEdits.FlowLabelMax);
        // The share of the cases of an exit of a decision or an inclusive gateway (spec 14.2, S2).
        builder.Decimal(flow => flow.SharePercent, 5, 2);
        builder.AuditedBy(flow => flow.CreatedBy, flow => flow.UpdatedBy);
        builder.References<Flow, DeletionBatch>(flow => flow.DeletionBatchId);
        builder.HasIndex(flow => new { flow.SourceStepId, flow.TargetStepId }, IndexNames.LiveFlowOfPair).IsUnique();
        builder.HasIndex(flow => flow.MapId);
        builder.HasIndex(flow => flow.TargetStepId);
        builder.HasIndex(flow => flow.DeletionBatchId);
        builder.Versioned();
    }
}

internal sealed class StepRaciConfiguration : IEntityTypeConfiguration<StepRaci>
{
    public void Configure(EntityTypeBuilder<StepRaci> builder)
    {
        builder.ToTable(TableNames.StepRaci);
        builder.KeyGivenByTheApplication(raci => new { raci.StepId, raci.LaneId });
        builder.References<StepRaci, Step>(raci => raci.StepId);
        builder.References<StepRaci, Lane>(raci => raci.LaneId);
        builder.Code(raci => raci.Role, 1).IsFixedLength();
        builder.AuditedBy(raci => raci.CreatedBy, raci => raci.UpdatedBy);
        builder.HasIndex(raci => raci.StepId, IndexNames.SingleApproverOfStep).IsUnique();
        builder.HasIndex(raci => raci.LaneId);
        builder.Versioned();
    }
}

internal sealed class DissolutionConfiguration : IEntityTypeConfiguration<Dissolution>
{
    public void Configure(EntityTypeBuilder<Dissolution> builder)
    {
        builder.ToTable(TableNames.Dissolution);
        builder.KeyGivenByTheApplication(dissolution => dissolution.Id);
        builder.References<Dissolution, ProcessMap>(dissolution => dissolution.MapId);
        builder.References<Dissolution, Step>(dissolution => dissolution.StepId);
        builder.References<Dissolution, DeletionBatch>(dissolution => dissolution.DeletionBatchId);
        builder.AuditedBy(dissolution => dissolution.CreatedBy, dissolution => dissolution.UpdatedBy);
        builder.References<Dissolution, AppUser>(dissolution => dissolution.ClosedBy);
        builder.HasIndex(dissolution => dissolution.MapId, IndexNames.OpenDissolutionOfMap).IsUnique();
        builder.HasIndex(dissolution => dissolution.StepId);
        builder.HasIndex(dissolution => dissolution.DeletionBatchId);
        builder.Versioned();
    }
}

internal sealed class DissolutionStepConfiguration : IEntityTypeConfiguration<DissolutionStep>
{
    public void Configure(EntityTypeBuilder<DissolutionStep> builder)
    {
        builder.ToTable(TableNames.DissolutionStep);
        builder.KeyGivenByTheApplication(item => new { item.DissolutionId, item.StepId });
        builder.References<DissolutionStep, Dissolution>(item => item.DissolutionId);
        builder.References<DissolutionStep, Step>(item => item.StepId);
        // The original lane has no foreign key (1.x `db/007`): it is in the trash with the detailed map and may be purged first.
        builder.HasIndex(item => item.StepId);
    }
}

internal sealed class DissolutionObjectConfiguration : IEntityTypeConfiguration<DissolutionObject>
{
    public void Configure(EntityTypeBuilder<DissolutionObject> builder)
    {
        builder.ToTable(TableNames.DissolutionObject);
        builder.KeyGivenByTheApplication(item => new { item.DissolutionId, item.ObjectType, item.ObjectId });
        builder.References<DissolutionObject, Dissolution>(item => item.DissolutionId);
        builder.Ascii(item => item.ObjectType, 10);
    }
}
