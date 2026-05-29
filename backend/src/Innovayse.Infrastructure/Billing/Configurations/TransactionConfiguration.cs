namespace Innovayse.Infrastructure.Billing.Configurations;

using Innovayse.Domain.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>EF Core configuration for the <see cref="Transaction"/> entity.</summary>
public sealed class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("transactions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ClientId).IsRequired();
        builder.Property(x => x.InvoiceId);
        builder.Property(x => x.Type).IsRequired().HasMaxLength(20);
        builder.Property(x => x.Amount).HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(x => x.Currency).IsRequired().HasMaxLength(3);
        builder.Property(x => x.Gateway).HasMaxLength(100);
        builder.Property(x => x.TransactionId).HasMaxLength(255);
        builder.Property(x => x.Description).IsRequired().HasMaxLength(500);
        builder.Property(x => x.CreatedAt).IsRequired();
    }
}
