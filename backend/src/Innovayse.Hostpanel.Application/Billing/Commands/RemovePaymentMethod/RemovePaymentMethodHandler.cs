using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Extensions;
using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Billing.Commands.RemovePaymentMethod;

/// <summary>
/// Handles <see cref="RemovePaymentMethodCommand"/>.
/// </summary>
public sealed class RemovePaymentMethodHandler
{
    /// <summary>The Wolverine bus, for the Clients module's card gateway customer query.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Card payment gateway, for detaching the payment method.</summary>
    private readonly ICardPaymentGateway _cardGateway;

    /// <summary>The module's data context, for withdrawing the agreement the removed card was charged under.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The clock the withdrawal's moment is read from.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="bus">The Wolverine bus, for the Clients module's card gateway customer query.</param>
    /// <param name="cardGateway">Card payment gateway, for detaching the payment method.</param>
    /// <param name="context">The module's data context, for withdrawing the agreement the removed card was charged under.</param>
    /// <param name="clock">The clock the withdrawal's moment is read from.</param>
    public RemovePaymentMethodHandler(IMessageBus bus, ICardPaymentGateway cardGateway, IHostpanelDbContext context, IClock clock)
    {
        _bus = bus;
        _cardGateway = cardGateway;
        _context = context;
        _clock = clock;
    }

    /// <summary>
    /// Detaches the given payment method from the client's card gateway customer.
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
    public async Task<Result> HandleAsync(RemovePaymentMethodCommand cmd, CancellationToken ct)
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

        // Detach takes a bare payment method ID with no customer scoping, so without this
        // check a client could remove a payment method that belongs to somebody else's
        // card gateway customer just by knowing (or guessing) its ID.
        var methods = await _cardGateway.ListPaymentMethodsAsync(new CardCustomerInstruction(customerId), ct);
        if (methods.Failure is { } listFailure)
        {
            return Result.FailureCode(listFailure.ToPaymentMethodErrorCode());
        }

        if (!methods.Methods.Any(m => m.Id == cmd.PaymentMethodId))
        {
            return Result.FailureCode(HostpanelErrorCodes.PAYMENT_METHOD_NOT_OWNED);
        }

        var detached = await _cardGateway.DetachPaymentMethodAsync(new CardDetachInstruction(cmd.PaymentMethodId), ct);
        if (detached.Failure is { } detachFailure)
        {
            return Result.FailureCode(detachFailure.ToPaymentMethodErrorCode());
        }

        // A card that is gone is never charged: the agreement made for it ends with it.
        var agreements = await _context.CardChargeConsents
            .Where(c => c.ClientId == customer.Value!.ClientId && c.GatewayPaymentMethodId == cmd.PaymentMethodId && c.Status != CardConsentStatus.Revoked)
            .ToListAsync(ct);
        foreach (var agreement in agreements)
        {
            agreement.Revoke("The card was removed", _clock.UtcNow);
        }

        if (agreements.Count > 0)
        {
            await _context.SaveChangesAsync(ct);
        }

        return Result.Success();
    }
}
