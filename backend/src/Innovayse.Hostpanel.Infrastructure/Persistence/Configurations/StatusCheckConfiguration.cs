using Innovayse.Hostpanel.Domain.StatusPage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Innovayse.Hostpanel.Infrastructure.Persistence.Configurations;

/// <summary>EF Core table and column configuration for <see cref="StatusCheck"/>.</summary>
public sealed class StatusCheckConfiguration : IEntityTypeConfiguration<StatusCheck>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<StatusCheck> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ComponentId).IsRequired();
        builder.Property(x => x.CheckedAt).IsRequired();
        builder.Property(x => x.Succeeded).IsRequired();
        builder.Property(x => x.LatencyMs).IsRequired();

        // The uptime figures read a component's checks over a window, and the pruning reads them by age.
        builder.HasIndex(x => new { x.ComponentId, x.CheckedAt });
        builder.HasIndex(x => x.CheckedAt);

        builder.HasOne<StatusComponent>()
            .WithMany()
            .HasForeignKey(x => x.ComponentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
