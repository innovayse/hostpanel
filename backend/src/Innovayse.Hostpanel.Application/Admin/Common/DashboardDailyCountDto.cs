namespace Innovayse.Hostpanel.Application.Admin.Common;

/// <summary>One day of a count series on the dashboard.</summary>
/// <param name="Date">The calendar day in the host time zone.</param>
/// <param name="Count">How many happened that day, zero for a quiet day.</param>
public sealed record DashboardDailyCountDto(DateOnly Date, int Count);
