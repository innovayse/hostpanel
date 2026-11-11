using Innovayse.Hostpanel.Domain.StatusPage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Innovayse.Hostpanel.Infrastructure.Persistence.Configurations;

/// <summary>EF Core table and column configuration for <see cref="StatusIncident"/>.</summary>
public sealed class StatusIncidentConfiguration : IEntityTypeConfiguration<StatusIncident>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<StatusIncident> builder)
    {
        builder.HasKey(x => x.Id);

        // Two operators posting to the same incident at once cannot overwrite each other: the second save of a
        // changed row fails with a concurrency conflict (409) instead of silently winning.
        builder.UseXminConcurrencyToken();

        builder.Property(x => x.Title).IsRequired().HasMaxLength(StatusIncident.TitleMaxLength);
        builder.Property(x => x.State).IsRequired().HasConversion<string>().HasMaxLength(50);
        builder.Property(x => x.StartedAt).IsRequired();
        builder.Property(x => x.ResolvedAt);
        builder.HasIndex(x => x.StartedAt);
        builder.HasIndex(x => x.State);

        builder.HasMany(x => x.Updates)
            .WithOne()
            .HasForeignKey(x => x.IncidentId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Updates).AutoInclude();
    }
}
