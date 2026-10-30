using Innovayse.Hostpanel.Domain.Products;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Innovayse.Hostpanel.Infrastructure.Persistence.Configurations;

/// <summary>EF Core table and column configuration for <see cref="Product"/>.</summary>
public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.GroupId).IsRequired();
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.Website).HasMaxLength(Product.WebsiteMaxLength);
        builder.Property(x => x.Slug).HasMaxLength(200);
        builder.Property(x => x.PackageName).HasMaxLength(100);
        builder.Property(x => x.Type).IsRequired().HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.MonthlyPrice).IsRequired().HasColumnType("numeric(18,4)");
        builder.Property(x => x.AnnualPrice).IsRequired().HasColumnType("numeric(18,4)");
        builder.Property(x => x.ServerGroupId).IsRequired(false);
        builder.Property(x => x.IsFeatured).IsRequired();
        builder.Property(x => x.SortOrder).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();

        // A SKU names one product only, so it is unique; most products have none, and the filter keeps
        // every product without one out of the index instead of making them collide on null.
        builder.Property(x => x.Sku).HasMaxLength(ProductSku.MaxLength);
        builder.HasIndex(x => x.Sku)
            .IsUnique()
            .HasFilter("\"Sku\" IS NOT NULL");

        // The metadata bag is a JSON object the domain has already validated; jsonb keeps it queryable.
        builder.Property(x => x.Metadata).HasColumnType("jsonb");

        // The default fills the column on rows that existed before it. The sentinel is a value the domain
        // never holds, so EF always writes the product's own grace period: with the CLR default 0 as the
        // sentinel, a product saved with no grace period at all would silently get the database's 7.
        builder.Property(x => x.GracePeriodDays)
            .IsRequired()
            .HasDefaultValue(Product.DefaultGracePeriodDays)
            .HasSentinel(int.MinValue);

        builder.ToTable(table => table.HasCheckConstraint(
            "CK_Products_GracePeriodDays",
            $"\"GracePeriodDays\" BETWEEN {Product.MinGracePeriodDays} AND {Product.MaxGracePeriodDays}"));

        // Per-currency prices. ProductPrice.ProductId is never set in memory, SetPrice leaves it 0,
        // so this foreign key is what stamps it when the product is saved.
        builder.HasMany(x => x.Prices)
            .WithOne()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
        // The backing field, not the read-only wrapper, so EF tracks adds and removes.
        builder.Navigation(x => x.Prices)
            .HasField("_prices")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(x => x.DomainEvents);
    }
}
