using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// Builds the invoices the fulfilment tests tell plugins about: one digital line and one line that is not
/// a product charge, in a currency other than the base one, so a test can tell the invoice currency
/// from the host one. An invoice that went through everything (it lapsed, was paid, then partly refunded) is
/// a valid subject for all three kinds of record.
/// </summary>
internal static class FulfilmentInvoiceSeeds
{
    /// <summary>The currency of every invoice built here; not the host base currency.</summary>
    internal const string Currency = "EUR";

    /// <summary>The reference the invoice was paid under.</summary>
    internal const string PaymentReference = "pay-ref-1";

    /// <summary>The reference the refund was recorded under.</summary>
    internal const string RefundReference = "refund-ref-1";

    /// <summary>The amount the refund returned.</summary>
    internal const decimal RefundedAmount = 4m;

    /// <summary>The SKU of the digital line.</summary>
    internal const string Sku = "licence-pro";

    /// <summary>The metadata of the digital line.</summary>
    internal const string Metadata = "{\"seats\":5}";

    /// <summary>Builds an unsaved invoice that lapsed, was then paid and partly refunded.</summary>
    /// <param name="clientId">The client the invoice belongs to.</param>
    /// <param name="now">The moment everything happened at.</param>
    /// <returns>The unsaved invoice.</returns>
    internal static Invoice Build(int clientId, DateTimeOffset now)
    {
        var invoice = Invoice.Create(clientId, now.AddDays(-1), Currency, now.AddDays(-30));
        invoice.AddItem("Licence", 10m, 2, null, new ProductSnapshot(42, BillingCycle.Annual, true, Sku, Metadata));
        invoice.AddItem("Setup fee", 5m, 1);
        invoice.MarkOverdue(now);
        invoice.MarkLapsed(now);
        invoice.Settle("manual", PaymentReference, now, TransactionSource.Manual);
        invoice.AddRefund(now, "manual", RefundReference, RefundedAmount, 0m, null, false, TransactionSource.Manual);
        return invoice;
    }

    /// <summary>Saves a fresh client and an invoice built by <see cref="Build"/> for them.</summary>
    /// <param name="db">The context to save through.</param>
    /// <param name="now">The moment everything on the invoice happened at.</param>
    /// <returns>The saved invoice.</returns>
    internal static async Task<Invoice> SeedAsync(AppDbContext db, DateTimeOffset now)
    {
        var client = Client.Create(
            $"subject-{Guid.NewGuid():N}", "Ada", "Lovelace", $"{Guid.NewGuid():N}@example.test", DateTimeOffset.UtcNow);
        db.Clients.Add(client);
        await db.SaveChangesAsync(CancellationToken.None);

        var invoice = Build(client.Id, now);
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync(CancellationToken.None);
        return invoice;
    }
}
