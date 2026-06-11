namespace Innovayse.Infrastructure.Persistence.Configurations;

using Innovayse.Domain.Migration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>EF Core table configuration for <see cref="MigrationLog"/>.</summary>
public sealed class MigrationLogConfiguration : IEntityTypeConfiguration<MigrationLog>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<MigrationLog> builder)
    {
        builder.ToTable("migration_logs");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.JobId).IsRequired();
        builder.Property(x => x.EntityType).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Identifier).IsRequired().HasMaxLength(300);
        builder.Property(x => x.Action).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Reason).HasMaxLength(500);
        builder.Property(x => x.CreatedAt).IsRequired();

        builder.HasIndex(x => x.JobId);
        builder.HasIndex(x => new { x.JobId, x.Action });
    }
}
