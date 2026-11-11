using Innovayse.Hostpanel.Domain.Clients;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Innovayse.Hostpanel.Infrastructure.Persistence.Configurations;

/// <summary>EF Core table and column configuration for <see cref="ClientGroup"/>.</summary>
public sealed class ClientGroupConfiguration : IEntityTypeConfiguration<ClientGroup>
{
    /// <summary>The rows that hold the default flag: the filter of the index that lets at most one group have it.</summary>
    private const string DefaultRowFilter = "\"IsDefault\"";

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ClientGroup> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(ClientGroup.NameMaxLength);
        builder.Property(x => x.Description).HasMaxLength(ClientGroup.DescriptionMaxLength);
        builder.Property(x => x.DiscountPercent).IsRequired().HasColumnType("numeric(5,2)");
        builder.Property(x => x.Color).IsRequired().HasMaxLength(ClientGroup.ColorLength);
        builder.Property(x => x.IsDefault).IsRequired();

        // At most one group is the default, for every writer: the domain clears the flag on the others
        // when it sets it, and this index refuses a second row that holds it.
        builder.HasIndex(x => x.IsDefault).IsUnique().HasFilter(DefaultRowFilter);

        builder.ToTable(table => table.HasCheckConstraint(
            "CK_ClientGroups_DiscountPercent",
            "\"DiscountPercent\" >= 0 AND \"DiscountPercent\" <= 100"));
    }
}
