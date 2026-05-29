namespace Innovayse.Infrastructure.Persistence.Configurations;

using Innovayse.Domain.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>EF Core table configuration for <see cref="NetworkIssue"/>.</summary>
public sealed class NetworkIssueConfiguration : IEntityTypeConfiguration<NetworkIssue>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<NetworkIssue> builder)
    {
        builder.ToTable("network_issues");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title).IsRequired().HasMaxLength(500);
        builder.Property(x => x.Type).IsRequired().HasConversion<string>().HasMaxLength(50);
        builder.Property(x => x.Server).HasMaxLength(255);
        builder.Property(x => x.Priority).IsRequired().HasConversion<string>().HasMaxLength(50);
        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(50);
        builder.Property(x => x.StartDate).IsRequired();
        builder.Property(x => x.EndDate);
        builder.Property(x => x.Description).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();

        builder.HasIndex(x => x.Status);
    }
}
