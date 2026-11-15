// ModelConfigurations.cs
// The list of the configurations of the model, one per record of the domain plus the history of the schema, the settings of the assistant, the comments of presentation of the steps and of the flows and the voices of the projects.

using Microsoft.EntityFrameworkCore;

namespace Topogenia.Persistence.Model;

/// <summary>
/// Applies the configuration of every table (`docs/modele-donnees.md`), in the order of the 1.x schema, then the converter
/// of the texts (ADR 038).
/// </summary>
public static class ModelConfigurations
{
    public static void Apply(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfiguration(new AppUserConfiguration());
        modelBuilder.ApplyConfiguration(new ProjectConfiguration());
        modelBuilder.ApplyConfiguration(new ProjectCounterConfiguration());
        modelBuilder.ApplyConfiguration(new ProjectMemberConfiguration());
        modelBuilder.ApplyConfiguration(new DeletionBatchConfiguration());
        modelBuilder.ApplyConfiguration(new ProcessMapConfiguration());
        modelBuilder.ApplyConfiguration(new LaneConfiguration());
        modelBuilder.ApplyConfiguration(new StepConfiguration());
        modelBuilder.ApplyConfiguration(new FlowConfiguration());
        modelBuilder.ApplyConfiguration(new StepRaciConfiguration());
        modelBuilder.ApplyConfiguration(new StepSheetConfiguration());
        modelBuilder.ApplyConfiguration(new SheetOperationConfiguration());
        modelBuilder.ApplyConfiguration(new StepSheetVersionConfiguration());
        modelBuilder.ApplyConfiguration(new DocumentConfiguration());
        modelBuilder.ApplyConfiguration(new PainPointConfiguration());
        modelBuilder.ApplyConfiguration(new PainPointStatusChangeConfiguration());
        modelBuilder.ApplyConfiguration(new DocumentUseConfiguration());
        modelBuilder.ApplyConfiguration(new TranslationConfiguration());
        modelBuilder.ApplyConfiguration(new MapSummaryConfiguration());
        modelBuilder.ApplyConfiguration(new StreamSnapshotConfiguration());
        modelBuilder.ApplyConfiguration(new DissolutionConfiguration());
        modelBuilder.ApplyConfiguration(new DissolutionStepConfiguration());
        modelBuilder.ApplyConfiguration(new DissolutionObjectConfiguration());
        modelBuilder.ApplyConfiguration(new OperationLogEntryConfiguration());
        modelBuilder.ApplyConfiguration(new AuditEventConfiguration());
        modelBuilder.ApplyConfiguration(new SchemaHistoryEntryConfiguration());
        modelBuilder.ApplyConfiguration(new AssistantSettingsConfiguration());
        modelBuilder.ApplyConfiguration(new PresentationCommentConfiguration());
        modelBuilder.ApplyConfiguration(new FlowPresentationCommentConfiguration());
        modelBuilder.ApplyConfiguration(new PresentationVoiceConfiguration());
        StoredTexts.Apply(modelBuilder);
    }
}
