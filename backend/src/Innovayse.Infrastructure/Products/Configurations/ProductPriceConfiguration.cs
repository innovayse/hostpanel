namespace Innovayse.Infrastructure.Products.Configurations;

using Innovayse.Domain.Products;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>EF Core mapping for <see cref="ProductPrice"/>.</summary>
public sealed class ProductPriceConfiguration : IEntityTypeConfiguration<ProductPrice>
{
    /// <summary>Column width for a three-character ISO code.</summary>
    private const int IsoCodeLength = 3;

    /// <summary>Column width for a <see cref="BillingCycle"/> name stored as text.</summary>
    private const int CycleLength = 16;

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ProductPrice> builder)
    {
        builder.ToTable("product_prices");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ProductId).IsRequired();
        builder.Property(x => x.CurrencyCode).IsRequired().HasMaxLength(IsoCodeLength);
        builder.Property(x => x.Cycle).IsRequired().HasConversion<string>().HasMaxLength(CycleLength);
        builder.Property(x => x.Amount).HasColumnType("numeric(18,4)").IsRequired();
        // One price per product, currency and cycle — SetPrice replaces, it never adds a second.
        builder.HasIndex(x => new { x.ProductId, x.CurrencyCode, x.Cycle }).IsUnique();
    }
}
