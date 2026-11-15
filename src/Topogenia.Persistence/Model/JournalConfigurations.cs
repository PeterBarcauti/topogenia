// JournalConfigurations.cs
// Tables of the translations, the operation log, the audit journal and the history of the schema (spec 4.1, 4.4, 4.6, 5.5), and of what is kept beside the closed core: the settings of the assistant, the comments of presentation, the voices of the projects.

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
        builder.Ascii(row => row.Resource, Assistant.AssistantSettingsRow.ResourceLength);
        builder.Property(row => row.Version).IsConcurrencyToken();
        builder.References<Assistant.AssistantSettingsRow, AppUser>(row => row.UpdatedBy);
    }
}

/// <summary>
/// `presentation_comment` (3.2, ADR 099 revised): one row per step and language, its key (a comment follows its step
/// from map to map, and goes before it at its purge; the row in the language of origin of the step is the original, the
/// others its translations); the text entered, its source of a closed list, the version as the concurrency token, the
/// author a user.
/// </summary>
internal sealed class PresentationCommentConfiguration : IEntityTypeConfiguration<Application.Presentation.PresentationComment>
{
    public void Configure(EntityTypeBuilder<Application.Presentation.PresentationComment> builder)
    {
        builder.ToTable(TableNames.PresentationComment);
        builder.KeyGivenByTheApplication(comment => new { comment.StepId, comment.Language });
        builder.References<Application.Presentation.PresentationComment, Step>(comment => comment.StepId);
        builder.Text(comment => comment.Text, Application.Presentation.PresentationCommentRules.TextMax);
        builder.Language(comment => comment.Language);
        builder.Code(comment => comment.Source, 10);
        builder.Property(comment => comment.Version).IsConcurrencyToken();
        builder.References<Application.Presentation.PresentationComment, AppUser>(comment => comment.UpdatedBy);
    }
}

/// <summary>
/// `flow_presentation_comment` (3.2, ADR 099, lot J10): one row per flow and language, its key (a comment follows its
/// flow to the trash and back, and goes before it at its purge; a flow has no language of its own: the row in the
/// language of origin of its source step is the original, the others its translations); the same columns and guards as
/// `presentation_comment`: the text entered, its source of a closed list, the version as the concurrency token, the
/// author a user.
/// </summary>
internal sealed class FlowPresentationCommentConfiguration : IEntityTypeConfiguration<Application.Presentation.FlowPresentationComment>
{
    public void Configure(EntityTypeBuilder<Application.Presentation.FlowPresentationComment> builder)
    {
        builder.ToTable(TableNames.FlowPresentationComment);
        builder.KeyGivenByTheApplication(comment => new { comment.FlowId, comment.Language });
        builder.References<Application.Presentation.FlowPresentationComment, Flow>(comment => comment.FlowId);
        builder.Text(comment => comment.Text, Application.Presentation.PresentationCommentRules.TextMax);
        builder.Language(comment => comment.Language);
        builder.Code(comment => comment.Source, 10);
        builder.Property(comment => comment.Version).IsConcurrencyToken();
        builder.References<Application.Presentation.FlowPresentationComment, AppUser>(comment => comment.UpdatedBy);
    }
}

/// <summary>
/// `presentation_voice` (3.2, ADR 103, lot J10): one row per project and language, its key (a voice follows its project to
/// the trash and back, and goes before it at its purge); the name of the voice as the browser gives it, whether it is a
/// voice of the computer (otherwise one read online), the version as the concurrency token, the author a user.
/// </summary>
internal sealed class PresentationVoiceConfiguration : IEntityTypeConfiguration<Application.Presentation.PresentationVoice>
{
    public void Configure(EntityTypeBuilder<Application.Presentation.PresentationVoice> builder)
    {
        builder.ToTable(TableNames.PresentationVoice);
        builder.KeyGivenByTheApplication(voice => new { voice.ProjectId, voice.Language });
        builder.References<Application.Presentation.PresentationVoice, Project>(voice => voice.ProjectId);
        builder.Language(voice => voice.Language);
        builder.Text(voice => voice.Name, Application.Presentation.PresentationVoiceRules.NameMax);
        builder.Property(voice => voice.Version).IsConcurrencyToken();
        builder.References<Application.Presentation.PresentationVoice, AppUser>(voice => voice.UpdatedBy);
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
