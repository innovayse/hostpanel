namespace Innovayse.Application.Billing.Commands.PayMyInvoice;

using Innovayse.Application.Billing.Commands.PayInvoice;
using Innovayse.Application.Billing.Common;
using Wolverine;

/// <summary>
/// Pays one of the calling client's own invoices, refusing every invoice that is not theirs.
/// </summary>
/// <remarks>
/// The check lives here rather than at the endpoint, so it travels with the message: nothing can
/// charge a card through <see cref="PayMyInvoiceCommand"/> without it having run. Once ownership
/// is settled the write is the same one the admin route performs, so this dispatches
/// <see cref="PayInvoiceCommand"/> rather than duplicating it.
/// </remarks>
/// <param name="ownership">The rule that says a client may only pay their own invoices.</param>
/// <param name="bus">Wolverine bus, used to reach the shared write once ownership is settled.</param>
public sealed class PayMyInvoiceHandler(IInvoiceOwnership ownership, IMessageBus bus)
{
    /// <summary>Handles <see cref="PayMyInvoiceCommand"/>.</summary>
    /// <param name="cmd">The command. It names no account: this pays the caller's own invoice.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once the payment has been taken.</returns>
    /// <exception cref="InvoiceNotFoundException">
    /// Thrown when the invoice is not the caller's, when no such invoice exists, and when the
    /// caller has no client record -- all with the same wording and status.
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when the request carries no subject.</exception>
    public async Task HandleAsync(PayMyInvoiceCommand cmd, CancellationToken ct)
    {
        await ownership.RequireOwnedByCallerAsync(cmd.InvoiceId, ct);
        await bus.InvokeAsync(new PayInvoiceCommand(cmd.InvoiceId, cmd.Currency), ct);
    }
}
