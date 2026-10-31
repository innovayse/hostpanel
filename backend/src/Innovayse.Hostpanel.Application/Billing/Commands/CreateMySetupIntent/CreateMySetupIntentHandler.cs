using System.Globalization;
using Innovayse.Hostpanel.Application.Billing.Commands.EnsureCardGatewayCustomer;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Extensions;
using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Billing.Commands.CreateMySetupIntent;

/// <summary>Handles <see cref="CreateMySetupIntentCommand"/>.</summary>
public sealed class CreateMySetupIntentHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Card payment gateway, for opening the setup intent.</summary>
    private readonly ICardPaymentGateway _cardGateway;

    /// <summary>Message bus, for the caller's account and the customer.</summary>
    private readonly IMessageBus _bus;

    /// <summary>The clock the agreement's moment is read from.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="cardGateway">Card payment gateway, for opening the setup intent.</param>
    /// <param name="bus">Message bus, for the caller's account and the customer.</param>
    /// <param name="clock">The clock the agreement's moment is read from.</param>
    public CreateMySetupIntentHandler(IHostpanelDbContext context, ICardPaymentGateway cardGateway, IMessageBus bus, IClock clock)
    {
        _context = context;
        _cardGateway = cardGateway;
        _bus = bus;
        _clock = clock;
    }

    /// <summary>Opens a setup intent and records the pending agreement tied to it.</summary>
    /// <param name="cmd">The agreement and where it came from.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The setup intent; <c>CLIENT_PROFILE_NOT_FOUND</c> when the caller acts for no account;
    /// <c>CLIENT_SUSPENDED</c>, <c>CLIENT_CLOSED</c> or <c>CLIENT_CARD_PAYMENT_DISABLED</c> when the account may not use cards;
    /// <c>PAYMENT_METHOD_UNAVAILABLE</c> when the deployment has no card gateway; or the gateway's refusal.
    /// </returns>
    public async Task<Result<CardSetupIntentDto>> HandleAsync(CreateMySetupIntentCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var configured = _cardGateway.RequireConfigured();
        if (configured.IsFailure)
        {
            return Result<CardSetupIntentDto>.FailureCode(configured.ErrorCode!);
        }

        var caller = await _bus.GetCallerCardGatewayCustomerAsync(ct);
        if (caller.IsFailure)
        {
            return Result<CardSetupIntentDto>.FailureCode(caller.ErrorCode!);
        }

        var clientId = caller.Value!.ClientId;
        var mayTransact = await _bus.EnsureClientMayTransactAsync(clientId, ct);
        if (mayTransact.IsFailure)
        {
            return Result<CardSetupIntentDto>.FailureCode(mayTransact.ErrorCode!);
        }

        var mayPayByCard = await _bus.EnsureClientMayPayByCardAsync(clientId, ct);
        if (mayPayByCard.IsFailure)
        {
            return Result<CardSetupIntentDto>.FailureCode(mayPayByCard.ErrorCode!);
        }

        var customer = await _bus.InvokeAsync<Result<string>>(new EnsureCardGatewayCustomerCommand(clientId), ct);
        if (customer.IsFailure)
        {
            return Result<CardSetupIntentDto>.FailureCode(customer.ErrorCode!);
        }

        var intent = await _cardGateway.CreateSetupIntentAsync(
            new CardSetupIntentInstruction(
                customer.Value!,
                clientId,
                string.Create(CultureInfo.InvariantCulture, $"setup-client-{clientId}-{Guid.NewGuid():N}")),
            ct);
        if (intent.Failure is { } failure)
        {
            return Result<CardSetupIntentDto>.FailureCode(failure.ToPaymentMethodErrorCode());
        }

        // The agreement waits as Pending until the gateway confirms, from its own record, that the card was saved.
        _context.CardChargeConsents.Add(CardChargeConsent.Request(
            clientId, intent.SetupIntentId!, CardConsentSource.AddCard, cmd.Origin?.IpAddress, cmd.Origin?.UserAgent, _clock.UtcNow));
        await _context.SaveChangesAsync(ct);

        return Result.Success(new CardSetupIntentDto(intent.SetupIntentId!, intent.ClientSecret!));
    }
}
