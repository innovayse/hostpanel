namespace Innovayse.Application.Reports.Queries.ClientsByCity;

using Innovayse.Application.Reports.DTOs;
using Innovayse.Application.Reports.Interfaces;

/// <summary>Handles <see cref="ClientsByCityQuery"/>.</summary>
public sealed class ClientsByCityHandler(IReportRepository repo)
{
    /// <summary>Returns client counts grouped by city.</summary>
    public Task<IReadOnlyList<ClientsByCityDto>> HandleAsync(ClientsByCityQuery query, CancellationToken ct)
        => repo.GetClientsByCityAsync(ct);
}
