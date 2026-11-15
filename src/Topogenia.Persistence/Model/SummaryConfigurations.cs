// SummaryConfigurations.cs
// Table of the summaries of the maps, one row per map at most, beside the map as a sheet beside its step (plan 3.1, lot R).

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Topogenia.Domain.Model;
using Topogenia.Domain.Structure;
using Topogenia.Domain.Summaries;

namespace Topogenia.Persistence.Model;

internal sealed class MapSummaryConfiguration : IEntityTypeConfiguration<MapSummary>
{
    public void Configure(EntityTypeBuilder<MapSummary> builder)
    {
        builder.ToTable(TableNames.MapSummary);
        builder.KeyGivenByTheApplication(summary => summary.Id);
        builder.References<MapSummary, ProcessMap>(summary => summary.MapId);
        builder.HasIndex(summary => summary.MapId).IsUnique().HasDatabaseName("UQ_map_summary_map_id");
        builder.Text(summary => summary.Text, StructureEdits.MapSummaryMax);
        builder.Language(summary => summary.SourceLanguage);
        builder.Code(summary => summary.Origin, 6);
        builder.Text(summary => summary.Model, MapSummaryRules.ModelMax);
        builder.Ascii(summary => summary.Fingerprint, MapFingerprint.Length);
        builder.AuditedBy(summary => summary.CreatedBy, summary => summary.UpdatedBy);
        builder.Versioned();
    }
}
