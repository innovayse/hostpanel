namespace Innovayse.Application.Reports.Queries.GetClientPicker;

using Innovayse.Application.Reports.DTOs;
using Innovayse.Application.Reports.Interfaces;

/// <summary>Handles <see cref="GetClientPickerQuery"/>.</summary>
/// <param name="repo">Reporting data access.</param>
public sealed class GetClientPickerHandler(IReportRepository repo)
{
    /// <summary>Returns every client as an id and name pair.</summary>
    /// <param name="query">The query. Carries no filters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Clients ordered as the repository returns them, for use in a picker.</returns>
    public Task<IReadOnlyList<ClientPickerDto>> HandleAsync(GetClientPickerQuery query, CancellationToken ct)
        => repo.GetClientPickerListAsync(ct);
}
