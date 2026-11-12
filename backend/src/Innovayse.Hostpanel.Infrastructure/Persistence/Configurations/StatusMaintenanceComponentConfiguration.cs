using Innovayse.Hostpanel.Domain.StatusPage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Innovayse.Hostpanel.Infrastructure.Persistence.Configurations;

/// <summary>EF Core table and column configuration for <see cref="StatusMaintenanceComponent"/>.</summary>
public sealed class StatusMaintenanceComponentConfiguration : IEntityTypeConfiguration<StatusMaintenanceComponent>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<StatusMaintenanceComponent> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.MaintenanceId).IsRequired();
        builder.Property(x => x.ComponentId).IsRequired();

        // A component is listed once per window.
        builder.HasIndex(x => new { x.MaintenanceId, x.ComponentId }).IsUnique();

        // A removed component leaves every window it was listed in.
        builder.HasOne<StatusComponent>()
            .WithMany()
            .HasForeignKey(x => x.ComponentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
