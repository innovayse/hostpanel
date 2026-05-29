namespace Innovayse.Infrastructure.Audit.Configurations;

using Innovayse.Domain.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>EF Core configuration for the <see cref="ActivityLog"/> entity.</summary>
public sealed class ActivityLogConfiguration : IEntityTypeConfiguration<ActivityLog>
{
    /// <summary>Configures the <see cref="ActivityLog"/> entity mapping.</summary>
    /// <param name="builder">Entity type builder.</param>
    public void Configure(EntityTypeBuilder<ActivityLog> builder)
    {
        builder.ToTable("activity_logs");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.ClientId).IsRequired();
        builder.Property(l => l.Description).IsRequired().HasMaxLength(1000);
        builder.Property(l => l.AdminId).HasMaxLength(450);
        builder.Property(l => l.AdminName).HasMaxLength(200);
        builder.Property(l => l.AdminEmail).HasMaxLength(255);
        builder.Property(l => l.IpAddress).HasMaxLength(45);
        builder.Property(l => l.CreatedAt).IsRequired();
        builder.HasIndex(l => l.ClientId);
        builder.HasIndex(l => l.CreatedAt);
    }
}
