namespace Innovayse.Hostpanel.Application.Admin.Common;

/// <summary>One day of a money series on the dashboard.</summary>
/// <param name="Date">The calendar day in the host time zone.</param>
/// <param name="Amount">The amount of that day in the base currency, zero for a day with no movement.</param>
public sealed record DashboardDailyAmountDto(DateOnly Date, decimal Amount);
