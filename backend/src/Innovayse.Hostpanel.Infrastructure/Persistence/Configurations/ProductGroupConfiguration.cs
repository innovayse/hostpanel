using System.Text.Json;
using Innovayse.Hostpanel.Domain.Products;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Innovayse.Hostpanel.Infrastructure.Persistence.Configurations;

/// <summary>EF Core table and column configuration for <see cref="ProductGroup"/>.</summary>
public sealed class ProductGroupConfiguration : IEntityTypeConfiguration<ProductGroup>
{
    /// <summary>
    /// Compares two allowed payment method lists by content and order: the list is stored as one jsonb value, so without
    /// it EF would compare references and miss a list changed in place.
    /// </summary>
    private static readonly ValueComparer<IReadOnlyList<string>> AllowedPaymentMethodsComparer = new(
        (a, b) => a == null ? b == null : b != null && a.SequenceEqual(b),
        v => v.Aggregate(0, (hash, item) => HashCode.Combine(hash, item)),
        v => v.ToList());

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ProductGroup> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.IsActive).IsRequired();

        // jsonb list of payment method module ids; empty means every method is allowed, which is also what the rows
        // that existed before the column get from the default.
        builder.Property(x => x.AllowedPaymentMethods)
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'[]'::jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null)
                     ?? new List<string>(),
                AllowedPaymentMethodsComparer)
            .HasField("_allowedPaymentMethods")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Ignore(x => x.DomainEvents);

        builder.HasMany(x => x.Products)
            .WithOne()
            .HasForeignKey(p => p.GroupId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(x => x.Products).HasField("_products");
    }
}
