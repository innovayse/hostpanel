using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.SDK.Plugins.Enums;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>
/// Maps the host's status enums to the SDK's, by name. The two sets have different numbers (the SDK starts at 1, the
/// host at 0), so a cast would silently report the wrong state; every value is listed and a value this mapper does not
/// know fails loudly instead of being passed on.
/// </summary>
internal static class PluginFactMapper
{
    /// <summary>Maps a service status to the status a plugin sees.</summary>
    /// <param name="status">The host's service status.</param>
    /// <returns>The SDK's status of the same name.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="status"/> is not a known service status.</exception>
    public static PluginServiceStatus ToPlugin(ServiceStatus status)
    {
        return status switch
        {
            ServiceStatus.Pending => PluginServiceStatus.Pending,
            ServiceStatus.Active => PluginServiceStatus.Active,
            ServiceStatus.Suspended => PluginServiceStatus.Suspended,
            ServiceStatus.Terminated => PluginServiceStatus.Terminated,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "The service status has no plugin equivalent."),
        };
    }

    /// <summary>Maps an invoice status to the status a plugin sees.</summary>
    /// <param name="status">The host's invoice status.</param>
    /// <returns>The SDK's status of the same name.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="status"/> is not a known invoice status.</exception>
    public static PluginInvoiceStatus ToPlugin(InvoiceStatus status)
    {
        return status switch
        {
            InvoiceStatus.Draft => PluginInvoiceStatus.Draft,
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
