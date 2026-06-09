namespace Innovayse.Infrastructure.Persistence.Configurations;

using Innovayse.Domain.Migration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>EF Core table configuration for <see cref="MigrationJob"/>.</summary>
public sealed class MigrationJobConfiguration : IEntityTypeConfiguration<MigrationJob>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<MigrationJob> builder)
    {
        builder.ToTable("migration_jobs");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Key).IsRequired().HasMaxLength(64);
        builder.HasIndex(x => x.Key).IsUnique();

        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Label).HasMaxLength(200);
        builder.Property(x => x.ErrorMessage).HasMaxLength(1000);
        builder.Property(x => x.CreatedAt).IsRequired();

        builder.Property(x => x.SourceUrl).IsRequired().HasMaxLength(500).HasDefaultValue(string.Empty);

        builder.Property(x => x.ExportClients).IsRequired().HasDefaultValue(true);
        builder.Property(x => x.ExportInvoices).IsRequired().HasDefaultValue(true);
        builder.Property(x => x.ExportServices).IsRequired().HasDefaultValue(true);
        builder.Property(x => x.ExportDomains).IsRequired().HasDefaultValue(true);
        builder.Property(x => x.ExportTickets).IsRequired().HasDefaultValue(true);
    }
}
