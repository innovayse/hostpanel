using Innovayse.Hostpanel.Application.Plugins.Common;
using Hostpanel.Sdk.Plugins.Host;

namespace Innovayse.Hostpanel.Infrastructure.Plugins.Extensions;

/// <summary>Maps the host's <see cref="PluginHostInvoiceDto"/> to the SDK's <see cref="PluginClientInvoice"/>.</summary>
public static class PluginHostInvoiceDtoExtensions
{
    /// <summary>Maps one client-visible invoice row to the record a plugin sees, with the status translated by name.</summary>
    /// <param name="invoice">The invoice columns the host shares with a plugin; never a draft.</param>
    /// <returns>The SDK's invoice record carrying the same values.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the invoice's status is a draft or has no plugin equivalent.
    /// </exception>
    public static PluginClientInvoice ToPlugin(this PluginHostInvoiceDto invoice)
    {
        return new PluginClientInvoice(
            invoice.Id,
            invoice.Status.ToPlugin(),
            invoice.Total,
            invoice.Currency,
            invoice.InvoiceDate,
            invoice.DueDate,
            invoice.PaidAt);
    }
}
