namespace Innovayse.Application.Admin.Queries.GetClientGrowthReport;

using Innovayse.Application.Admin.DTOs;
using Innovayse.Domain.Clients.Interfaces;

/// <summary>
/// Handles <see cref="GetClientGrowthReportQuery"/> by grouping new clients by registration date.
/// </summary>
/// <param name="clientRepo">Client repository.</param>
public sealed class GetClientGrowthReportHandler(IClientRepository clientRepo)
{
    /// <summary>
    /// Returns daily new client counts for the requested date range.
    /// </summary>
    /// <param name="query">The client growth report query with start and end dates.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of daily client growth items ordered by date.</returns>
    public async Task<IReadOnlyList<ClientGrowthReportItemDto>> HandleAsync(
        GetClientGrowthReportQuery query, CancellationToken ct)
    {
        var clients = await clientRepo.GetCreatedBetweenAsync(query.StartDate, query.EndDate, ct);

        return clients
            .GroupBy(c => DateOnly.FromDateTime(c.CreatedAt.UtcDateTime))
            .Select(g => new ClientGrowthReportItemDto(g.Key, g.Count()))
            .OrderBy(r => r.Date)
            .ToList();
    }
}
