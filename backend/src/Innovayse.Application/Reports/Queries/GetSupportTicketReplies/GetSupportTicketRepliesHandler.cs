namespace Innovayse.Application.Reports.Queries.GetSupportTicketReplies;

using Innovayse.Application.Reports.Common;
using Innovayse.Application.Reports.Interfaces;

/// <summary>Handles <see cref="GetSupportTicketRepliesQuery"/>.</summary>
/// <param name="repo">Reporting data access.</param>
public sealed class GetSupportTicketRepliesHandler(IReportRepository repo)
{
    /// <summary>Returns how many ticket replies each admin posted on each day of the month.</summary>
    /// <param name="query">Year and month to report on; either may be omitted.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Reply counts per admin per day for the month.</returns>
    public Task<SupportTicketRepliesDto> HandleAsync(
        GetSupportTicketRepliesQuery query, CancellationToken ct)
    {
        // The period defaults here rather than at the endpoint so every caller of the query,
        // HTTP or not, lands on the same month.
        var now = DateTime.UtcNow;
        return repo.GetSupportTicketRepliesAsync(query.Year ?? now.Year, query.Month ?? now.Month, ct);
    }
}
