using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Extensions;
using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Billing.Queries.GetMyPaymentMethods;

/// <summary>
/// Handles <see cref="GetMyPaymentMethodsQuery"/>.
/// </summary>
public sealed class GetMyPaymentMethodsHandler
{
    /// <summary>The Wolverine bus, for the Clients module's card gateway customer query.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Card payment gateway, for reading saved payment methods.</summary>
    private readonly ICardPaymentGateway _cardGateway;

    /// <summary>Data context, for the agreements that let a card be charged without the client.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="bus">The Wolverine bus, for the Clients module's card gateway customer query.</param>
    /// <param name="cardGateway">Card payment gateway, for reading saved payment methods.</param>
    /// <param name="context">Data context, for the agreements that let a card be charged without the client.</param>
    public GetMyPaymentMethodsHandler(IMessageBus bus, ICardPaymentGateway cardGateway, IHostpanelDbContext context)
    {
        _bus = bus;
        _cardGateway = cardGateway;
        _context = context;
    }

    /// <summary>
    /// Retrieves the saved payment methods for the authenticated client.
    /// </summary>
    /// <param name="query">The query. It names no account: this reads the caller's own.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The client's saved cards and bank accounts, empty for a client who has never had one
    /// attached or when the card gateway has no keys; <c>CLIENT_PROFILE_NOT_FOUND</c> when the caller has no client record; or
    /// <c>PAYMENT_GATEWAY_SERVICE_UNAVAILABLE</c> / <c>PAYMENT_METHOD_UPSTREAM_FAILED</c> when the card
    /// gateway could not be reached or refused the call.
    /// </returns>
    public async Task<Result<IReadOnlyList<CardPaymentMethodDto>>> HandleAsync(
        GetMyPaymentMethodsQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var customer = await _bus.GetCallerCardGatewayCustomerAsync(ct);
        if (customer.IsFailure)
        {
            return Result<IReadOnlyList<CardPaymentMethodDto>>.FailureCode(customer.ErrorCode!);
        }

        var customerId = customer.Value!.CustomerId;
        if (customerId is null)
        {
            return Result.Success<IReadOnlyList<CardPaymentMethodDto>>([]);
        }

        // Without the gateway's keys no card can have been saved, and the gateway would throw on its
        // empty key rather than answer (#606).
        if (!_cardGateway.IsConfigured)
        {
            return Result.Success<IReadOnlyList<CardPaymentMethodDto>>([]);
        }

        var methods = await _cardGateway.ListPaymentMethodsAsync(new CardCustomerInstruction(customerId), ct);
        if (methods.Failure is { } failure)
        {
            return Result<IReadOnlyList<CardPaymentMethodDto>>.FailureCode(failure.ToPaymentMethodErrorCode());
        }

        var agreed = await _context.CardChargeConsents
            .Where(c => c.ClientId == customer.Value.ClientId && c.Status == CardConsentStatus.Active)
            .Select(c => c.GatewayPaymentMethodId)
            .ToListAsync(ct);

        return Result.Success<IReadOnlyList<CardPaymentMethodDto>>(
            [.. methods.Methods.Select(m => m with { AutoCharge = agreed.Contains(m.Id) })]);
    }
}
