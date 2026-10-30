using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Innovayse.Hostpanel.Infrastructure.Persistence.Configurations;

/// <summary>EF Core configuration for the <see cref="InvoiceItem"/> entity.</summary>
public sealed class InvoiceItemConfiguration : IEntityTypeConfiguration<InvoiceItem>
{
    /// <summary>Column width for a <see cref="BillingCycle"/> name stored as text, as on the service it renews.</summary>
    private const int BillingCycleLength = 20;

    /// <summary>Configures the <c>InvoiceItems</c> table mapping.</summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<InvoiceItem> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.InvoiceId).IsRequired();

        // Nullable, unindexed-by-default no longer: "the invoices for this service" filters on
        // it, and without an index that read is a scan of every line ever billed. No FK
        // constraint is declared: a terminated service's row may be removed while the invoice
        // that charged for it must survive as a financial record, and a constraint would either
        // block that or cascade the invoice line away with it.
        builder.Property(x => x.ClientServiceId);
        builder.HasIndex(x => x.ClientServiceId);

        // Same reasoning as ClientServiceId: an index for "this domain's renewals", no FK, because
        // the invoice must survive the domain row.
        builder.Property(x => x.RegisteredDomainId);
        builder.Property(x => x.RenewalPeriodStart);
        builder.Property(x => x.RenewalPeriodEnd);
        builder.Property(x => x.RenewalAppliedAt).IsConcurrencyToken();

        // The claim taken before a registrar call. Both renewal markers are concurrency tokens: the
        // UPDATE that saves a claim or an apply carries their read values in its WHERE, so of two
        // runners only one save wins and the other gets DbUpdateConcurrencyException. Tokens are
        // not schema, so no column changes.
        builder.Property(x => x.RenewalAttemptAt).IsConcurrencyToken();

        // The duplicate guard asks "is this period already invoiced" per target.
        builder.HasIndex(x => new { x.ClientServiceId, x.RenewalPeriodStart });
        builder.HasIndex(x => new { x.RegisteredDomainId, x.RenewalPeriodStart });

        // The daily sweep reads only paid-but-unapplied renewal lines; a partial index keeps that
        // read off every line ever billed.
        builder.HasIndex(x => x.RenewalAppliedAt)
            .HasDatabaseName("IX_InvoiceItems_PendingRenewal")
            .HasFilter("\"RenewalAppliedAt\" IS NULL AND \"RenewalPeriodStart\" IS NOT NULL");

        builder.Ignore(x => x.IsRenewal);
        builder.Ignore(x => x.IsDigital);

        // What the product was when the line was billed, stored in the line's own row. Optional: a line that
        // is not a product's charge, and every line written before snapshots were kept, has none, and all
        // five columns are null. ProductId is never null on a snapshot, which is how EF tells the two apart.
        // The column names are given here because EF would otherwise prefix them with the navigation's name.
        builder.OwnsOne(x => x.ProductSnapshot, snapshot =>
        {
            snapshot.Property(x => x.ProductId).HasColumnName("ProductId");
            snapshot.Property(x => x.BillingCycle)
                .HasColumnName("BillingCycle")
                .HasConversion<string>()
                .HasMaxLength(BillingCycleLength);
            snapshot.Property(x => x.IsDigital).HasColumnName("IsDigital");
            snapshot.Property(x => x.Sku).HasColumnName("Sku").HasMaxLength(ProductSku.MaxLength);
            snapshot.Property(x => x.Metadata).HasColumnName("ProductMetadata").HasColumnType("jsonb");
        });
        builder.Navigation(x => x.ProductSnapshot).IsRequired(false);

        builder.Property(x => x.Description).HasMaxLength(500).IsRequired();
        builder.Property(x => x.UnitPrice).HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(x => x.Quantity).IsRequired();
        builder.Property(x => x.Amount).HasColumnType("numeric(18,4)").IsRequired();
    }
}
