namespace Innovayse.Hostpanel.Application.Admin.Common;

/// <summary>Income of the dashboard period: one amount per day, the total and how it compares with the period before.</summary>
/// <param name="Days">One amount per day of the period, oldest first, every day present.</param>
/// <param name="Total">Income of the whole period in the base currency.</param>
/// <param name="PreviousTotal">Income of the period of the same length right before it.</param>
/// <param name="ChangePercent">Change of <paramref name="Total"/> against <paramref name="PreviousTotal"/> in percent; null when the previous period earned nothing.</param>
/// <param name="DailyAverage">The total spread over the days of the period, the target line of the chart.</param>
public sealed record DashboardIncomeDto(
    IReadOnlyList<DashboardDailyAmountDto> Days,
    decimal Total,
    decimal PreviousTotal,
    decimal? ChangePercent,
    decimal DailyAverage);
