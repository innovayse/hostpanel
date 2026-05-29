namespace Innovayse.Application.Support.Queries.GetSupportOverview;

using Innovayse.Application.Support.DTOs;
using Innovayse.Domain.Support.Interfaces;

/// <summary>
/// Aggregates support overview statistics for a given time period.
/// Computes ticket counts, reply counts, average first response time,
/// and tickets-by-hour distribution.
/// </summary>
public sealed class GetSupportOverviewHandler(ITicketRepository repo)
{
    /// <summary>
    /// Handles <see cref="GetSupportOverviewQuery"/>.
    /// </summary>
    /// <param name="query">The overview query with period filter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="SupportOverviewDto"/> with aggregated stats.</returns>
    public async Task<SupportOverviewDto> HandleAsync(GetSupportOverviewQuery query, CancellationToken ct)
    {
        var (from, to) = ResolvePeriod(query.Period);
        var tickets = await repo.ListByDateRangeAsync(from, to, ct);

        var newTickets = tickets.Count;

        var clientReplies = tickets
            .SelectMany(t => t.Replies)
            .Count(r => !r.IsStaffReply && r.CreatedAt >= from && r.CreatedAt < to);

        var staffReplies = tickets
            .SelectMany(t => t.Replies)
            .Count(r => r.IsStaffReply && r.CreatedAt >= from && r.CreatedAt < to);

        var ticketsWithoutReply = tickets
            .Count(t => !t.Replies.Any(r => r.IsStaffReply));

        string? averageFirstResponse = null;
        var firstResponseTimes = tickets
            .Select(t =>
            {
                var firstStaffReply = t.Replies
                    .Where(r => r.IsStaffReply)
                    .OrderBy(r => r.CreatedAt)
                    .FirstOrDefault();
                return firstStaffReply != null ? (TimeSpan?)(firstStaffReply.CreatedAt - t.CreatedAt) : null;
            })
            .Where(ts => ts.HasValue)
            .Select(ts => ts!.Value)
            .ToList();

        if (firstResponseTimes.Count > 0)
        {
            var avgTicks = (long)firstResponseTimes.Average(ts => ts.Ticks);
            var avg = TimeSpan.FromTicks(avgTicks);
            averageFirstResponse = FormatTimeSpan(avg);
        }

        var ticketsByHour = new int[24];
        foreach (var ticket in tickets)
        {
            var hour = ticket.CreatedAt.Hour;
            ticketsByHour[hour]++;
        }

        return new SupportOverviewDto(
            newTickets,
            clientReplies,
            staffReplies,
            ticketsWithoutReply,
            averageFirstResponse,
            ticketsByHour);
    }

    /// <summary>
    /// Resolves a period string into a date range.
    /// </summary>
    /// <param name="period">The period identifier.</param>
    /// <returns>A tuple of (from, to) date boundaries.</returns>
    private static (DateTimeOffset From, DateTimeOffset To) ResolvePeriod(string period)
    {
        var now = DateTimeOffset.UtcNow;
        var todayStart = new DateTimeOffset(now.Date, TimeSpan.Zero);

        return period.ToLowerInvariant() switch
        {
            "yesterday" => (todayStart.AddDays(-1), todayStart),
            "last7days" => (todayStart.AddDays(-7), now),
            "last30days" => (todayStart.AddDays(-30), now),
            _ => (todayStart, now), // "today" or default
        };
    }

    /// <summary>
    /// Formats a <see cref="TimeSpan"/> as a human-readable string (e.g. "2h 15m").
    /// </summary>
    /// <param name="ts">The time span to format.</param>
    /// <returns>Formatted string.</returns>
    private static string FormatTimeSpan(TimeSpan ts)
    {
        if (ts.TotalDays >= 1)
        {
            return $"{(int)ts.TotalDays}d {ts.Hours}h {ts.Minutes}m";
        }

        if (ts.TotalHours >= 1)
        {
            return $"{(int)ts.TotalHours}h {ts.Minutes}m";
        }

        return $"{ts.Minutes}m";
    }
}
