using Innovayse.Hostpanel.Application.Billing.Commands.CancelInvoice;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Queries.GetInvoiceBrief;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Orders.Extensions;

/// <summary>What the Orders module asks Billing about the invoice of an order, shaped once for every use case that needs it.</summary>
public static class OrderInvoiceBusExtensions
{
    /// <summary>
    /// Cancels the order's invoice unless it has nothing left to collect. A missing, cancelled or refunded invoice is left as it is;
    /// any other is cancelled through Billing, which refuses one that already took money.
    /// </summary>
    /// <param name="bus">The message bus.</param>
    /// <param name="invoiceId">The order's invoice.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success, or whichever code <see cref="CancelInvoiceCommand"/> refuses the invoice with.</returns>
    public static async Task<Result> CancelOpenInvoiceAsync(this IMessageBus bus, int invoiceId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(bus);

        var invoice = await bus.InvokeAsync<Result<InvoiceBriefDto>>(new GetInvoiceBriefQuery(invoiceId), ct);
        if (invoice.IsFailure || invoice.Value!.Status is InvoiceStatus.Cancelled or InvoiceStatus.Refunded)
        {
            return Result.Success();
        }

        return await bus.InvokeAsync<Result>(new CancelInvoiceCommand(invoiceId), ct);
    }

    /// <summary>Tells whether the order's invoice has been paid.</summary>
    /// <param name="bus">The message bus.</param>
    /// <param name="invoiceId">The order's invoice.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> when Billing reports the invoice as paid.</returns>
    public static async Task<bool> IsInvoicePaidAsync(this IMessageBus bus, int invoiceId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(bus);

        var invoice = await bus.InvokeAsync<Result<InvoiceBriefDto>>(new GetInvoiceBriefQuery(invoiceId), ct);
        return invoice.IsSuccess && invoice.Value!.Status is InvoiceStatus.Paid;
    }
}
