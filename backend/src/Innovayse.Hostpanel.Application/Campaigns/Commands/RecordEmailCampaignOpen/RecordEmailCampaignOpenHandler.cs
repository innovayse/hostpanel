using Innovayse.Hostpanel.Application.Campaigns.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Application.Campaigns.Commands.RecordEmailCampaignOpen;

/// <summary>
/// Counts a load of the tracking pixel on the recipient the token names: every load adds to the open count and the first one
/// stamps the moment. The recipient is found by the hash of the token, the same lookup the unsubscribe link uses.
/// </summary>
public sealed class RecordEmailCampaignOpenHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The clock the open moment is read from.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="clock">The clock the open moment is read from.</param>
    public RecordEmailCampaignOpenHandler(IHostpanelDbContext context, IClock clock)
    {
        _context = context;
        _clock = clock;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The token from the pixel address.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success once the open is counted; <c>CAMPAIGN_MAIL_LINK_NOT_FOUND</c> when the token names no recipient.</returns>
    public async Task<Result> HandleAsync(RecordEmailCampaignOpenCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var now = _clock.UtcNow;
        var found = await CampaignMailTracker.RecordAsync(_context, cmd.Token, recipient => recipient.RecordOpen(now), ct);

        return found ? Result.Success() : Result.FailureCode(HostpanelErrorCodes.CAMPAIGN_MAIL_LINK_NOT_FOUND);
    }
}
