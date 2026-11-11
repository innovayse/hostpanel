using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Campaigns;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Campaigns.Commands.DeleteEmailCampaign;

/// <summary>Deletes a draft <see cref="EmailCampaign"/>; its segments go with it. A campaign that has left the draft state is kept.</summary>
public sealed class DeleteEmailCampaignHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public DeleteEmailCampaignHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The campaign to remove.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success, or <c>CAMPAIGN_NOT_FOUND</c> when the id names no campaign. A campaign that is not a draft is refused by the domain as <c>CAMPAIGN_NOT_DRAFT</c>.</returns>
    public async Task<Result> HandleAsync(DeleteEmailCampaignCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var campaign = await _context.EmailCampaigns.FirstOrDefaultAsync(x => x.Id == cmd.Id, ct);
        if (campaign is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.CAMPAIGN_NOT_FOUND);
        }

        campaign.EnsureCanDelete();

        _context.EmailCampaigns.Remove(campaign);
        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}
