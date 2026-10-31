using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Billing.Commands.RequestCardChargeConsent;

/// <summary>Handles <see cref="RequestCardChargeConsentCommand"/>.</summary>
public sealed class RequestCardChargeConsentHandler
{
    /// <summary>The longest user agent kept with an agreement.</summary>
    private const int UserAgentMaxLength = 512;

    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The clock the agreement's moment is read from.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="clock">The clock the agreement's moment is read from.</param>
    public RequestCardChargeConsentHandler(IHostpanelDbContext context, IClock clock)
    {
        _context = context;
        _clock = clock;
    }

    /// <summary>Stores the pending agreement, once per gateway intent.</summary>
    /// <param name="cmd">The agreement.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success, also when the intent already has its agreement (a repeated request for the same intent).</returns>
    public async Task<Result> HandleAsync(RequestCardChargeConsentCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        if (await _context.CardChargeConsents.AnyAsync(c => c.GatewayIntentId == cmd.GatewayIntentId, ct))
        {
            return Result.Success();
        }

        var userAgent = cmd.Origin.UserAgent is { Length: > UserAgentMaxLength } longAgent ? longAgent[..UserAgentMaxLength] : cmd.Origin.UserAgent;
        _context.CardChargeConsents.Add(CardChargeConsent.Request(
            cmd.ClientId, cmd.GatewayIntentId, cmd.Source, cmd.Origin.IpAddress, userAgent, _clock.UtcNow));
        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // The unique index on the intent: a parallel request recorded it between the check and the insert.
            _context.DiscardPendingChanges();
            if (!await _context.CardChargeConsents.AnyAsync(c => c.GatewayIntentId == cmd.GatewayIntentId, ct))
            {
                throw;
            }
        }

        return Result.Success();
    }
}
