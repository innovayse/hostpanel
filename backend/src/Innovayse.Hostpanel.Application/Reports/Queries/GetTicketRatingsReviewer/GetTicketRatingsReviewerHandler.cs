using Innovayse.Hostpanel.Application.Common.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Reports.Common;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Reports.Queries.GetTicketRatingsReviewer;

/// <summary>Handles <see cref="GetTicketRatingsReviewerQuery"/>.</summary>
public sealed class GetTicketRatingsReviewerHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public GetTicketRatingsReviewerHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">Minimum rating and date range to narrow by.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>One row per rated reply, newest first. Never refuses.</returns>
    public async Task<Result<TicketRatingsReviewerDto>> HandleAsync(
        GetTicketRatingsReviewerQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var tickets = _context.Tickets.AsNoTracking().Where(t => t.Rating != null);

        if (query.MinRating.HasValue)
        {
            tickets = tickets.Where(t => t.Rating >= query.MinRating.Value);
        }

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

        var raw = await tickets.OrderByDescending(t => t.FeedbackAt).ToListAsync(ct);

        var rows = raw.Select(t => new TicketRatingRowDto(
            t.Id,
            t.FeedbackAt!.Value,
            t.FeedbackComment ?? t.Subject,
            t.AssignedToStaffId.HasValue ? $"Staff #{t.AssignedToStaffId}" : "Unassigned",
            t.Rating!.Value))
            .ToList();

        return Result<TicketRatingsReviewerDto>.Success(new TicketRatingsReviewerDto(rows));
    }
}
