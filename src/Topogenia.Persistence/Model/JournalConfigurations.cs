// JournalConfigurations.cs
// Tables of the translations, the operation log, the audit journal and the history of the schema (spec 4.1, 4.4, 4.6, 5.5).

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Topogenia.Domain.Model;
using Topogenia.Persistence.Schema;

namespace Topogenia.Persistence.Model;

internal sealed class TranslationConfiguration : IEntityTypeConfiguration<Translation>
{
    public void Configure(EntityTypeBuilder<Translation> builder)
    {
        builder.ToTable(TableNames.Translation);
        // Polymorphic reference without foreign key (1.x): the translations of an object deleted logically stay for its restoration.
        builder.KeyGivenByTheApplication(translation => new { translation.ObjectType, translation.ObjectId, translation.Field, translation.Language });
        builder.Code(translation => translation.ObjectType, 10);
        builder.Code(translation => translation.Field, 10);
        builder.Language(translation => translation.Language);
        builder.Text(translation => translation.Text, 4000);
        builder.Code(translation => translation.Source, 6);
        builder.References<Translation, AppUser>(translation => translation.UpdatedBy);
        builder.Versioned();
    }
}

internal sealed class OperationLogEntryConfiguration : IEntityTypeConfiguration<OperationLogEntry>
{
    public void Configure(EntityTypeBuilder<OperationLogEntry> builder)
    {
        builder.ToTable(TableNames.OperationLog);
        builder.KeyGivenByTheApplication(entry => entry.Id);
        builder.References<OperationLogEntry, Project>(entry => entry.ProjectId);
        builder.References<OperationLogEntry, ProcessMap>(entry => entry.MapId);
        builder.References<OperationLogEntry, AppUser>(entry => entry.UserId);
        builder.Ascii(entry => entry.Kind, 60);
        builder.Text(entry => entry.ForwardJson, null);
        builder.Text(entry => entry.InverseJson, null);
        builder.Text(entry => entry.TouchedJson, null).HasColumnName("touched");
        // The entry redone from is an identifier only: the oldest entries beyond the depth are trimmed (spec 4.6).
        builder.HasIndex(entry => new { entry.MapId, entry.Sequence }).IsUnique().HasDatabaseName("UQ_operation_log_map_id_sequence");
        builder.HasIndex(entry => new { entry.MapId, entry.UserId });
        builder.HasIndex(entry => entry.ProjectId);
    }
}

internal sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> builder)
    {
        builder.ToTable(TableNames.AuditEvent);
        builder.KeyGivenByTheApplication(audit => audit.Id);
        builder.Property(audit => audit.At).HasColumnName("occurred_at");
        builder.References<AuditEvent, AppUser>(audit => audit.UserId);
        builder.Ascii(audit => audit.Kind, 60);
        // The project is an identifier only: the audit of a purge outlives the purged project (spec 5.5).
        builder.Text(audit => audit.Details, 4000);
        builder.HasIndex(audit => audit.At);
        builder.HasIndex(audit => audit.ProjectId);
    }
}

/// <summary>
/// `assistant_settings` (3.0): one row, its key given by the application; the codes and technical texts in ASCII, the
/// secret as the host protected it, the version as the concurrency token, the author a user.
/// </summary>
internal sealed class AssistantSettingsConfiguration : IEntityTypeConfiguration<Assistant.AssistantSettingsRow>
{
    public void Configure(EntityTypeBuilder<Assistant.AssistantSettingsRow> builder)
    {
        builder.ToTable(TableNames.AssistantSettings);
        builder.KeyGivenByTheApplication(row => row.Id);
        builder.Code(row => row.Connector, 20);
        builder.Ascii(row => row.Model, Application.Assistant.AssistantRules.ModelMax);
        builder.Ascii(row => row.Region, 40);
        builder.Ascii(row => row.AwsProfile, Application.Assistant.AssistantRules.ProfileMax);
        builder.Ascii(row => row.AllowedClassifications, Assistant.AssistantSettingsRow.ClassificationsLength);
        builder.Ascii(row => row.ProtectedSecret, Assistant.AssistantSettingsRow.ProtectedSecretLength);
        builder.Ascii(row => row.SecretHint, Application.Assistant.AssistantRules.HintLength);
        builder.Property(row => row.Version).IsConcurrencyToken();
        builder.References<Assistant.AssistantSettingsRow, AppUser>(row => row.UpdatedBy);
    }
}

internal sealed class SchemaHistoryEntryConfiguration : IEntityTypeConfiguration<SchemaHistoryEntry>
{
    public void Configure(EntityTypeBuilder<SchemaHistoryEntry> builder)
    {
        builder.ToTable(TableNames.SchemaHistory);
        builder.KeyGivenByTheApplication(entry => entry.MigrationId);
        builder.Ascii(entry => entry.MigrationId, SchemaHistoryEntry.MigrationIdLength);
        builder.Ascii(entry => entry.MigrationFingerprint, Fingerprints.Length).IsFixedLength();
        builder.Property(entry => entry.SchemaFingerprint).HasMaxLength(Fingerprints.Length).IsUnicode(false).IsFixedLength();
        builder.Ascii(entry => entry.ApplicationVersion, 32);
    }
}
