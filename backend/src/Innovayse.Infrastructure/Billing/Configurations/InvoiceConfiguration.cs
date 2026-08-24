namespace Innovayse.Infrastructure.Billing.Configurations;

using Innovayse.Domain.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>EF Core configuration for the <see cref="Invoice"/> aggregate.</summary>
public sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    /// <summary>Configures the <c>invoices</c> table mapping.</summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("invoices");
        builder.HasKey(x => x.Id);

        // Optimistic concurrency via PostgreSQL's system `xmin` column — no migration needed,
        // the column always exists on every table. Guards against CompleteGatewayPaymentHandler's
        // read-then-write racing itself (result-page auto-check vs. retry button vs. the
        // reconciler) and double-recording the same bank payment.
        //
        // NOTE: Npgsql.EntityFrameworkCore.PostgreSQL 9.0.3 (the version pinned by this solution)
        // does not expose a `UseXminAsConcurrencyToken()` extension method — verified by
        // reflecting the installed package assembly, no such member exists. The equivalent,
        // documented manual pattern below (a shadow `uint` property mapped to the existing
        // `xmin` column, marked as a row version) achieves the same result via core EF APIs only.
        builder.Property<uint>("xmin")
            .HasColumnName("xmin")
            .IsRowVersion();

        builder.Property(x => x.ClientId).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.DueDate).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.PaidAt);
        builder.Property(x => x.Total).HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(x => x.GatewayTransactionId).HasMaxLength(255);
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.Property(x => x.InvoiceDate).IsRequired();
        builder.Property(x => x.PaymentMethod).HasMaxLength(100);
        builder.Property(x => x.GatewayModule).HasMaxLength(100);
        builder.Property(x => x.GatewayOrderId).HasMaxLength(255);
        builder.Property(x => x.GatewayStartedAt);
        builder.Property(x => x.TaxRate).HasColumnType("numeric(5,2)").IsRequired();
        builder.Property(x => x.Tax).HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(x => x.SubTotal).HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(x => x.Credit).HasColumnType("numeric(18,4)").IsRequired();

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
    }
}
