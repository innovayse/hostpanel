using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Extensions;
using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Billing.Commands.SetDefaultPaymentMethod;

/// <summary>
/// Handles <see cref="SetDefaultPaymentMethodCommand"/>.
/// </summary>
public sealed class SetDefaultPaymentMethodHandler
{
    /// <summary>The Wolverine bus, for the Clients module's card gateway customer query.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Card payment gateway, for updating the customer's default payment method.</summary>
    private readonly ICardPaymentGateway _cardGateway;

    /// <summary>Initializes the handler.</summary>
    /// <param name="bus">The Wolverine bus, for the Clients module's card gateway customer query.</param>
    /// <param name="cardGateway">Card payment gateway, for updating the customer's default payment method.</param>
    public SetDefaultPaymentMethodHandler(IMessageBus bus, ICardPaymentGateway cardGateway)
    {
        _bus = bus;
        _cardGateway = cardGateway;
    }

    /// <summary>
    /// Makes the given payment method the client's default.
    /// </summary>
    /// <param name="cmd">The command. It names no account: this acts on the caller's own.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success, or <c>CLIENT_PROFILE_NOT_FOUND</c> when the caller has no client record,
    /// <c>NO_SAVED_PAYMENT_METHODS</c> when the client has never saved one, or
    /// <c>PAYMENT_METHOD_NOT_OWNED</c> when the id does not belong to this client, or
    /// <c>PAYMENT_METHOD_UNAVAILABLE</c> when the card gateway has no keys, or
    /// <c>PAYMENT_GATEWAY_SERVICE_UNAVAILABLE</c> / <c>PAYMENT_METHOD_UPSTREAM_FAILED</c> when the card
    /// gateway could not be reached or refused the call.
    /// </returns>
    public async Task<Result> HandleAsync(SetDefaultPaymentMethodCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var customer = await _bus.GetCallerCardGatewayCustomerAsync(ct);
        if (customer.IsFailure)
        {
            return Result.FailureCode(customer.ErrorCode!);
        }

        var customerId = customer.Value!.CustomerId;
        if (customerId is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.NO_SAVED_PAYMENT_METHODS);
        }

        // Without the gateway's keys there is no card to change, and the gateway would throw on its
        // empty key rather than answer (#606).
        var configured = _cardGateway.RequireConfigured();
        if (configured.IsFailure)
        {
            return configured;
        }

        // The card gateway's own API would reject a payment method that isn't attached to this customer,
        // but checking here first means a mismatched ID gets this handler's own error rather
        // than an opaque gateway refusal, and it keeps the same check in place if that
        // gateway-side validation ever turns out to be less strict than assumed.
        var methods = await _cardGateway.ListPaymentMethodsAsync(new CardCustomerInstruction(customerId), ct);
        if (methods.Failure is { } listFailure)
        {
            return Result.FailureCode(listFailure.ToPaymentMethodErrorCode());
        }

        if (!methods.Methods.Any(m => m.Id == cmd.PaymentMethodId))
        {
            return Result.FailureCode(HostpanelErrorCodes.PAYMENT_METHOD_NOT_OWNED);
        }

        var updated = await _cardGateway.SetDefaultPaymentMethodAsync(
            new CardDefaultMethodInstruction(customerId, cmd.PaymentMethodId!), ct);
        if (updated.Failure is { } updateFailure)
        {
            return Result.FailureCode(updateFailure.ToPaymentMethodErrorCode());
        }

        return Result.Success();
    }
}
