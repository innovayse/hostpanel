namespace Innovayse.Application.Billing.Commands.StartMyGatewayPayment;

using Innovayse.Application.Billing.Commands.StartGatewayPayment;
using Innovayse.Application.Billing.Common;
using Wolverine;

/// <summary>
/// Starts a hosted-gateway payment on one of the calling client's own invoices, refusing every
/// invoice that is not theirs.
/// </summary>
/// <remarks>
/// The check lives here rather than at the endpoint, so it travels with the message: nothing can
/// open a payment session through <see cref="StartMyGatewayPaymentCommand"/> without it having
/// run. Once ownership is settled the work is the same the shared command does, so this
/// dispatches <see cref="StartGatewayPaymentCommand"/> -- including its return-URL checks, which
/// stay in one place.
/// </remarks>
/// <param name="ownership">The rule that says a client may only pay their own invoices.</param>
/// <param name="bus">Wolverine bus, used to reach the shared command once ownership is settled.</param>
public sealed class StartMyGatewayPaymentHandler(IInvoiceOwnership ownership, IMessageBus bus)
{
    /// <summary>Handles <see cref="StartMyGatewayPaymentCommand"/>.</summary>
    /// <param name="cmd">The command. It names no account: this pays the caller's own invoice.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The absolute URL to redirect the payer to.</returns>
    /// <exception cref="InvoiceNotFoundException">
    /// Thrown when the invoice is not the caller's, when no such invoice exists, and when the
    /// caller has no client record -- all with the same wording and status.
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when the request carries no subject.</exception>
    public async Task<string> HandleAsync(StartMyGatewayPaymentCommand cmd, CancellationToken ct)
    {
        await ownership.RequireOwnedByCallerAsync(cmd.InvoiceId, ct);

        return await bus.InvokeAsync<string>(
            new StartGatewayPaymentCommand(cmd.InvoiceId, cmd.Module, cmd.ReturnUrl), ct);
    }
}
