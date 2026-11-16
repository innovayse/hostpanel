using Innovayse.Hostpanel.Application.Campaigns.Common;
using Innovayse.Hostpanel.Application.Campaigns.Extensions;
using Innovayse.Hostpanel.Application.Common.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Extensions;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Campaigns.Queries.ListEmailCampaigns;

/// <summary>
/// Loads one page of campaigns, newest first, narrowed by the search text on the subject and by status in SQL ahead of
/// paging, and counts the page's recipients by status in one grouped query, so a list costs three queries however many
/// campaigns and recipients there are and never loads a recipient row.
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
    /// <param name="query">The page, page size, search and status filter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A page of campaigns, newest first. Never refuses.</returns>
    public async Task<Result<PaginatedList<EmailCampaignListItemDto>>> HandleAsync(ListEmailCampaignsQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var filtered = _context.EmailCampaigns.AsNoTracking().IgnoreAutoIncludes().Search(query.Search, x => x.Subject);

        if (query.Status is { } status)
        {
            filtered = filtered.Where(x => x.Status == status);
        }

        // The id ends the order so a page never repeats or skips a row between requests.
        var ordered = filtered.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id);
        var totalCount = await ordered.CountAsync(ct);
        var campaigns = await ordered.Paginate(query.Page, query.PageSize).ToListAsync(ct);
        var stats = await _context.EmailCampaignRecipients.AsNoTracking().StatsByCampaignAsync(campaigns.Select(x => x.Id).ToList(), ct);

        return Result.Success(new PaginatedList<EmailCampaignListItemDto>(
            campaigns.Select(x => x.ToListItem(stats[x.Id])).ToList(), totalCount, query.Page, query.PageSize));
    }
}
