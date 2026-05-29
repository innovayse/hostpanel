namespace Innovayse.Infrastructure.Billing.Configurations;

using Innovayse.Domain.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>EF Core configuration for the <see cref="InvoiceTransaction"/> entity.</summary>
public sealed class InvoiceTransactionConfiguration : IEntityTypeConfiguration<InvoiceTransaction>
{
    /// <summary>Configures the <c>invoice_transactions</c> table mapping.</summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<InvoiceTransaction> builder)
    {
        builder.ToTable("invoice_transactions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.InvoiceId).IsRequired();
        builder.Property(x => x.Date).IsRequired();
        builder.Property(x => x.Gateway).HasMaxLength(100).IsRequired();
        builder.Property(x => x.TransactionId).HasMaxLength(255).IsRequired();
        builder.Property(x => x.Amount).HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(x => x.Fees).HasColumnType("numeric(18,4)").IsRequired().HasDefaultValue(0m);
        builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(1000);
    }
}
