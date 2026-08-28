namespace Innovayse.Application.Reports.Queries.GetTicketFeedbackComments;

using Innovayse.Application.Reports.DTOs;
using Innovayse.Application.Reports.Interfaces;

/// <summary>Handles <see cref="GetTicketFeedbackCommentsQuery"/>.</summary>
/// <param name="repo">Reporting data access.</param>
public sealed class GetTicketFeedbackCommentsHandler(IReportRepository repo)
{
    /// <summary>Returns the feedback clients left on tickets, narrowed by the filters.</summary>
    /// <param name="query">Admin and date range to narrow by.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>One row per piece of feedback.</returns>
    public Task<TicketFeedbackCommentsDto> HandleAsync(
        GetTicketFeedbackCommentsQuery query, CancellationToken ct)
        => repo.GetTicketFeedbackCommentsAsync(query.StaffName, query.From, query.To, ct);
}
