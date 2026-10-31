using Innovayse.Hostpanel.Application.Audit.Common;
using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Billing.Commands.RevokeMyCardConsents;

/// <summary>Handles <see cref="RevokeMyCardConsentsCommand"/>.</summary>
public sealed class RevokeMyCardConsentsHandler
{
    /// <summary>The reason recorded on an agreement the client withdrew.</summary>
    private const string WithdrawnByClient = "Withdrawn by the client";

    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Message bus, for the caller's account.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Stages the activity log line in the same save as the withdrawal.</summary>
    private readonly IActivityLogger _activity;

    /// <summary>The clock the withdrawal's moment is read from.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="bus">Message bus, for the caller's account.</param>
    /// <param name="activity">Stages the activity log line in the same save as the withdrawal.</param>
    /// <param name="clock">The clock the withdrawal's moment is read from.</param>
    public RevokeMyCardConsentsHandler(IHostpanelDbContext context, IMessageBus bus, IActivityLogger activity, IClock clock)
    {
        _context = context;
        _bus = bus;
        _activity = activity;
        _clock = clock;
    }

    /// <summary>Withdraws every pending or active agreement of the caller's account.</summary>
    /// <param name="cmd">The command; it names no account.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success (also when there was nothing to withdraw), or <c>CLIENT_PROFILE_NOT_FOUND</c>.</returns>
    public async Task<Result> HandleAsync(RevokeMyCardConsentsCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var caller = await _bus.GetCallerCardGatewayCustomerAsync(ct);
        if (caller.IsFailure)
        {
            return Result.FailureCode(caller.ErrorCode!);
        }

        var clientId = caller.Value!.ClientId;
        var open = await _context.CardChargeConsents
            .Where(c => c.ClientId == clientId && c.Status != CardConsentStatus.Revoked)
            .ToListAsync(ct);
        if (open.Count == 0)
        {
            return Result.Success();
        }

        var now = _clock.UtcNow;
        foreach (var consent in open)
        {
            consent.Revoke(WithdrawnByClient, now);
        }

        await _activity.RecordAsync(new ActivityLogEntry(clientId, "Automatic Renewal Card Charges Turned Off"), ct);
        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}
