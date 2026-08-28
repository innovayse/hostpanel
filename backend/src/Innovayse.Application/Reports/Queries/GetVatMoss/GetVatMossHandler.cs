namespace Innovayse.Application.Reports.Queries.GetVatMoss;

using Innovayse.Application.Reports.DTOs;
using Innovayse.Application.Reports.Interfaces;

/// <summary>Handles <see cref="GetVatMossQuery"/>.</summary>
/// <param name="repo">Reporting data access.</param>
public sealed class GetVatMossHandler(IReportRepository repo)
{
    /// <summary>Calendar months in a quarter, used to derive the quarter today falls in.</summary>
    private const int MonthsPerQuarter = 3;

    /// <summary>Returns VAT settlement figures for the requested quarter.</summary>
    /// <param name="query">Year and quarter to report on; either may be omitted.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>VAT collected per member state for the quarter.</returns>
    public Task<VatMossDto> HandleAsync(GetVatMossQuery query, CancellationToken ct)
    {
        // The period defaults here rather than at the endpoint so every caller of the query,
        // HTTP or not, lands on the same quarter.
        var now = DateTime.UtcNow;
        var year = query.Year ?? now.Year;
        var quarter = query.Quarter ?? (((now.Month - 1) / MonthsPerQuarter) + 1);
        return repo.GetVatMossAsync(year, quarter, ct);
    }
}
