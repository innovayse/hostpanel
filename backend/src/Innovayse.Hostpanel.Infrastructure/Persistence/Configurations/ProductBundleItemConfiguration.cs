using Innovayse.Hostpanel.Domain.Products;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Innovayse.Hostpanel.Infrastructure.Persistence.Configurations;

/// <summary>EF Core table and column configuration for <see cref="ProductBundleItem"/>.</summary>
public sealed class ProductBundleItemConfiguration : IEntityTypeConfiguration<ProductBundleItem>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ProductBundleItem> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.BundleId).IsRequired();
        builder.Property(x => x.ProductId).IsRequired();
        builder.Property(x => x.IsPrimary).IsRequired();

        // A product appears in a bundle once.
        builder.HasIndex(x => new { x.BundleId, x.ProductId }).IsUnique();

        // A product that is still a component of a bundle cannot be deleted: the bundle would silently
        // lose the component and could end up without a primary one.
        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.ProductId);
    }
}
