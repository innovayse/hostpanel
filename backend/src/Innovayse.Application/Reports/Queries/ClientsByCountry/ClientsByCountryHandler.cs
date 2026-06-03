namespace Innovayse.Application.Reports.Queries.ClientsByCountry;

using Innovayse.Application.Reports.DTOs;
using Innovayse.Application.Reports.Interfaces;

/// <summary>Handles <see cref="ClientsByCountryQuery"/>.</summary>
public sealed class ClientsByCountryHandler(IReportRepository repo)
{
    /// <summary>Returns client counts grouped by country.</summary>
    public Task<IReadOnlyList<ClientsByCountryDto>> HandleAsync(ClientsByCountryQuery query, CancellationToken ct)
        => repo.GetClientsByCountryAsync(ct);
}
