using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Innovayse.Hostpanel.Infrastructure.Persistence.Configurations;

/// <summary>EF Core table and column configuration for <see cref="ProductOptionGroup"/>.</summary>
public sealed class ProductOptionGroupConfiguration : IEntityTypeConfiguration<ProductOptionGroup>
{
    /// <summary>Column width for a <see cref="ProductOptionType"/> name stored as text.</summary>
    private const int TypeLength = 16;

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ProductOptionGroup> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ProductId).IsRequired();
        builder.Property(x => x.Name).IsRequired().HasMaxLength(ProductOptionGroup.NameMaxLength);
        builder.Property(x => x.Type).IsRequired().HasConversion<string>().HasMaxLength(TypeLength);
        builder.Property(x => x.IsRequired).IsRequired();
        builder.Property(x => x.DisplayOrder).IsRequired();
        builder.Property(x => x.MinQuantity);
        builder.Property(x => x.MaxQuantity);

        // The database backs up the two rules the aggregate already enforces, so a row written
        // around it is still refused: a negative position, and a range that is not a range.
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_ProductOptionGroups_DisplayOrderNotNegative", "\"DisplayOrder\" >= 0");
            table.HasCheckConstraint(
                "CK_ProductOptionGroups_QuantityRange",
                "(\"MinQuantity\" IS NULL AND \"MaxQuantity\" IS NULL) OR "
                + "(\"MinQuantity\" >= 0 AND \"MaxQuantity\" >= 1 AND \"MaxQuantity\" >= \"MinQuantity\")");
        });

        // Deleting a product takes its option groups with it: they configure that product and
        // mean nothing without it.
        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        // Options are the group's own rows. ProductOption.ProductOptionGroupId is never set in memory,
        // so this foreign key is what stamps it when the group is saved.
        builder.HasMany(x => x.Options)
            .WithOne()
            .HasForeignKey(x => x.ProductOptionGroupId)
            .OnDelete(DeleteBehavior.Cascade);
        // The backing field, not the read-only wrapper, so EF tracks adds and removes.
        builder.Navigation(x => x.Options)
            .HasField("_options")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // A product's groups are read together, in display order.
        builder.HasIndex(x => new { x.ProductId, x.DisplayOrder });
    }
}
