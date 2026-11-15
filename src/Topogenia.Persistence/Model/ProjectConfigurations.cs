// ProjectConfigurations.cs
// Tables of the users, projects, counters, members and deletion batches (spec 4.1, 1.x `db/001`, `db/006`).

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Topogenia.Domain.Model;
using Topogenia.Domain.Projects;
using Topogenia.Domain.Values;

namespace Topogenia.Persistence.Model;

internal sealed class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.ToTable(TableNames.AppUser);
        builder.KeyGivenByTheApplication(user => user.Id);
        // The login keeps its case for display (spec 4.1); uniqueness is on the normalised form, never on a collation.
        builder.Text(user => user.Login, Login.MaxLength).HasColumnName("windows_login");
        builder.Text(user => user.LoginNormalized, Login.MaxLength);
        builder.HasIndex(user => user.LoginNormalized).IsUnique().HasDatabaseName("UQ_app_user_login_normalized");
        builder.Text(user => user.DisplayName, UserRules.DisplayNameMax);
        builder.Text(user => user.Email, UserRules.EmailMax);
        builder.Property(user => user.Language).HasMaxLength(ConfigurationExtensions.LanguageLength).IsUnicode(false);
        builder.References<AppUser, AppUser>(user => user.UpdatedBy);
        builder.Versioned();
    }
}

internal sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable(TableNames.Project);
        builder.KeyGivenByTheApplication(project => project.Id);
        // A code is reserved while its project is in the trash (spec 4.5): unique over every row.
        builder.Ascii(project => project.Code, 6);
        builder.HasIndex(project => project.Code).IsUnique().HasDatabaseName("UQ_project_code");
        builder.Text(project => project.Name, ProjectCreation.NameMax);
        builder.Language(project => project.SourceLanguage);
        builder.Text(project => project.Description, ProjectCreation.DescriptionMax);
        builder.Text(project => project.SharedFolder, ProjectCreation.SharedFolderMax);
        builder.Code(project => project.Classification, 24);
        builder.Text(project => project.SharingGroupLabel, ProjectCreation.SharingGroupLabelMax);
        builder.Text(project => project.TranslationGlossary, ProjectCreation.TranslationGlossaryMax);
        // The work time of the value stream (spec 14.2, S6): working day and hours of a full-time equivalent in a month.
        builder.RequiredDecimal(project => project.WorkdayHours, 4, 2);
        builder.RequiredDecimal(project => project.FteHoursPerMonth, 5, 2);
        builder.AuditedBy(project => project.CreatedBy, project => project.UpdatedBy);
        builder.References<Project, AppUser>(project => project.DeletedBy);
        builder.Versioned();
    }
}

internal sealed class ProjectCounterConfiguration : IEntityTypeConfiguration<ProjectCounter>
{
    public void Configure(EntityTypeBuilder<ProjectCounter> builder)
    {
        builder.ToTable(TableNames.ProjectCounter);
        builder.KeyGivenByTheApplication(counter => new { counter.ProjectId, counter.Counter });
        builder.Code(counter => counter.Counter, 10);
        builder.References<ProjectCounter, Project>(counter => counter.ProjectId);
    }
}

internal sealed class ProjectMemberConfiguration : IEntityTypeConfiguration<ProjectMember>
{
    public void Configure(EntityTypeBuilder<ProjectMember> builder)
    {
        builder.ToTable(TableNames.ProjectMember);
        builder.KeyGivenByTheApplication(member => new { member.ProjectId, member.UserId });
        builder.Code(member => member.Role, 12);
        builder.References<ProjectMember, Project>(member => member.ProjectId);
        builder.References<ProjectMember, AppUser>(member => member.UserId);
        builder.HasIndex(member => member.UserId);
        builder.AuditedBy(member => member.CreatedBy, member => member.UpdatedBy);
        builder.Versioned();
    }
}

internal sealed class DeletionBatchConfiguration : IEntityTypeConfiguration<DeletionBatch>
{
    public void Configure(EntityTypeBuilder<DeletionBatch> builder)
    {
        builder.ToTable(TableNames.DeletionBatch);
        builder.KeyGivenByTheApplication(batch => batch.Id);
        builder.References<DeletionBatch, Project>(batch => batch.ProjectId);
        builder.HasIndex(batch => batch.ProjectId);
        // The operation that deleted (spec 4.6) is an identifier only: an operation may be trimmed from the log or never logged.
        builder.References<DeletionBatch, AppUser>(batch => batch.DeletedBy);
        builder.References<DeletionBatch, AppUser>(batch => batch.RestoredBy);
    }
}
