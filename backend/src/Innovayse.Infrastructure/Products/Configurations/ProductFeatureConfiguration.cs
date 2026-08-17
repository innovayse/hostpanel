namespace Innovayse.Infrastructure.Products.Configurations;

using Innovayse.Domain.Products;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>EF Core table and column configuration for <see cref="ProductFeature"/>.</summary>
public sealed class ProductFeatureConfiguration : IEntityTypeConfiguration<ProductFeature>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ProductFeature> builder)
    {
        builder.ToTable("product_features");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ProductId).IsRequired();
        builder.Property(x => x.Label).IsRequired().HasMaxLength(120);
        builder.Property(x => x.Value).IsRequired().HasMaxLength(200);
        builder.Property(x => x.SortOrder).IsRequired();

        // Deleting a product takes its specification with it: these lines describe
        // that product and mean nothing without it.
        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        // The storefront reads every line for a set of products at once, ordered.
        builder.HasIndex(x => new { x.ProductId, x.SortOrder });
    }
}
