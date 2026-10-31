using Innovayse.Hostpanel.Application.Audit.Common;
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

namespace Innovayse.Hostpanel.Application.Billing.Commands.ConfirmMySetupIntent;

/// <summary>Handles <see cref="ConfirmMySetupIntentCommand"/>.</summary>
public sealed class ConfirmMySetupIntentHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Card payment gateway, for reading the setup intent back and setting the default card.</summary>
    private readonly ICardPaymentGateway _cardGateway;

    /// <summary>Message bus, for the caller's account.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Stages the activity log line in the same save as the agreement.</summary>
    private readonly IActivityLogger _activity;

    /// <summary>The clock the activation moment is read from.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="cardGateway">Card payment gateway, for reading the setup intent back and setting the default card.</param>
    /// <param name="bus">Message bus, for the caller's account.</param>
    /// <param name="activity">Stages the activity log line in the same save as the agreement.</param>
    /// <param name="clock">The clock the activation moment is read from.</param>
    public ConfirmMySetupIntentHandler(
        IHostpanelDbContext context, ICardPaymentGateway cardGateway, IMessageBus bus, IActivityLogger activity, IClock clock)
    {
        _context = context;
        _cardGateway = cardGateway;
        _bus = bus;
        _activity = activity;
        _clock = clock;
    }

    /// <summary>Verifies the setup intent with the gateway and activates the agreement.</summary>
    /// <param name="cmd">The setup intent.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success once the agreement is active (also when it already was); <c>CARD_SETUP_NOT_FOUND</c> when the
    /// caller's account opened no such setup; <c>CARD_SETUP_NOT_COMPLETED</c> when the gateway did not save
    /// the card; <c>PAYMENT_METHOD_NOT_OWNED</c> when the saved card is on another customer; or the gateway's refusal.
    /// </returns>
    public async Task<Result> HandleAsync(ConfirmMySetupIntentCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var configured = _cardGateway.RequireConfigured();
        if (configured.IsFailure)
        {
            return configured;
        }

        var caller = await _bus.GetCallerCardGatewayCustomerAsync(ct);
        if (caller.IsFailure)
        {
            return Result.FailureCode(caller.ErrorCode!);
        }

        var clientId = caller.Value!.ClientId;
        var consent = await _context.CardChargeConsents.FirstOrDefaultAsync(
            c => c.ClientId == clientId && c.GatewayIntentId == cmd.SetupIntentId && c.Source == CardConsentSource.AddCard, ct);
        if (consent is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.CARD_SETUP_NOT_FOUND);
        }

        if (consent.Status == CardConsentStatus.Active)
        {
            return Result.Success();
        }

        // What the browser says is never taken for granted: the gateway's own record decides.
        var status = await _cardGateway.VerifySetupIntentAsync(new CardSetupIntentVerifyInstruction(cmd.SetupIntentId), ct);
        if (status.Failure is { } failure)
        {
            return Result.FailureCode(failure.ToPaymentMethodErrorCode());
        }

        if (!status.Succeeded || status.PaymentMethodId is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.CARD_SETUP_NOT_COMPLETED);
        }

        if (status.CustomerId is null || status.CustomerId != caller.Value.CustomerId)
        {
            return Result.FailureCode(HostpanelErrorCodes.PAYMENT_METHOD_NOT_OWNED);
        }

        if (!consent.Activate(status.PaymentMethodId, _clock.UtcNow))
        {
            // Withdrawn between the request and now: the card stays saved, the agreement does not come back.
            return Result.FailureCode(HostpanelErrorCodes.CARD_SETUP_NOT_COMPLETED);
        }

        await _activity.RecordAsync(
            new ActivityLogEntry(clientId, $"Card Saved For Automatic Renewal Charges - Payment Method ID: {status.PaymentMethodId}"), ct);
        await _context.SaveChangesAsync(ct);

        // The first card becomes the default, so automatic charges have a card to use without a second step.
        var methods = await _cardGateway.ListPaymentMethodsAsync(new CardCustomerInstruction(status.CustomerId), ct);
        if (methods.Failure is null && !methods.Methods.Any(m => m.IsDefault))
        {
            await _cardGateway.SetDefaultPaymentMethodAsync(new CardDefaultMethodInstruction(status.CustomerId, status.PaymentMethodId), ct);
        }

        return Result.Success();
    }
}
