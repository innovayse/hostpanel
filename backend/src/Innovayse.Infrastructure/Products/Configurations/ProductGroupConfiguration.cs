namespace Innovayse.Infrastructure.Products.Configurations;

using Innovayse.Domain.Products;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>EF Core table and column configuration for <see cref="ProductGroup"/>.</summary>
public sealed class ProductGroupConfiguration : IEntityTypeConfiguration<ProductGroup>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ProductGroup> builder)
    {
        builder.ToTable("product_groups");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.IsActive).IsRequired();
        builder.Ignore(x => x.DomainEvents);

        builder.HasMany(x => x.Products)
            .WithOne()
            .HasForeignKey(p => p.GroupId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(x => x.Products).HasField("_products");
    }
}
