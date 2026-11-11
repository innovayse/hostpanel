using Innovayse.Hostpanel.Application.Campaigns.Extensions;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Campaigns;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Campaigns.Commands.UpdateEmailCampaign;

/// <summary>Loads a campaign with its segments, replaces its content and segments through <see cref="EmailCampaign.Update"/>, and persists the result.</summary>
public sealed class UpdateEmailCampaignHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public UpdateEmailCampaignHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The new content and segments of the campaign.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success, or <c>CAMPAIGN_NOT_FOUND</c> when the id names no campaign. A campaign that is no longer a draft is refused by the domain as <c>CAMPAIGN_NOT_DRAFT</c>.</returns>
    public async Task<Result> HandleAsync(UpdateEmailCampaignCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var campaign = await _context.EmailCampaigns.FirstOrDefaultAsync(x => x.Id == cmd.Id, ct);
        if (campaign is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.CAMPAIGN_NOT_FOUND);
        }

        campaign.Update(cmd.Subject, cmd.Body, cmd.Segments.ToDefinitions());

        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}
