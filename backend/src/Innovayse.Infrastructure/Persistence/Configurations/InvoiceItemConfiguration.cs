namespace Innovayse.Infrastructure.Persistence.Configurations;

using Innovayse.Domain.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>EF Core table configuration for <see cref="InvoiceItem"/>.</summary>
public sealed class InvoiceItemConfiguration : IEntityTypeConfiguration<InvoiceItem>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<InvoiceItem> builder)
    {
        builder.ToTable("invoice_items");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.InvoiceId)
            .IsRequired();

        builder.Property(x => x.Description)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.UnitPrice)
            .IsRequired()
            .HasColumnType("numeric(18,4)");

        builder.Property(x => x.Quantity)
            .IsRequired();

        builder.Property(x => x.Amount)
            .IsRequired()
            .HasColumnType("numeric(18,4)");
    }
}
