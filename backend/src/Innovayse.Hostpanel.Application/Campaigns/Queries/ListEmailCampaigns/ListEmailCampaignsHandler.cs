using Innovayse.Hostpanel.Application.Campaigns.Common;
using Innovayse.Hostpanel.Application.Campaigns.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Campaigns.Queries.ListEmailCampaigns;

/// <summary>
/// Loads every campaign, newest first, and counts all their recipients by status in one grouped query, so a list
/// costs two queries however many campaigns and recipients there are and never loads a recipient row.
/// </summary>
public sealed class ListEmailCampaignsHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public ListEmailCampaignsHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The query; it carries no input.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Every campaign, newest first. Never refuses.</returns>
    public async Task<Result<IReadOnlyList<EmailCampaignListItemDto>>> HandleAsync(ListEmailCampaignsQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var campaigns = await _context.EmailCampaigns.AsNoTracking().IgnoreAutoIncludes().OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).ToListAsync(ct);
        var stats = await _context.EmailCampaignRecipients.AsNoTracking().StatsByCampaignAsync(campaigns.Select(x => x.Id).ToList(), ct);

        return Result<IReadOnlyList<EmailCampaignListItemDto>>.Success(campaigns.Select(x => x.ToListItem(stats[x.Id])).ToList());
    }
}
