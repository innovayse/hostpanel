using Innovayse.Hostpanel.Domain.StatusPage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Innovayse.Hostpanel.Infrastructure.Persistence.Configurations;

/// <summary>EF Core table and column configuration for <see cref="StatusMaintenance"/>.</summary>
public sealed class StatusMaintenanceConfiguration : IEntityTypeConfiguration<StatusMaintenance>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<StatusMaintenance> builder)
    {
        builder.HasKey(x => x.Id);
        builder.UseXminConcurrencyToken();

        builder.Property(x => x.Title).IsRequired().HasMaxLength(StatusMaintenance.TitleMaxLength);
        builder.Property(x => x.Description).HasMaxLength(StatusMaintenance.DescriptionMaxLength);
        builder.Property(x => x.ScheduledStart).IsRequired();
        builder.Property(x => x.ScheduledEnd).IsRequired();
        builder.Property(x => x.CancelledAt);

        // The public list and the monitoring pass both ask for the windows that have not ended yet.
        builder.HasIndex(x => x.ScheduledEnd);

        builder.HasMany(x => x.Components)
            .WithOne()
            .HasForeignKey(x => x.MaintenanceId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Components).AutoInclude();
    }
}
