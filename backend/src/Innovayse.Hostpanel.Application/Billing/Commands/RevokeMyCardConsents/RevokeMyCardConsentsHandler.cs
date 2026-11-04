using Innovayse.Hostpanel.Application.Audit.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Billing.Commands.RevokeMyCardConsents;

/// <summary>Handles <see cref="RevokeMyCardConsentsCommand"/>.</summary>
public sealed class RevokeMyCardConsentsHandler
{
    /// <summary>How many times the withdrawal is tried again after it loses a race with a confirmation of the same agreement.</summary>
    private const int MaxAttempts = 3;

    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Resolves the caller's client record.</summary>
    private readonly IClientAccess _access;

    /// <summary>Stages the activity log line in the same save as the withdrawal.</summary>
    private readonly IActivityLogger _activity;

    /// <summary>The clock the withdrawal's moment is read from.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="access">Resolves the caller's client record.</param>
    /// <param name="activity">Stages the activity log line in the same save as the withdrawal.</param>
    /// <param name="clock">The clock the withdrawal's moment is read from.</param>
    public RevokeMyCardConsentsHandler(IHostpanelDbContext context, IClientAccess access, IActivityLogger activity, IClock clock)
    {
        _context = context;
        _access = access;
        _activity = activity;
        _clock = clock;
    }

    /// <summary>Withdraws every pending or active agreement of the caller's account.</summary>
    /// <param name="cmd">The command; it names no account.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success (also when there was nothing to withdraw), or <c>CLIENT_PROFILE_NOT_FOUND</c>.</returns>
    /// <exception cref="ConcurrencyConflictException">The withdrawal kept losing to a confirmation of the same agreement after <see cref="MaxAttempts"/> tries.</exception>
    public async Task<Result> HandleAsync(RevokeMyCardConsentsCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var caller = await _access.RequireClientIdAsync(ct);
        if (caller.IsFailure)
        {
            return Result.FailureCode(caller.ErrorCode!);
        }

        var clientId = caller.Value;
        for (var attempt = 1; ; attempt++)
        {
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
                consent.Revoke(CardConsentRevocationReason.WithdrawnByClient, now);
            }

            await _activity.RecordAsync(new ActivityLogEntry(clientId, "Automatic Renewal Card Charges Turned Off"), ct);
            try
            {
                await _context.SaveChangesAsync(ct);
                return Result.Success();
            }
            catch (ConcurrencyConflictException) when (attempt < MaxAttempts)
            {
                // A confirmation activated one of the agreements between the read and the save. A withdrawal is
                // final, so it is read and withdrawn again rather than given up. Everything is detached, not only
                // reset: a reset keeps the stale row version, and the read would hand the same stale rows back.
                _context.DetachAll();
            }
        }
    }
}
