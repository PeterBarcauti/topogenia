// AppDbContext.cs
// The EF Core context shared by both providers: one model of the records of the domain, snake_case names, UTC dates (D02, spec 4.1).

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Topogenia.Persistence.Model;

namespace Topogenia.Persistence;

/// <summary>
/// One model for both databases (D02). The immutable records of the domain are mapped as they are (constructors, Guid
/// keys given by the application, integer versions as concurrency tokens, closed lists as their codes, UTC dates); the
/// provider adds its schema, the filters of the partial unique indexes and the CHECK constraints through its model
/// customizer (ADR 025). No foreign key gets an automatic index: the indexes are those of the configurations (1.x).
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);
        configurationBuilder.Conventions.Remove<ForeignKeyIndexConvention>();
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<UtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        ModelConfigurations.Apply(modelBuilder);
        ModelNaming.Apply(modelBuilder);
    }
}
