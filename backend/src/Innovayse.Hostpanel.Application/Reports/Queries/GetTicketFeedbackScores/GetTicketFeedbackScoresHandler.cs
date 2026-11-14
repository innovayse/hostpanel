using Innovayse.Hostpanel.Application.Common.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Reports.Common;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Reports.Queries.GetTicketFeedbackScores;

/// <summary>Handles <see cref="GetTicketFeedbackScoresQuery"/>.</summary>
public sealed class GetTicketFeedbackScoresHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public GetTicketFeedbackScoresHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The date range to report on; both bounds are optional.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>One row per admin, with the count of each rating and the mean. Never refuses.</returns>
    public async Task<Result<TicketFeedbackScoresDto>> HandleAsync(
        GetTicketFeedbackScoresQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var tickets = _context.Tickets.AsNoTracking().Where(t => t.Rating != null);

        if (query.From.HasValue)
        {
            var fromDto = query.From.Value.StartOfDayUtc();
            tickets = tickets.Where(t => t.FeedbackAt >= fromDto);
        }

        if (query.To.HasValue)
        {
            var toDto = query.To.Value.EndOfDayExclusiveUtc();
            tickets = tickets.Where(t => t.FeedbackAt < toDto);
        }

        var raw = await tickets.ToListAsync(ct);

        var rows = raw
            .GroupBy(t => t.AssignedToStaffId.HasValue ? $"Staff #{t.AssignedToStaffId}" : "Unassigned")
            .Select(g =>
            {
                int CountOf(int rating) => g.Count(t => t.Rating == rating);
                return new TicketFeedbackScoreRowDto(
                    g.Key,
                    CountOf(1), CountOf(2), CountOf(3), CountOf(4), CountOf(5),
                    CountOf(6), CountOf(7), CountOf(8), CountOf(9), CountOf(10),
                    g.Count(),
                    Math.Round(g.Average(t => (double)t.Rating!.Value), 2));
            })
            .OrderByDescending(r => r.TotalRatings)
            .ToList();

        return Result<TicketFeedbackScoresDto>.Success(new TicketFeedbackScoresDto(rows));
    }
}
