using Innovayse.Hostpanel.Application.Billing.Commands.StartGatewayPayment;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Billing.Commands.StartMyGatewayPayment;

/// <summary>
/// Starts a hosted-gateway payment on one of the calling client's own invoices, refusing every
/// invoice that is not theirs.
/// </summary>
/// <remarks>
/// The check lives here rather than at the endpoint, so it travels with the message: nothing can
/// open a payment session through <see cref="StartMyGatewayPaymentCommand"/> without it having
/// run. Once ownership is settled the work is the same the shared command does, so this
/// dispatches <see cref="StartGatewayPaymentCommand"/>, including its return-URL checks, which
/// stay in one place.
/// </remarks>
public sealed class StartMyGatewayPaymentHandler
{
    /// <summary>The rule that says a client may only pay their own invoices.</summary>
    private readonly IInvoiceOwnership _ownership;

    /// <summary>Wolverine bus, used to reach the shared command once ownership is settled.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Initializes a new instance of <see cref="StartMyGatewayPaymentHandler"/>.</summary>
    /// <param name="ownership">The rule that says a client may only pay their own invoices.</param>
    /// <param name="bus">Wolverine bus, used to reach the shared command once ownership is settled.</param>
    public StartMyGatewayPaymentHandler(IInvoiceOwnership ownership, IMessageBus bus)
    {
        _ownership = ownership;
        _bus = bus;
    }

    /// <summary>Handles <see cref="StartMyGatewayPaymentCommand"/>.</summary>
    /// <param name="cmd">The command. It names no account: this pays the caller's own invoice.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The absolute URL to redirect the payer to, or <c>INVOICE_NOT_FOUND</c> (404) when the
    /// invoice is not the caller's, when no such invoice exists, or when the caller has no client
    /// record.
    /// </returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when the request carries no subject.</exception>
    public async Task<Result<RedirectDto>> HandleAsync(StartMyGatewayPaymentCommand cmd, CancellationToken ct)
    {
        var ownershipOutcome = await _ownership.RequireOwnedByCallerAsync(cmd.InvoiceId, ct);
        if (ownershipOutcome.IsFailure)
        {
            return Result<RedirectDto>.FailureCode(ownershipOutcome.ErrorCode!);
        }

        // The staff use case already answers with a code; its refusal passes through unchanged, so
        // a payer on the client route sees the same code an administrator would.
        return await _bus.InvokeAsync<Result<RedirectDto>>(
            new StartGatewayPaymentCommand(cmd.InvoiceId, cmd.Module, cmd.ReturnUrl), ct);
    }
}
