using Innovayse.Hostpanel.Application.Campaigns.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Campaigns;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Campaigns.Commands.RecordEmailCampaignClick;

/// <summary>
/// Resolves a tracked link to its destination and counts the click. The destination is read from the stored body of the
/// campaign the token's recipient belongs to, by the index the link carries, so the only addresses that can ever be answered are
/// links the admin wrote into that body; nothing the caller sends is ever an address. A click on a link that is not there
/// is refused and counts for nothing.
/// </summary>
public sealed class RecordEmailCampaignClickHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The clock the click moment is read from.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="clock">The clock the click moment is read from.</param>
    public RecordEmailCampaignClickHandler(IHostpanelDbContext context, IClock clock)
    {
        _context = context;
        _clock = clock;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The token and link index from the tracked link.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The absolute http or https address to send the reader to; <c>CAMPAIGN_MAIL_LINK_NOT_FOUND</c> when the token names no recipient or the body has no link at that index.</returns>
    public async Task<Result<string>> HandleAsync(RecordEmailCampaignClickCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var tokenHash = EmailCampaignRecipient.HashToken(cmd.Token);

        // A body cannot change once the campaign leaves draft, so the body read now is the one the mail was built from.
        var body = await _context.EmailCampaigns.AsNoTracking().IgnoreAutoIncludes()
            .Where(c => c.Recipients.Any(r => r.TrackingTokenHash == tokenHash))
            .Select(c => c.Body)
            .FirstOrDefaultAsync(ct);
        IReadOnlyList<string> links = body is null ? [] : CampaignBodyLinks.Extract(body);
        if (cmd.LinkIndex >= links.Count)
        {
            return Result<string>.FailureCode(HostpanelErrorCodes.CAMPAIGN_MAIL_LINK_NOT_FOUND);
        }

        var now = _clock.UtcNow;
        var found = await CampaignMailTracker.RecordAsync(_context, cmd.Token, recipient => recipient.RecordClick(now), ct);

        return found
            ? Result<string>.Success(links[cmd.LinkIndex])
            : Result<string>.FailureCode(HostpanelErrorCodes.CAMPAIGN_MAIL_LINK_NOT_FOUND);
    }
}
