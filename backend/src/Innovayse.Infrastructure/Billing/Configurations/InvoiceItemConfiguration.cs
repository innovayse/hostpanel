namespace Innovayse.Infrastructure.Billing.Configurations;

using Innovayse.Domain.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>EF Core configuration for the <see cref="InvoiceItem"/> entity.</summary>
public sealed class InvoiceItemConfiguration : IEntityTypeConfiguration<InvoiceItem>
{
    /// <summary>Configures the <c>invoice_items</c> table mapping.</summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<InvoiceItem> builder)
    {
        builder.ToTable("invoice_items");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.InvoiceId).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(500).IsRequired();
        builder.Property(x => x.UnitPrice).HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(x => x.Quantity).IsRequired();
        builder.Property(x => x.Amount).HasColumnType("numeric(18,4)").IsRequired();
    }
}
