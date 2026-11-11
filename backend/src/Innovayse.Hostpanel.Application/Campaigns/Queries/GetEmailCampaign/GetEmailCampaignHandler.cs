using Innovayse.Hostpanel.Application.Campaigns.Common;
using Innovayse.Hostpanel.Application.Campaigns.Extensions;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Campaigns.Queries.GetEmailCampaign;

/// <summary>Loads one campaign with its segments, counts its recipients in the database, and maps it to <see cref="EmailCampaignDto"/>.</summary>
public sealed class GetEmailCampaignHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public GetEmailCampaignHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The campaign id to retrieve.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The campaign, or <c>CAMPAIGN_NOT_FOUND</c> when the id names no campaign.</returns>
    public async Task<Result<EmailCampaignDto>> HandleAsync(GetEmailCampaignQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var campaign = await _context.EmailCampaigns.AsNoTracking().FirstOrDefaultAsync(x => x.Id == query.Id, ct);
        if (campaign is null)
        {
            return Result<EmailCampaignDto>.FailureCode(HostpanelErrorCodes.CAMPAIGN_NOT_FOUND);
        }

        var stats = await _context.EmailCampaignRecipients.AsNoTracking().StatsByCampaignAsync([campaign.Id], ct);
        return Result<EmailCampaignDto>.Success(campaign.ToDto(stats[campaign.Id]));
    }
}
