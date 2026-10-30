using Innovayse.Hostpanel.Application.Billing.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Reports.Common;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Application.Reports.Queries.GetIncomeForecast;

/// <summary>Handles <see cref="GetIncomeForecastQuery"/>.</summary>
public sealed class GetIncomeForecastHandler
{
    /// <summary>Months in the trailing window the quarterly column sums over.</summary>
    private const int QuarterWindow = 3;

    /// <summary>Months in the trailing window the semi-annual column sums over.</summary>
    private const int SemiAnnualWindow = 6;

    /// <summary>Three-letter month names, indexed 0 for January.</summary>
    private static readonly string[] MonthNames =
        ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];

    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The source of the current moment.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="clock">The source of the current moment.</param>
    public GetIncomeForecastHandler(IHostpanelDbContext context, IClock clock)
    {
        _context = context;
        _clock = clock;
    }

    /// <summary>
    /// Projects the year's monthly income (payments less refunds, in the base currency) into the rolling windows the forecast
    /// screen shows.
    /// </summary>
    /// <param name="query">Year to forecast; may be omitted.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>One row per month of the year, in calendar order. Never refuses.</returns>
    public async Task<Result<IReadOnlyList<IncomeForecastRowDto>>> HandleAsync(
        GetIncomeForecastQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var year = query.Year ?? _clock.UtcNow.UtcDateTime.Year;

        var income = await _context.GetYearIncomeAsync(year, ct);
        var monthly = income.Months;

        // Each window is trailing and clamped at January, so the early months sum fewer than
        // their nominal length rather than wrapping into the previous year.
        var annual = monthly.Sum();
        IReadOnlyList<IncomeForecastRowDto> rows = [.. monthly.Select((amount, index) => new IncomeForecastRowDto(
            MonthNames[index],
            amount,
            monthly.Take(index + 1).TakeLast(QuarterWindow).Sum(),
            monthly.Take(index + 1).TakeLast(SemiAnnualWindow).Sum(),
            annual,
            monthly.Take(index + 1).Sum(),
            income.Currency))];

        return Result<IReadOnlyList<IncomeForecastRowDto>>.Success(rows);
    }
}
