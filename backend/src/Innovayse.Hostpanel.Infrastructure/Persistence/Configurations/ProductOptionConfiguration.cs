using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Innovayse.Hostpanel.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core table and column configuration for <see cref="ProductOption"/>. The foreign key to the
/// group is configured from the group's side, in <see cref="ProductOptionGroupConfiguration"/>.
/// </summary>
public sealed class ProductOptionConfiguration : IEntityTypeConfiguration<ProductOption>
{
    /// <summary>Column width for a <see cref="ProductOptionPriceType"/> name stored as text.</summary>
    private const int PriceTypeLength = 16;

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ProductOption> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ProductOptionGroupId).IsRequired();
        builder.Property(x => x.Name).IsRequired().HasMaxLength(ProductOption.NameMaxLength);

        // The same column type as a product price, so a modifier and a price are the same kind of money.
        builder.Property(x => x.PriceModifier).HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(x => x.SetupFeeModifier).HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(x => x.PriceType).IsRequired().HasConversion<string>().HasMaxLength(PriceTypeLength);
        builder.Property(x => x.DisplayOrder).IsRequired();

        // The database backs up the aggregate's rules, so a row written around it is still refused.
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_ProductOptions_DisplayOrderNotNegative", "\"DisplayOrder\" >= 0");
            table.HasCheckConstraint(
                "CK_ProductOptions_ModifiersNotNegative",
                "\"PriceModifier\" >= 0 AND \"SetupFeeModifier\" >= 0");
            table.HasCheckConstraint(
                "CK_ProductOptions_PercentageAtMost100",
                "\"PriceType\" <> 'Percentage' OR (\"PriceModifier\" <= 100 AND \"SetupFeeModifier\" <= 100)");
        });

        // A group's options are read together, in display order.
        builder.HasIndex(x => new { x.ProductOptionGroupId, x.DisplayOrder });
    }
}
