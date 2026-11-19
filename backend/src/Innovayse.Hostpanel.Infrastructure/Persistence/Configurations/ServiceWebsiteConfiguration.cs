using Innovayse.Hostpanel.Domain.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Innovayse.Hostpanel.Infrastructure.Persistence.Configurations;

/// <summary>EF Core configuration for <see cref="ServiceWebsite"/> (table <c>ServiceWebsites</c>); the shared columns are in <see cref="HostingResourceConfiguration"/>.</summary>
public sealed class ServiceWebsiteConfiguration : IEntityTypeConfiguration<ServiceWebsite>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ServiceWebsite> builder)
    {
        builder.Property(x => x.WebsiteId).IsRequired();
        builder.HasIndex(x => x.WebsiteId).IsUnique();
        builder.Property(x => x.Hostname).IsRequired().HasMaxLength(253);
        builder.Property(x => x.DocumentRoot).IsRequired().HasMaxLength(500);
        builder.Property(x => x.PhpVersion).HasMaxLength(16);
        builder.Property(x => x.DocumentRootMayBeLink).IsRequired();

        // The database, not only the handler, refuses the same host name twice on one service, so two concurrent
        // adds cannot both be recorded. Removed rows are history and do not count.
        builder.HasIndex(x => new { x.ClientServiceId, x.Hostname })
            .IsUnique()
            .HasFilter(HostingResourceConfiguration.NotRemovedFilter)
            .HasDatabaseName("IX_ServiceWebsites_ClientServiceId_Hostname");
    }
}
