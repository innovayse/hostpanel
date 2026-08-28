namespace Innovayse.Application.Reports.Queries.GetCreditsReviewer;

using Innovayse.Application.Reports.DTOs;
using Innovayse.Application.Reports.Interfaces;

/// <summary>Handles <see cref="GetCreditsReviewerQuery"/>.</summary>
/// <param name="repo">Reporting data access.</param>
public sealed class GetCreditsReviewerHandler(IReportRepository repo)
{
    /// <summary>Returns the credits issued to clients that match the filters.</summary>
    /// <param name="query">Client, date range and amount range to narrow by.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The matching credits with their totals.</returns>
    public Task<CreditsReviewerDto> HandleAsync(GetCreditsReviewerQuery query, CancellationToken ct)
        => repo.GetCreditsReviewerAsync(
            query.ClientId, query.From, query.To, query.MinAmount, query.MaxAmount, ct);
}
