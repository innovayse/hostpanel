using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Clients;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Innovayse.Hostpanel.Infrastructure.Persistence.Configurations;

/// <summary>EF Core configuration for the <see cref="Transaction"/> entity.</summary>
public sealed class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    /// <summary>
    /// The name of the unique index that keeps a gateway from recording the same payment twice. The
    /// database context recognises a violation of it by this name, so the name is written once, here.
    /// </summary>
    public const string GatewayPaymentReferenceIndex = "IX_Transactions_PaymentMethod_TransactionId_GatewayPayment";

    /// <summary>The filter that limits <see cref="GatewayPaymentReferenceIndex"/> to gateway payment rows.</summary>
    private const string GatewayPaymentFilter = "\"Source\" = 'GatewayPayment'";

    /// <summary>Configures the <c>Transactions</c> table mapping.</summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
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
        builder.Property(x => x.Currency).IsRequired().HasMaxLength(CurrencyCode.Length);
        builder.Property(x => x.Source).IsRequired().HasConversion<string>().HasMaxLength(20);

        // The ledger lists newest first, per client or across all of them, and every report ranges
        // over the date; an invoice's payments and a gateway reference are looked up on their own.
        builder.HasIndex(x => x.Date);
        builder.HasIndex(x => x.InvoiceId);
        builder.HasIndex(x => new { x.ClientId, x.Date });
        builder.HasIndex(x => x.TransactionId);

        // A gateway reports a payment under one reference, so the same method and reference twice among
        // gateway payments is a replay the database refuses. Manual entries and imports keep no such
        // promise (a clerk may type any reference, a source system owns its own), so they are outside it.
        builder.HasIndex(x => new { x.PaymentMethod, x.TransactionId }, GatewayPaymentReferenceIndex)
            .IsUnique()
            .HasFilter(GatewayPaymentFilter);

        // A client who still has this record cannot be deleted: billing, service and support history
        // is kept, so the database refuses the delete instead of leaving rows without an owner.
        builder.HasOne<Client>()
            .WithMany()
            .HasForeignKey(x => x.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        // A ledger row outlives the invoice it settled: deleting a Draft or Cancelled invoice only clears the link.
        builder.HasOne<Invoice>()
            .WithMany()
            .HasForeignKey(x => x.InvoiceId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
