// SheetConfigurations.cs
// Tables of the sheets, their procedure and versions, documents, uses and pain points (spec 4.1, 1.x `db/001`).

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Topogenia.Domain.Documents;
using Topogenia.Domain.Model;
using Topogenia.Domain.Sheets;
using Topogenia.Domain.Values;

namespace Topogenia.Persistence.Model;

internal sealed class StepSheetConfiguration : IEntityTypeConfiguration<StepSheet>
{
    public void Configure(EntityTypeBuilder<StepSheet> builder)
    {
        builder.ToTable(TableNames.StepSheet);
        builder.KeyGivenByTheApplication(sheet => sheet.Id);
        builder.References<StepSheet, Step>(sheet => sheet.StepId);
        builder.HasIndex(sheet => sheet.StepId).IsUnique().HasDatabaseName("UQ_step_sheet_step_id");
        builder.Code(sheet => sheet.Status, 12);
        builder.Text(sheet => sheet.Actor, SheetContentReader.TextMax);
        builder.Text(sheet => sheet.Purpose, SheetContentReader.TextMax);
        builder.Text(sheet => sheet.Tools, SheetContentReader.TextMax);
        builder.Code(sheet => sheet.Frequency, 16);
        builder.Decimal(sheet => sheet.DurationMinutes, 9, 2);
        builder.Decimal(sheet => sheet.LeadTime, 9, 2);
        builder.Code(sheet => sheet.LeadTimeUnit, 8);
        builder.Text(sheet => sheet.FreeText, null);
        builder.Language(sheet => sheet.SourceLanguage);
        builder.Text(sheet => sheet.ClarificationRequest, SheetWorkflow.ClarificationMax);
        builder.AuditedBy(sheet => sheet.CreatedBy, sheet => sheet.UpdatedBy);
        builder.Versioned();
    }
}

internal sealed class SheetOperationConfiguration : IEntityTypeConfiguration<SheetOperation>
{
    public void Configure(EntityTypeBuilder<SheetOperation> builder)
    {
        builder.ToTable(TableNames.StepOperation);
        builder.KeyGivenByTheApplication(operation => new { operation.SheetId, operation.Position });
        builder.References<SheetOperation, StepSheet>(operation => operation.SheetId);
        builder.Text(operation => operation.Text, SheetContentReader.TaskTextMax);
        builder.Text(operation => operation.Tool, SheetContentReader.TaskToolMax);
        builder.Code(operation => operation.Nature, 12);
    }
}

internal sealed class StepSheetVersionConfiguration : IEntityTypeConfiguration<StepSheetVersion>
{
    public void Configure(EntityTypeBuilder<StepSheetVersion> builder)
    {
        builder.ToTable(TableNames.StepSheetVersion);
        builder.KeyGivenByTheApplication(version => version.Id);
        builder.References<StepSheetVersion, StepSheet>(version => version.SheetId);
        builder.HasIndex(version => new { version.SheetId, version.VersionNumber }).IsUnique()
            .HasDatabaseName("UQ_step_sheet_version_sheet_id_version_number");
        builder.Text(version => version.ContentJson, null);
        // Denormalised from the frozen content (spec 4.1): read without any JSON function of the database.
        builder.Code(version => version.Frequency, 16);
        builder.Decimal(version => version.DurationMinutes, 9, 2);
        builder.Decimal(version => version.LeadTime, 9, 2);
        builder.Code(version => version.LeadTimeUnit, 8);
        builder.References<StepSheetVersion, AppUser>(version => version.ValidatedBy);
    }
}

internal sealed class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.ToTable(TableNames.Document);
        builder.KeyGivenByTheApplication(document => document.Id);
        builder.References<Document, Step>(document => document.StepId);
        builder.Code(document => document.Direction, 6);
        builder.Text(document => document.Name, DocumentRules.NameMax);
        builder.Text(document => document.Location, DocumentLocation.MaxLength);
        builder.Code(document => document.LocationType, 4);
        builder.Decimal(document => document.WaitHours, 9, 2);
        builder.Language(document => document.SourceLanguage);
        builder.AuditedBy(document => document.CreatedBy, document => document.UpdatedBy);
        builder.HasIndex(document => new { document.StepId, document.Direction });
        builder.Versioned();
    }
}

internal sealed class DocumentUseConfiguration : IEntityTypeConfiguration<DocumentUse>
{
    public void Configure(EntityTypeBuilder<DocumentUse> builder)
    {
        builder.ToTable(TableNames.DocumentUse);
        builder.KeyGivenByTheApplication(use => new { use.DocumentId, use.StepId });
        builder.References<DocumentUse, Document>(use => use.DocumentId);
        builder.References<DocumentUse, Step>(use => use.StepId);
        builder.Decimal(use => use.WaitHours, 9, 2);
        builder.References<DocumentUse, PainPoint>(use => use.PainPointId);
        builder.AuditedBy(use => use.CreatedBy, use => use.UpdatedBy);
        builder.HasIndex(use => use.StepId);
        builder.HasIndex(use => use.PainPointId, IndexNames.PainPointOfUse).IsUnique();
        builder.Versioned();
    }
}

internal sealed class PainPointConfiguration : IEntityTypeConfiguration<PainPoint>
{
    public void Configure(EntityTypeBuilder<PainPoint> builder)
    {
        builder.ToTable(TableNames.PainPoint);
        builder.KeyGivenByTheApplication(painPoint => painPoint.Id);
        builder.References<PainPoint, Project>(painPoint => painPoint.ProjectId);
        builder.References<PainPoint, Step>(painPoint => painPoint.StepId);
        builder.Ascii(painPoint => painPoint.Code, ConfigurationExtensions.ReadableCodeLength);
        builder.HasIndex(painPoint => painPoint.Code).IsUnique().HasDatabaseName("UQ_pain_point_code");
        builder.Text(painPoint => painPoint.Description, DocumentRules.PainPointDescriptionMax);
        builder.Language(painPoint => painPoint.SourceLanguage);
        builder.Decimal(painPoint => painPoint.LostHoursPerMonth, 6, 1);
        builder.Code(painPoint => painPoint.Status, 12);
        builder.AuditedBy(painPoint => painPoint.CreatedBy, painPoint => painPoint.UpdatedBy);
        builder.HasIndex(painPoint => painPoint.ProjectId);
        builder.HasIndex(painPoint => painPoint.StepId);
        builder.Versioned();
    }
}

internal sealed class PainPointStatusChangeConfiguration : IEntityTypeConfiguration<PainPointStatusChange>
{
    public void Configure(EntityTypeBuilder<PainPointStatusChange> builder)
    {
        builder.ToTable(TableNames.PainPointStatusHistory);
        builder.KeyGivenByTheApplication(change => change.Id);
        builder.References<PainPointStatusChange, PainPoint>(change => change.PainPointId);
        builder.Code(change => change.StatusBefore, 12);
        builder.Code(change => change.StatusAfter, 12);
        builder.Text(change => change.Comment, DocumentRules.StatusCommentMax);
        builder.References<PainPointStatusChange, AppUser>(change => change.ChangedBy);
        builder.HasIndex(change => new { change.PainPointId, change.ChangedAt });
    }
}
