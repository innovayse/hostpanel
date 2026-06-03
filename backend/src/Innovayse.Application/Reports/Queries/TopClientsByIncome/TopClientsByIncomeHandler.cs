namespace Innovayse.Application.Reports.Queries.TopClientsByIncome;

using Innovayse.Application.Reports.DTOs;
using Innovayse.Application.Reports.Interfaces;

/// <summary>Handles <see cref="TopClientsByIncomeQuery"/>.</summary>
public sealed class TopClientsByIncomeHandler(IReportRepository repo)
{
    /// <summary>Returns top clients ranked by transaction income.</summary>
    public Task<IReadOnlyList<TopClientByIncomeDto>> HandleAsync(TopClientsByIncomeQuery query, CancellationToken ct)
        => repo.GetTopClientsByIncomeAsync(query.Take, ct);
}
