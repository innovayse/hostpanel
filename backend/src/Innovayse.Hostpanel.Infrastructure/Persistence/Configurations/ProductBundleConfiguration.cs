using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Innovayse.Hostpanel.Infrastructure.Persistence.Configurations;

/// <summary>EF Core table and column configuration for <see cref="ProductBundle"/>.</summary>
public sealed class ProductBundleConfiguration : IEntityTypeConfiguration<ProductBundle>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ProductBundle> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(ProductBundle.NameMaxLength);
        builder.Property(x => x.Description).HasMaxLength(ProductBundle.DescriptionMaxLength);
        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.SortOrder).IsRequired();
        builder.Property(x => x.IncludesDomain).IsRequired();
        builder.Property(x => x.DomainYears);
        builder.Property(x => x.DiscountType).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.DiscountAmount).IsRequired().HasPrecision(18, 4);
        builder.Property(x => x.DiscountCurrency).HasMaxLength(CurrencyCode.Length);
        builder.Ignore(x => x.DomainEvents);

        // The storefront lists the active bundles in the operator's order.
        builder.HasIndex(x => new { x.IsActive, x.SortOrder });

        // Backstops for the rules the aggregate enforces, so a row written around it is refused too.
        // The enum is stored as text, so the rule names its members.
        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "CK_ProductBundles_DomainTerm",
                $"(\"IncludesDomain\" AND \"DomainYears\" BETWEEN {ProductBundle.MinDomainYears} AND {ProductBundle.MaxDomainYears}) OR (NOT \"IncludesDomain\" AND \"DomainYears\" IS NULL)");
            table.HasCheckConstraint(
                "CK_ProductBundles_Discount",
                $"(\"DiscountType\" = '{nameof(BundleDiscountType.None)}' AND \"DiscountAmount\" = 0 AND \"DiscountCurrency\" IS NULL)"
                + $" OR (\"DiscountType\" = '{nameof(BundleDiscountType.Percentage)}' AND \"DiscountAmount\" BETWEEN 0 AND {ProductBundle.MaxDiscountPercentage} AND \"DiscountCurrency\" IS NULL)"
                + $" OR (\"DiscountType\" = '{nameof(BundleDiscountType.Fixed)}' AND \"DiscountAmount\" >= 0 AND \"DiscountCurrency\" IS NOT NULL)");
        });

        builder.HasMany(x => x.Items)
            .WithOne()
            .HasForeignKey(i => i.BundleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Items).HasField("_items");
    }
}
