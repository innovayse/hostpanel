namespace Innovayse.Infrastructure.Billing.Configurations;

using Innovayse.Domain.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>EF Core configuration for the <see cref="Transaction"/> entity.</summary>
public sealed class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    /// <summary>Configures the <c>transactions</c> table mapping.</summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("transactions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ClientId).IsRequired();
        builder.Property(x => x.Date).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(500).IsRequired();
        builder.Property(x => x.TransactionId).HasMaxLength(255).IsRequired();
        builder.Property(x => x.InvoiceId);
        builder.Property(x => x.PaymentMethod).HasMaxLength(100).IsRequired();
        builder.Property(x => x.AmountIn).HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(x => x.AmountOut).HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(x => x.Fees).HasColumnType("numeric(18,4)").IsRequired().HasDefaultValue(0m);
        builder.Property(x => x.AddedToCredit).IsRequired().HasDefaultValue(false);

        builder.HasIndex(x => x.ClientId);
    }
}
