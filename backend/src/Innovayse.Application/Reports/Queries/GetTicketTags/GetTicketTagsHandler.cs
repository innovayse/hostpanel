namespace Innovayse.Application.Reports.Queries.GetTicketTags;

using Innovayse.Application.Reports.Common;
using Innovayse.Application.Reports.Interfaces;

/// <summary>Handles <see cref="GetTicketTagsQuery"/>.</summary>
/// <param name="repo">Reporting data access.</param>
public sealed class GetTicketTagsHandler(IReportRepository repo)
{
    /// <summary>Returns every tag used in the period with how often it was applied.</summary>
    /// <param name="query">The date range to report on; both bounds are optional.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>One row per distinct tag, with its usage count.</returns>
    public Task<TicketTagsDto> HandleAsync(GetTicketTagsQuery query, CancellationToken ct)
        => repo.GetTicketTagsAsync(query.From, query.To, ct);
}
