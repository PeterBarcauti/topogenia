// SnapshotConfigurations.cs
// Table of the photos of the indicators of a project (Makigami M6, M8, plan 3.1): its indicators in typed columns, frozen, beside the project.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Topogenia.Domain.Model;
using Topogenia.Domain.Snapshots;

namespace Topogenia.Persistence.Model;

internal sealed class StreamSnapshotConfiguration : IEntityTypeConfiguration<StreamSnapshot>
{
    public void Configure(EntityTypeBuilder<StreamSnapshot> builder)
    {
        builder.ToTable(TableNames.StreamSnapshot);
        builder.KeyGivenByTheApplication(snapshot => snapshot.Id);
        builder.References<StreamSnapshot, Project>(snapshot => snapshot.ProjectId);
        builder.HasIndex(snapshot => snapshot.ProjectId);
        builder.Text(snapshot => snapshot.Label, SnapshotRules.LabelMax);
        builder.Code(snapshot => snapshot.Origin, 8);
        builder.AuditedBy(snapshot => snapshot.CreatedBy, snapshot => snapshot.UpdatedBy);
        builder.Versioned();
    }
}
