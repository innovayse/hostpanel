using Innovayse.Hostpanel.Domain.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Innovayse.Hostpanel.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for what every <see cref="HostingResource"/> shares. The base is abstract and has no table of
/// its own (table-per-concrete-type): <c>ServiceWebsites</c> and <c>ServiceDatabases</c> each carry these columns.
/// </summary>
public sealed class HostingResourceConfiguration : IEntityTypeConfiguration<HostingResource>
{
    /// <summary>The filter that leaves removed rows, which are history, out of a unique index.</summary>
    public const string NotRemovedFilter = "\"State\" <> 'Removed'";

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<HostingResource> builder)
    {
        builder.UseTpcMappingStrategy();
        builder.HasKey(x => x.Id);
        builder.Property(x => x.AccountUsername).IsRequired().HasMaxLength(64);

        // The plugin id rule (PluginIdentifier) caps an id at 64 characters.
        builder.Property(x => x.IssuedToPluginId).IsRequired().HasMaxLength(64);
        builder.Property(x => x.State).IsRequired().HasConversion<string>().HasMaxLength(20);

        builder.HasOne<ClientService>()
            .WithMany()
            .HasForeignKey(x => x.ClientServiceId)
            .OnDelete(DeleteBehavior.Cascade);

        // No foreign key to Servers on purpose: the pinned server id must survive the server row
        // being deleted, so the record still says where the resource was created.
        builder.HasIndex(x => x.ServerId);
    }
}
