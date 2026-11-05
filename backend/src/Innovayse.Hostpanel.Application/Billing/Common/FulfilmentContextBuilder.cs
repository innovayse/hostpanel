using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Fulfilment;
using Hostpanel.Sdk.Plugins.Identity;

namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>
/// Turns an invoice, with its lines and transactions loaded, into what a fulfilment plugin is handed for
/// each thing that can happen to it: paid, refunded, lapsed, or a refund it is asked to advise on. Pure:
/// nothing is read from the database here, so the same invoice and reference always yield the same context.
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
/// <para>
/// The client reference is computed by the caller for the plugin that will receive the context
/// (<c>IPluginReferences.ForClient</c>), never here: a reference differs per plugin, and the builder does not
/// know which plugin it builds for. It is the same value an endpoint call of that plugin for that client
/// carries, so a plugin can join what it sold with what its endpoints see.
/// </para>
/// </remarks>
public static class FulfilmentContextBuilder
{
    /// <summary>Builds the context of <see cref="IFulfilmentPlugin.OnInvoicePaidAsync"/>.</summary>
    /// <param name="invoice">The paid invoice, with its items loaded.</param>
    /// <param name="clientRef">The receiving plugin's own reference to the invoice's client.</param>
    /// <param name="account">The client's external account, or <see langword="null"/> when the client is not linked to one.</param>
    /// <returns>The paid context; the transaction reference is the payment's, or <see langword="null"/> for a manual payment.</returns>
    /// <exception cref="InvalidOperationException">The invoice has no payment date.</exception>
    public static InvoicePaidContext Paid(Invoice invoice, string clientRef, ExternalAccount? account)
    {
        var paidAt = invoice.PaidAt
            ?? throw new InvalidOperationException($"Invoice {invoice.Id} has no payment date to tell a plugin about.");

        return new InvoicePaidContext(
            invoice.Id, invoice.ClientId, clientRef, account, Lines(invoice), invoice.GatewayTransactionId, paidAt);
    }

    /// <summary>Builds the context of <see cref="IFulfilmentPlugin.OnInvoiceRefundedAsync"/>.</summary>
    /// <param name="invoice">The refunded invoice, with its items and transactions loaded.</param>
    /// <param name="clientRef">The receiving plugin's own reference to the invoice's client.</param>
    /// <param name="account">The client's external account, or <see langword="null"/>.</param>
    /// <returns>
    /// The refunded context: everything refunded so far, and the reference and date of the latest refund.
    /// </returns>
    /// <exception cref="InvalidOperationException">The invoice has no refund recorded.</exception>
    public static InvoiceRefundedContext Refunded(Invoice invoice, string clientRef, ExternalAccount? account)
    {
        var latest = invoice.Transactions
            .Where(t => t.Type == InvoiceTransactionType.Refund)
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.Id)
            .FirstOrDefault()
            ?? throw new InvalidOperationException($"Invoice {invoice.Id} has no refund to tell a plugin about.");

        return new InvoiceRefundedContext(
            invoice.Id,
            invoice.ClientId,
            clientRef,
            account,
            Lines(invoice),
            invoice.AmountRefunded,
            invoice.Currency,
            latest.TransactionId,
            latest.Date);
    }

    /// <summary>Builds the context of <see cref="IFulfilmentPlugin.OnSubscriptionLapsedAsync"/>.</summary>
    /// <param name="invoice">The lapsed invoice, with its items loaded.</param>
    /// <param name="clientRef">The receiving plugin's own reference to the invoice's client.</param>
    /// <param name="account">The client's external account, or <see langword="null"/>.</param>
    /// <returns>The lapsed context.</returns>
    /// <exception cref="InvalidOperationException">The invoice has not lapsed.</exception>
    public static SubscriptionLapsedContext Lapsed(Invoice invoice, string clientRef, ExternalAccount? account)
    {
        var lapsedAt = invoice.LapsedAt
            ?? throw new InvalidOperationException($"Invoice {invoice.Id} has not lapsed.");

        return new SubscriptionLapsedContext(
            invoice.Id, invoice.ClientId, clientRef, account, Lines(invoice), invoice.DueDate, lapsedAt);
    }

    /// <summary>Builds the context of <see cref="IFulfilmentPlugin.AdviseRefundAsync"/>.</summary>
    /// <param name="invoice">The paid invoice an admin wants to refund, with its items loaded.</param>
    /// <param name="clientRef">The asked plugin's own reference to the invoice's client.</param>
    /// <param name="account">The client's external account, or <see langword="null"/>.</param>
    /// <param name="requested">The amount asked for, in the invoice currency.</param>
    /// <returns>The refund advice context, in the invoice currency.</returns>
    public static RefundAdviceContext RefundAdvice(
        Invoice invoice, string clientRef, ExternalAccount? account, decimal requested)
    {
        return new RefundAdviceContext(
            invoice.Id, invoice.ClientId, clientRef, account, Lines(invoice), requested, invoice.Currency);
    }

    /// <summary>The product lines of an invoice, as a plugin sees them.</summary>
    /// <param name="invoice">The invoice, with its items loaded.</param>
    /// <returns>One entry per line that carries a product snapshot, in line order.</returns>
    public static IReadOnlyList<FulfilmentLine> Lines(Invoice invoice)
    {
        List<FulfilmentLine> lines = [];
        foreach (var item in invoice.Items.OrderBy(item => item.Id))
        {
            if (item.ProductSnapshot is not { } snapshot)
            {
                continue;
            }

            lines.Add(new FulfilmentLine(
                snapshot.ProductId,
                snapshot.Sku,
                snapshot.Metadata,
                item.Quantity,
                item.Amount,
                invoice.Currency,
                PluginCycleOf(snapshot.BillingCycle)));
        }

        return lines;
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
