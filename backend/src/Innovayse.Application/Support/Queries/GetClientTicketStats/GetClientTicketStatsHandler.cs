namespace Innovayse.Application.Support.Queries.GetClientTicketStats;

using Innovayse.Application.Support.DTOs;
using Innovayse.Domain.Support.Interfaces;

/// <summary>
/// Computes ticket statistics for a client broken down by time period.
/// </summary>
public sealed class GetClientTicketStatsHandler(ITicketRepository repo)
{
    /// <summary>
    /// Handles <see cref="GetClientTicketStatsQuery"/>.
    /// </summary>
    /// <param name="query">The stats query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Ticket statistics for the client.</returns>
    public async Task<ClientTicketStatsDto> HandleAsync(GetClientTicketStatsQuery query, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var thisMonthStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var lastMonthStart = thisMonthStart.AddMonths(-1);
        var thisYearStart = new DateTimeOffset(now.Year, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var lastYearStart = new DateTimeOffset(now.Year - 1, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var lastYearEnd = thisYearStart;

        var thisMonth = await repo.CountByClientIdAndDateRangeAsync(query.ClientId, thisMonthStart, now, ct);
        var lastMonth = await repo.CountByClientIdAndDateRangeAsync(query.ClientId, lastMonthStart, thisMonthStart, ct);
        var thisYear = await repo.CountByClientIdAndDateRangeAsync(query.ClientId, thisYearStart, now, ct);
        var lastYear = await repo.CountByClientIdAndDateRangeAsync(query.ClientId, lastYearStart, lastYearEnd, ct);

        return new ClientTicketStatsDto(thisMonth, lastMonth, thisYear, lastYear);
    }
}
