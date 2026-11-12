using Innovayse.Hostpanel.Domain.StatusPage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Innovayse.Hostpanel.Infrastructure.Persistence.Configurations;

/// <summary>EF Core table and column configuration for <see cref="StatusComponent"/>.</summary>
public sealed class StatusComponentConfiguration : IEntityTypeConfiguration<StatusComponent>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<StatusComponent> builder)
    {
        builder.HasKey(x => x.Id);
        builder.UseXminConcurrencyToken();

        builder.Property(x => x.GroupId).IsRequired();
        builder.Property(x => x.Name).IsRequired().HasMaxLength(StatusComponent.NameMaxLength);
        builder.Property(x => x.Description).HasMaxLength(StatusComponent.DescriptionMaxLength);
        builder.Property(x => x.DisplayOrder).IsRequired();
        builder.Property(x => x.CurrentState).IsRequired().HasConversion<string>().HasMaxLength(50);
        builder.Property(x => x.CheckUrl).HasMaxLength(StatusComponent.CheckUrlMaxLength);
        builder.Property(x => x.MonitoringEnabled).IsRequired();
        builder.Property(x => x.FailureThreshold).IsRequired().HasDefaultValue(StatusComponent.DefaultFailureThreshold);
        builder.Property(x => x.ConsecutiveFailures).IsRequired();
        builder.Property(x => x.StateOverride).HasConversion<string>().HasMaxLength(50);
        builder.HasIndex(x => new { x.GroupId, x.DisplayOrder });

        // The incident the automatic checks opened; it outlives nothing, so a deleted incident only clears the link.
        builder.HasOne<StatusIncident>()
            .WithMany()
            .HasForeignKey(x => x.ActiveIncidentId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
