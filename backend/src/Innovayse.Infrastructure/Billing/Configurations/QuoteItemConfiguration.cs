namespace Innovayse.Infrastructure.Billing.Configurations;

using Innovayse.Domain.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

<<<<<<< HEAD
/// <summary>EF Core configuration for the <see cref="QuoteItem"/> owned entity.</summary>
public sealed class QuoteItemConfiguration : IEntityTypeConfiguration<QuoteItem>
{
    /// <inheritdoc/>
=======
/// <summary>EF Core configuration for the <see cref="QuoteItem"/> entity.</summary>
public sealed class QuoteItemConfiguration : IEntityTypeConfiguration<QuoteItem>
{
    /// <summary>Configures the <c>quote_items</c> table mapping.</summary>
    /// <param name="builder">The entity type builder.</param>
>>>>>>> origin/main
    public void Configure(EntityTypeBuilder<QuoteItem> builder)
    {
        builder.ToTable("quote_items");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.QuoteId).IsRequired();
<<<<<<< HEAD
        builder.Property(x => x.Description).IsRequired().HasMaxLength(500);
        builder.Property(x => x.UnitPrice).HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(x => x.Quantity).IsRequired();
=======
        builder.Property(x => x.Quantity).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(500).IsRequired();
        builder.Property(x => x.UnitPrice).HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(x => x.DiscountPercent).HasColumnType("numeric(5,2)").IsRequired();
        builder.Property(x => x.Taxed).IsRequired();
        builder.Property(x => x.Amount).HasColumnType("numeric(18,4)").IsRequired();
>>>>>>> origin/main
    }
}
