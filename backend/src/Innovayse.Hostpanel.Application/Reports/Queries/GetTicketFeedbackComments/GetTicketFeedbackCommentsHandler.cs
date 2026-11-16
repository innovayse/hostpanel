using Innovayse.Hostpanel.Application.Common.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Reports.Common;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Reports.Queries.GetTicketFeedbackComments;

/// <summary>Handles <see cref="GetTicketFeedbackCommentsQuery"/>.</summary>
public sealed class GetTicketFeedbackCommentsHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public GetTicketFeedbackCommentsHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">Admin and date range to narrow by.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>One row per piece of feedback, newest first. Never refuses.</returns>
    public async Task<Result<TicketFeedbackCommentsDto>> HandleAsync(
        GetTicketFeedbackCommentsQuery query, CancellationToken ct)
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

        var raw = await tickets.OrderByDescending(t => t.FeedbackAt).ToListAsync(ct);

        // StaffName here is "Staff #{id}" rather than a real name: the assignee is stored as a
        // plain staff id with no local join target.
        var rows = raw
            .Where(t => string.IsNullOrWhiteSpace(query.StaffName)
                || (t.AssignedToStaffId.HasValue && $"Staff #{t.AssignedToStaffId}" == query.StaffName))
            .Select(t => new TicketFeedbackCommentRowDto(
                t.Id,
                t.AssignedToStaffId.HasValue ? $"Staff #{t.AssignedToStaffId}" : "Unassigned",
                t.Subject,
                t.FeedbackComment,
                t.Rating!.Value,
                t.FeedbackLeftBy ?? string.Empty,
                t.FeedbackAt!.Value))
            .ToList();

        return Result<TicketFeedbackCommentsDto>.Success(new TicketFeedbackCommentsDto(rows));
    }
}
