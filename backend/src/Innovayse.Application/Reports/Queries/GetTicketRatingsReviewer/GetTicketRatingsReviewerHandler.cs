namespace Innovayse.Application.Reports.Queries.GetTicketRatingsReviewer;

using Innovayse.Application.Reports.DTOs;
using Innovayse.Application.Reports.Interfaces;

/// <summary>Handles <see cref="GetTicketRatingsReviewerQuery"/>.</summary>
/// <param name="repo">Reporting data access.</param>
public sealed class GetTicketRatingsReviewerHandler(IReportRepository repo)
{
    /// <summary>Returns the rated ticket replies that match the filters.</summary>
    /// <param name="query">Minimum rating and date range to narrow by.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>One row per rated reply.</returns>
    public Task<TicketRatingsReviewerDto> HandleAsync(
        GetTicketRatingsReviewerQuery query, CancellationToken ct)
        => repo.GetTicketRatingsReviewerAsync(query.MinRating, query.From, query.To, ct);
}
