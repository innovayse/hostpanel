namespace Innovayse.Infrastructure.Products.Configurations;

using Innovayse.Domain.Products;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>EF Core table and column configuration for <see cref="Product"/>.</summary>
public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.GroupId).IsRequired();
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.Type).IsRequired().HasConversion<string>();
        builder.Property(x => x.Status).IsRequired().HasConversion<string>();
        builder.Property(x => x.MonthlyPrice).IsRequired().HasColumnType("numeric(18,4)");
        builder.Property(x => x.AnnualPrice).IsRequired().HasColumnType("numeric(18,4)");
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Ignore(x => x.DomainEvents);
    }
}
