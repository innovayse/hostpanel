using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Campaigns;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Campaigns.Commands.ScheduleEmailCampaign;

/// <summary>Loads a campaign with its segments and queues it through <see cref="EmailCampaign.Schedule"/>, judging the moment by the application clock.</summary>
public sealed class ScheduleEmailCampaignHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The clock the current moment is read from.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="clock">The clock the current moment is read from.</param>
    public ScheduleEmailCampaignHandler(IHostpanelDbContext context, IClock clock)
    {
        _context = context;
        _clock = clock;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The campaign to queue and when to send it.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success, or <c>CAMPAIGN_NOT_FOUND</c> when the id names no campaign. The domain refuses a campaign that is not a draft
    /// (<c>CAMPAIGN_NOT_DRAFT</c>), one without a segment (<c>CAMPAIGN_NO_SEGMENT</c>) and a moment that is not in the future
    /// (<c>CAMPAIGN_SCHEDULE_IN_PAST</c>).
    /// </returns>
    public async Task<Result> HandleAsync(ScheduleEmailCampaignCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        // The segments load with the campaign (they are auto-included), because a campaign with none cannot be scheduled.
        var campaign = await _context.EmailCampaigns.FirstOrDefaultAsync(x => x.Id == cmd.Id, ct);
        if (campaign is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.CAMPAIGN_NOT_FOUND);
        }

        campaign.Schedule(cmd.At, _clock.UtcNow);

        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}
