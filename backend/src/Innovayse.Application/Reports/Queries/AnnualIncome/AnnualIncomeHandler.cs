namespace Innovayse.Application.Reports.Queries.AnnualIncome;

using Innovayse.Application.Reports.DTOs;
using Innovayse.Application.Reports.Interfaces;

/// <summary>Handles <see cref="AnnualIncomeQuery"/>.</summary>
public sealed class AnnualIncomeHandler(IReportRepository repo)
{
    /// <summary>Returns monthly income totals for the given year.</summary>
    public Task<IReadOnlyList<AnnualIncomeDto>> HandleAsync(AnnualIncomeQuery query, CancellationToken ct)
        => repo.GetAnnualIncomeAsync(query.Year, ct);
}
