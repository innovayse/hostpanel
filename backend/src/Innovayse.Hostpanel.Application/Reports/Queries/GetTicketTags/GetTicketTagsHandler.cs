using Innovayse.Hostpanel.Application.Common.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Reports.Common;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Reports.Queries.GetTicketTags;

/// <summary>Handles <see cref="GetTicketTagsQuery"/>.</summary>
public sealed class GetTicketTagsHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public GetTicketTagsHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The date range to report on; both bounds are optional.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>One row per distinct tag, with its usage count, most used first. Never refuses.</returns>
    public async Task<Result<TicketTagsDto>> HandleAsync(GetTicketTagsQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Every tag belongs to exactly one ticket, so flattening through the navigation reaches
        // every row of the TicketTags table.
        var tags = _context.Tickets.AsNoTracking().SelectMany(t => t.Tags);

        if (query.From.HasValue || query.To.HasValue)
        {
            var ticketIds = _context.Tickets.AsNoTracking().AsQueryable();

            if (query.From.HasValue)
            {
                var fromDto = query.From.Value.StartOfDayUtc();
                ticketIds = ticketIds.Where(t => t.CreatedAt >= fromDto);
            }

            if (query.To.HasValue)
            {
                var toDto = query.To.Value.EndOfDayExclusiveUtc();
                ticketIds = ticketIds.Where(t => t.CreatedAt < toDto);
            }

            var ids = await ticketIds.Select(t => t.Id).ToListAsync(ct);
            tags = tags.Where(tag => ids.Contains(tag.TicketId));
        }

        var raw = await tags
            .GroupBy(t => t.Name)
            .Select(g => new { Tag = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync(ct);

        var rows = raw.Select(x => new TicketTagRowDto(x.Tag, x.Count)).ToList();
        return Result<TicketTagsDto>.Success(new TicketTagsDto(rows));
    }
}
