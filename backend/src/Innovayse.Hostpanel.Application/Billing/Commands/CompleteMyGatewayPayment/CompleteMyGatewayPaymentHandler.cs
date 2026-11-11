using Innovayse.Hostpanel.Application.Billing.Commands.CompleteGatewayPayment;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Billing.Commands.CompleteMyGatewayPayment;

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
public sealed class CompleteMyGatewayPaymentHandler
{
    /// <summary>The rule that says a client may only settle their own invoices.</summary>
    private readonly IInvoiceOwnership _ownership;

    /// <summary>Wolverine bus, used to reach the shared command once ownership is settled.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Initializes a new instance of <see cref="CompleteMyGatewayPaymentHandler"/>.</summary>
    /// <param name="ownership">The rule that says a client may only settle their own invoices.</param>
    /// <param name="bus">Wolverine bus, used to reach the shared command once ownership is settled.</param>
    public CompleteMyGatewayPaymentHandler(IInvoiceOwnership ownership, IMessageBus bus)
    {
        _ownership = ownership;
        _bus = bus;
    }

    /// <summary>Handles <see cref="CompleteMyGatewayPaymentCommand"/>.</summary>
    /// <param name="cmd">The command. It names no account: this settles the caller's own invoice.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The state the gateway reported for the session, or <c>INVOICE_NOT_FOUND</c> (404) when the
    /// invoice is not the caller's, when no such invoice exists, or when the caller has no client
    /// record.
    /// </returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when the request carries no subject.</exception>
    public async Task<Result<GatewayCompletionDto>> HandleAsync(
        CompleteMyGatewayPaymentCommand cmd, CancellationToken ct)
    {
        var ownershipOutcome = await _ownership.RequireOwnedByCallerAsync(cmd.InvoiceId, ct);
        if (ownershipOutcome.IsFailure)
        {
            return Result<GatewayCompletionDto>.FailureCode(ownershipOutcome.ErrorCode!);
        }

        // The staff use case already answers with a code; its refusal passes through unchanged.
        return await _bus.InvokeAsync<Result<GatewayCompletionDto>>(
            new CompleteGatewayPaymentCommand(cmd.InvoiceId), ct);
    }
}
