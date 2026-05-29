namespace Innovayse.Infrastructure.Billing.Configurations;

using Innovayse.Domain.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>EF Core configuration for the <see cref="QuoteItem"/> owned entity.</summary>
public sealed class QuoteItemConfiguration : IEntityTypeConfiguration<QuoteItem>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<QuoteItem> builder)
    {
        builder.ToTable("quote_items");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.QuoteId).IsRequired();
        builder.Property(x => x.Description).IsRequired().HasMaxLength(500);
        builder.Property(x => x.UnitPrice).HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(x => x.Quantity).IsRequired();
    }
}
