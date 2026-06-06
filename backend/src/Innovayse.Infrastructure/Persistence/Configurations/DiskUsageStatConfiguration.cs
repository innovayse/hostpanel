namespace Innovayse.Infrastructure.Persistence.Configurations;

using Innovayse.Domain.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>EF Core table configuration for <see cref="DiskUsageStat"/>.</summary>
public sealed class DiskUsageStatConfiguration : IEntityTypeConfiguration<DiskUsageStat>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<DiskUsageStat> builder)
    {
        builder.ToTable("disk_usage_stats");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ServerName).IsRequired().HasMaxLength(253);
        builder.Property(x => x.Domain).IsRequired().HasMaxLength(253);
        builder.HasIndex(x => x.Domain).IsUnique();
        builder.Property(x => x.ClientName).IsRequired().HasMaxLength(300);
        builder.Property(x => x.DiskUsageMb).IsRequired();
        builder.Property(x => x.DiskLimitMb).IsRequired();
        builder.Property(x => x.BwUsageMb).IsRequired();
        builder.Property(x => x.BwLimitMb).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();
    }
}
