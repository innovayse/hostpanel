using Innovayse.Hostpanel.Domain.Billing.Enums;
using Hostpanel.Sdk.Plugins.Enums;

namespace Innovayse.Hostpanel.Infrastructure.Plugins.Extensions;

/// <summary>
/// Maps the host's <see cref="InvoiceStatus"/> to the SDK's <see cref="PluginInvoiceStatus"/>, by name. The two sets have
/// different numbers (the SDK starts at 1, the host at 0), so a cast would silently report the wrong state; every shared
/// value is listed and a value this mapping does not know fails loudly instead of being passed on.
/// </summary>
public static class InvoiceStatusExtensions
{
    /// <summary>
    /// Maps a client-visible invoice status to the status a plugin sees. <see cref="InvoiceStatus.Draft"/> has no arm: the
    /// host reads invoices for a plugin through <c>Invoice.ClientVisibleFilter</c>, so a draft never reaches this mapping,
    /// and one that did would be a leak the throwing default arm refuses rather than passes on.
    /// </summary>
    /// <param name="status">The host's invoice status; never a draft.</param>
    /// <returns>The SDK's status of the same name.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="status"/> is <see cref="InvoiceStatus.Draft"/> or not a known invoice status.
    /// </exception>
    public static PluginInvoiceStatus ToPlugin(this InvoiceStatus status)
    {
        return status switch
        {
            InvoiceStatus.Unpaid => PluginInvoiceStatus.Unpaid,
            InvoiceStatus.Paid => PluginInvoiceStatus.Paid,
            InvoiceStatus.Overdue => PluginInvoiceStatus.Overdue,
            InvoiceStatus.Cancelled => PluginInvoiceStatus.Cancelled,
            InvoiceStatus.Refunded => PluginInvoiceStatus.Refunded,
            InvoiceStatus.Collections => PluginInvoiceStatus.Collections,
            InvoiceStatus.PaymentPending => PluginInvoiceStatus.PaymentPending,
            InvoiceStatus.PartiallyRefunded => PluginInvoiceStatus.PartiallyRefunded,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "The invoice status has no plugin equivalent."),
        };
    }
}
