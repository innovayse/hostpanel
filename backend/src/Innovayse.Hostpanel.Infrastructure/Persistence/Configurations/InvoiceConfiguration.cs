using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Clients;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Innovayse.Hostpanel.Infrastructure.Persistence.Configurations;

/// <summary>EF Core configuration for the <see cref="Invoice"/> aggregate.</summary>
public sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    /// <summary>Column width for a three-character ISO 4217 currency code.</summary>
    private const int CurrencyCodeLength = 3;

    /// <summary>Column width for the source system's invoice number.</summary>
    private const int ExternalIdLength = 100;

    /// <summary>Configures the <c>Invoices</c> table mapping.</summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.HasKey(x => x.Id);

        // Guards CompleteGatewayPaymentHandler's read-then-write racing itself (result-page
        // auto-check vs. retry button vs. the reconciler) and double-recording the same bank payment.
        builder.UseXminConcurrencyToken();

        builder.Property(x => x.ClientId).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.DueDate).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.PaidAt);
        builder.Property(x => x.LapsedAt);

        // Recomputed from the lines on every change; the default is what every invoice raised before
        // product snapshots were kept is, since none of its lines can be known to be digital.
        builder.Property(x => x.HasDigitalItems).IsRequired().HasDefaultValue(false);

        builder.Property(x => x.Total).HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(x => x.GatewayTransactionId).HasMaxLength(255);
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.Property(x => x.InvoiceDate).IsRequired();
        builder.Property(x => x.PaymentMethod).HasMaxLength(100);
        builder.Property(x => x.GatewayModule).HasMaxLength(100);
        builder.Property(x => x.GatewayOrderId).HasMaxLength(255);
        builder.Property(x => x.GatewayStartedAt);
        builder.Property(x => x.GatewayCheckedAt);
        builder.Property(x => x.GatewayCheckCount).HasDefaultValue(0).IsRequired();
        builder.Property(x => x.GatewayConcludedAt);
        builder.Property(x => x.GatewayStalledNotifiedAt);
        builder.Property(x => x.TaxRate).HasColumnType("numeric(5,2)").IsRequired();
        builder.Property(x => x.Tax).HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(x => x.SubTotal).HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(x => x.Credit).HasColumnType("numeric(18,4)").IsRequired();
        // The ISO 4217 code every amount on this invoice is in. Backfilled to USD by the
        // AddInvoiceCurrency migration, because every invoice raised before it was a dollar figure.
        builder.Property(x => x.Currency).IsRequired().HasMaxLength(CurrencyCodeLength);

        // The source system's invoice number, kept for migration de-duplication. A source number is
        // unique only within one source, so it is unique per client, and only where one is set.
        builder.Property(x => x.ExternalId).HasMaxLength(ExternalIdLength);

        // One index per read the handlers run: the client's invoice list, the overdue sweep
        // (Status, DueDate), the newest-first lists, the paid-in-period reports (Status, PaidAt),
        // and the reconciler's gateway-session lookup, which only ever reads invoices with a session.
        builder.HasIndex(x => x.ClientId);
        builder.HasIndex(x => new { x.Status, x.DueDate });
        builder.HasIndex(x => x.CreatedAt);
        builder.HasIndex(x => new { x.Status, x.PaidAt });
        builder.HasIndex(x => x.GatewayOrderId)
            .HasFilter("\"GatewayOrderId\" IS NOT NULL");
        builder.HasIndex(x => new { x.ClientId, x.ExternalId })
            .IsUnique()
            .HasFilter("\"ExternalId\" IS NOT NULL");

        // Navigation: Invoice owns a collection of InvoiceItems via private backing field _items.
        builder.HasMany(x => x.Items)
            .WithOne()
            .HasForeignKey(x => x.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Items)
            .HasField("_items")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // Navigation: Invoice owns a collection of InvoiceTransactions via private backing field _transactions.
        builder.HasMany(x => x.Transactions)
            .WithOne()
            .HasForeignKey(x => x.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Transactions)
            .HasField("_transactions")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // A client who still has this record cannot be deleted: billing, service and support history
        // is kept, so the database refuses the delete instead of leaving rows without an owner.
        builder.HasOne<Client>()
            .WithMany()
            .HasForeignKey(x => x.ClientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
