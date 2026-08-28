namespace Innovayse.Application.Reports.Queries.GetIncomeForecast;

using Innovayse.Application.Reports.Interfaces;

/// <summary>Handles <see cref="GetIncomeForecastQuery"/>.</summary>
/// <param name="repo">Reporting data access.</param>
public sealed class GetIncomeForecastHandler(IReportRepository repo)
{
    /// <summary>Months in the trailing window the quarterly column sums over.</summary>
    private const int QuarterWindow = 3;

    /// <summary>Months in the trailing window the semi-annual column sums over.</summary>
    private const int SemiAnnualWindow = 6;

    /// <summary>
    /// Projects the year monthly income into the rolling windows the forecast screen shows.
    /// </summary>
    /// <param name="query">Year to forecast; may be omitted.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>One row per month of the year, in calendar order.</returns>
    public async Task<IReadOnlyList<IncomeForecastRowDto>> HandleAsync(
        GetIncomeForecastQuery query, CancellationToken ct)
    {
        var monthly = await repo.GetAnnualIncomeAsync(query.Year ?? DateTime.UtcNow.Year, ct);

        // Each window is trailing and clamped at January, so the early months sum fewer than
        // their nominal length rather than wrapping into the previous year.
        var annual = monthly.Sum(x => x.Amount);
        return [.. monthly.Select((row, index) => new IncomeForecastRowDto(
            row.Month,
            row.Amount,
            monthly.Skip(Math.Max(0, index - (QuarterWindow - 1))).Take(QuarterWindow).Sum(x => x.Amount),
            monthly.Skip(Math.Max(0, index - (SemiAnnualWindow - 1))).Take(SemiAnnualWindow).Sum(x => x.Amount),
            annual,
            monthly.Take(index + 1).Sum(x => x.Amount)))];
    }
}
