namespace Innovayse.Application.Billing.Commands.CompleteMyGatewayPayment;

using Innovayse.Application.Billing.Commands.CompleteGatewayPayment;
using Innovayse.Application.Billing.Common;
using Wolverine;

/// <summary>
/// Verifies a hosted-gateway payment on one of the calling client's own invoices, refusing every
/// invoice that is not theirs.
/// </summary>
/// <remarks>
/// The check lives here rather than at the endpoint, so it travels with the message: nothing can
/// probe a payment state through <see cref="CompleteMyGatewayPaymentCommand"/> without it having
/// run. Once ownership is settled the work is the same the shared command does, so this
/// dispatches <see cref="CompleteGatewayPaymentCommand"/> and keeps its idempotency in one place.
/// </remarks>
/// <param name="ownership">The rule that says a client may only settle their own invoices.</param>
/// <param name="bus">Wolverine bus, used to reach the shared command once ownership is settled.</param>
public sealed class CompleteMyGatewayPaymentHandler(IInvoiceOwnership ownership, IMessageBus bus)
{
    /// <summary>Handles <see cref="CompleteMyGatewayPaymentCommand"/>.</summary>
    /// <param name="cmd">The command. It names no account: this settles the caller's own invoice.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The state the gateway reported for the session.</returns>
    /// <exception cref="InvoiceNotFoundException">
    /// Thrown when the invoice is not the caller's, when no such invoice exists, and when the
    /// caller has no client record -- all with the same wording and status.
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when the request carries no subject.</exception>
    public async Task<GatewayCompletionState> HandleAsync(
        CompleteMyGatewayPaymentCommand cmd, CancellationToken ct)
    {
        await ownership.RequireOwnedByCallerAsync(cmd.InvoiceId, ct);

        return await bus.InvokeAsync<GatewayCompletionState>(
            new CompleteGatewayPaymentCommand(cmd.InvoiceId), ct);
    }
}
