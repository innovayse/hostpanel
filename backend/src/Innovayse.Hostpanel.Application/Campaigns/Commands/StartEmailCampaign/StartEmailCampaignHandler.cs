using Innovayse.Hostpanel.Application.Campaigns.Common;
using Innovayse.Hostpanel.Application.Campaigns.Extensions;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Campaigns.Commands.StartEmailCampaign;

/// <summary>
/// Moves a scheduled campaign to sending and fixes its recipients in the same save: the audience is resolved once, with the
/// segments the campaign holds, and each reached client (one per sign-in address) becomes a pending recipient. A client
/// mailed later never changes who this campaign went to.
/// </summary>
public sealed class StartEmailCampaignHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Resolves which clients the segments reach, and the address each is mailed at.</summary>
    private readonly CampaignAudience _audience;

    /// <summary>The clock the relative expiry rules are measured from.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="audience">Resolves which clients the segments reach, and the address each is mailed at.</param>
    /// <param name="clock">The clock the relative expiry rules are measured from.</param>
    public StartEmailCampaignHandler(IHostpanelDbContext context, CampaignAudience audience, IClock clock)
    {
        _context = context;
        _audience = audience;
        _clock = clock;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The campaign to start.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success, or <c>CAMPAIGN_NOT_FOUND</c> when the id names no campaign. The domain refuses a campaign that is not scheduled (<c>CAMPAIGN_NOT_SCHEDULED</c>).</returns>
    public async Task<Result> HandleAsync(StartEmailCampaignCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var campaign = await _context.EmailCampaigns.FirstOrDefaultAsync(x => x.Id == cmd.Id, ct);
        if (campaign is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.CAMPAIGN_NOT_FOUND);
        }

        campaign.StartSending();

        foreach (var client in await _audience.ReachAsync(campaign.Segments.ToDtos(), _clock.UtcNow, ct))
        {
            // ReachAsync only returns clients that have an address.
            campaign.AddRecipient(client.Id, client.Email!, client.Language);
        }

        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}
