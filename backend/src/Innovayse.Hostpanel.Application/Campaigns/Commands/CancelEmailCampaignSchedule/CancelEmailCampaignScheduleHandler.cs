using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Campaigns;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Campaigns.Commands.CancelEmailCampaignSchedule;

/// <summary>Takes a scheduled <see cref="EmailCampaign"/> back to a draft through <see cref="EmailCampaign.CancelSchedule"/> and persists it.</summary>
public sealed class CancelEmailCampaignScheduleHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public CancelEmailCampaignScheduleHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The campaign whose schedule is cancelled.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success, or <c>CAMPAIGN_NOT_FOUND</c> when the id names no campaign. A campaign that is not scheduled is refused by the domain as <c>CAMPAIGN_NOT_SCHEDULED</c>.</returns>
    public async Task<Result> HandleAsync(CancelEmailCampaignScheduleCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var campaign = await _context.EmailCampaigns.FirstOrDefaultAsync(x => x.Id == cmd.Id, ct);
        if (campaign is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.CAMPAIGN_NOT_FOUND);
        }

        campaign.CancelSchedule();

        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}
