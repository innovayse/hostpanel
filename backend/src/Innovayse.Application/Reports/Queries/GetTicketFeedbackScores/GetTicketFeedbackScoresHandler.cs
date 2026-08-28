namespace Innovayse.Application.Reports.Queries.GetTicketFeedbackScores;

using Innovayse.Application.Reports.DTOs;
using Innovayse.Application.Reports.Interfaces;

/// <summary>Handles <see cref="GetTicketFeedbackScoresQuery"/>.</summary>
/// <param name="repo">Reporting data access.</param>
public sealed class GetTicketFeedbackScoresHandler(IReportRepository repo)
{
    /// <summary>Returns each admin average feedback score over the requested range.</summary>
    /// <param name="query">The date range to report on; both bounds are optional.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>One row per admin, with the count of each rating and the mean.</returns>
    public Task<TicketFeedbackScoresDto> HandleAsync(
        GetTicketFeedbackScoresQuery query, CancellationToken ct)
        => repo.GetTicketFeedbackScoresAsync(query.From, query.To, ct);
}
