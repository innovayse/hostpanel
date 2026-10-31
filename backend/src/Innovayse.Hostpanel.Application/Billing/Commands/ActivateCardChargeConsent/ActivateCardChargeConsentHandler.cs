using Innovayse.Hostpanel.Application.Audit.Common;
using Innovayse.Hostpanel.Application.Billing.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Application.Billing.Commands.ActivateCardChargeConsent;

/// <summary>Handles <see cref="ActivateCardChargeConsentCommand"/>.</summary>
public sealed class ActivateCardChargeConsentHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Stages the activity log line in the same save as the agreement.</summary>
    private readonly IActivityLogger _activity;

    /// <summary>The clock the activation moment is read from.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="activity">Stages the activity log line in the same save as the agreement.</param>
    /// <param name="clock">The clock the activation moment is read from.</param>
    public ActivateCardChargeConsentHandler(IHostpanelDbContext context, IActivityLogger activity, IClock clock)
    {
        _context = context;
        _activity = activity;
        _clock = clock;
    }

    /// <summary>Activates the agreement of the intent, when there is one waiting.</summary>
    /// <param name="cmd">The intent and the card it saved.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success, whether or not an agreement was waiting.</returns>
    public async Task<Result> HandleAsync(ActivateCardChargeConsentCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        if (await _context.ActivateCardConsentAsync(cmd.ClientId, cmd.GatewayIntentId, cmd.PaymentMethodId, _clock.UtcNow, ct))
        {
            await _activity.RecordAsync(
                new ActivityLogEntry(cmd.ClientId, $"Card Saved For Automatic Renewal Charges - Payment Method ID: {cmd.PaymentMethodId}"), ct);
            await _context.SaveChangesAsync(ct);
        }

        return Result.Success();
    }
}
