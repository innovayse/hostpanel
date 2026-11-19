using Innovayse.Hostpanel.Domain.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Innovayse.Hostpanel.Infrastructure.Persistence.Configurations;

/// <summary>EF Core configuration for <see cref="ServiceDatabase"/> (table <c>ServiceDatabases</c>); the shared columns are in <see cref="HostingResourceConfiguration"/>.</summary>
public sealed class ServiceDatabaseConfiguration : IEntityTypeConfiguration<ServiceDatabase>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ServiceDatabase> builder)
    {
        builder.Property(x => x.DatabaseId).IsRequired();
        builder.HasIndex(x => x.DatabaseId).IsUnique();
        builder.Property(x => x.Label).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(64);
        builder.Property(x => x.Username).IsRequired().HasMaxLength(64);

        // The database, not only the handler, refuses the same label twice for one plugin on one service, so two
        // concurrent adds cannot both be recorded. Removed rows are history and do not count.
        builder.HasIndex(x => new { x.ClientServiceId, x.IssuedToPluginId, x.Label })
            .IsUnique()
            .HasFilter(HostingResourceConfiguration.NotRemovedFilter)
            .HasDatabaseName("IX_ServiceDatabases_ClientServiceId_IssuedToPluginId_Label");
    }
}
