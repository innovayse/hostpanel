using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.SDK.Plugins.Enums;
using Innovayse.SDK.Plugins.Fulfilment;
using Innovayse.SDK.Plugins.Identity;

namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>
/// Turns an invoice, with its lines and transactions loaded, into what a fulfilment plugin is handed for
/// each thing that can happen to it: paid, refunded or lapsed. Pure: nothing is read from the database
/// here, so the same invoice always yields the same context.
/// </summary>
/// <remarks>
/// <para>
/// A line is handed over when it carries a product snapshot, which is how the product looked when it
/// was billed; a line without one (a domain renewal, a billable item, an adjustment) is not a product's
/// charge and is left out. The SKU and metadata come from the snapshot, never from the catalogue, which
/// may have changed since. A plugin ignores the lines it does not fulfil.
/// </para>
/// <para>
/// Every amount is in the invoice's own currency, and each line says so.
/// </para>
/// </remarks>
public static class FulfilmentContextBuilder
{
    /// <summary>Builds the context of <see cref="IFulfilmentPlugin.OnInvoicePaidAsync"/>.</summary>
    /// <param name="invoice">The paid invoice, with its items loaded.</param>
    /// <param name="account">The client's external account, or <see langword="null"/> when the client is not linked to one.</param>
    /// <returns>The paid context; the transaction reference is the payment's, or <see langword="null"/> for a manual payment.</returns>
    /// <exception cref="InvalidOperationException">The invoice has no payment date.</exception>
    public static InvoicePaidContext Paid(Invoice invoice, ExternalAccount? account)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        var paidAt = invoice.PaidAt
            ?? throw new InvalidOperationException($"Invoice {invoice.Id} has no payment date to tell a plugin about.");

        return new InvoicePaidContext(
            invoice.Id, invoice.ClientId, account, Lines(invoice), invoice.GatewayTransactionId, paidAt);
    }

    /// <summary>Builds the context of <see cref="IFulfilmentPlugin.OnInvoiceRefundedAsync"/>.</summary>
    /// <param name="invoice">The refunded invoice, with its items and transactions loaded.</param>
    /// <param name="account">The client's external account, or <see langword="null"/>.</param>
    /// <returns>
    /// The refunded context: everything refunded so far, and the reference and date of the latest refund.
    /// </returns>
    /// <exception cref="InvalidOperationException">The invoice has no refund recorded.</exception>
    public static InvoiceRefundedContext Refunded(Invoice invoice, ExternalAccount? account)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        var latest = invoice.Transactions
            .Where(t => t.Type == InvoiceTransactionType.Refund)
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.Id)
            .FirstOrDefault()
            ?? throw new InvalidOperationException($"Invoice {invoice.Id} has no refund to tell a plugin about.");

        return new InvoiceRefundedContext(
            invoice.Id,
            invoice.ClientId,
            account,
            Lines(invoice),
            invoice.AmountRefunded,
            invoice.Currency,
            latest.TransactionId,
            latest.Date);
    }

    /// <summary>Builds the context of <see cref="IFulfilmentPlugin.OnSubscriptionLapsedAsync"/>.</summary>
    /// <param name="invoice">The lapsed invoice, with its items loaded.</param>
    /// <param name="account">The client's external account, or <see langword="null"/>.</param>
    /// <returns>The lapsed context.</returns>
    /// <exception cref="InvalidOperationException">The invoice has not lapsed.</exception>
    public static SubscriptionLapsedContext Lapsed(Invoice invoice, ExternalAccount? account)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        var lapsedAt = invoice.LapsedAt
            ?? throw new InvalidOperationException($"Invoice {invoice.Id} has not lapsed.");

        return new SubscriptionLapsedContext(
            invoice.Id, invoice.ClientId, account, Lines(invoice), invoice.DueDate, lapsedAt);
    }

    /// <summary>The product lines of an invoice, as a plugin sees them.</summary>
    /// <param name="invoice">The invoice, with its items loaded.</param>
    /// <returns>One entry per line that carries a product snapshot, in line order.</returns>
    public static IReadOnlyList<FulfilmentLine> Lines(Invoice invoice)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        return invoice.Items
            .OrderBy(item => item.Id)
            .Where(item => item.ProductSnapshot is not null)
            .Select(item => new FulfilmentLine(
                item.ProductSnapshot!.ProductId,
                item.ProductSnapshot.Sku,
                item.ProductSnapshot.Metadata,
                item.Quantity,
                item.Amount,
                invoice.Currency,
                PluginCycleOf(item.ProductSnapshot.BillingCycle)))
            .ToList();
    }

    /// <summary>Maps the host's billing cycle to the cycle the plugin contract names.</summary>
    /// <param name="cycle">The cycle the product was sold on.</param>
    /// <returns>The plugin contract's cycle.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="cycle"/> is not a defined cycle.</exception>
    private static PluginBillingCycle PluginCycleOf(BillingCycle cycle)
    {
        // An explicit arm per cycle, no cast: the two enums number their members differently, and a cycle
        // added to the host must be mapped here on purpose, not leak through as a number.
        return cycle switch
        {
            BillingCycle.Monthly => PluginBillingCycle.Monthly,
            BillingCycle.Annual => PluginBillingCycle.Annual,
            BillingCycle.Quarterly => PluginBillingCycle.Quarterly,
            BillingCycle.SemiAnnual => PluginBillingCycle.SemiAnnual,
            BillingCycle.Biennial => PluginBillingCycle.Biennial,
            _ => throw new ArgumentOutOfRangeException(nameof(cycle), cycle, "The billing cycle has no plugin counterpart."),
        };
    }
}
