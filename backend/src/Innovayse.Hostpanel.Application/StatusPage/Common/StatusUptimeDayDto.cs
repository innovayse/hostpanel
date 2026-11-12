namespace Innovayse.Hostpanel.Application.StatusPage.Common;

/// <summary>One bar of a component's uptime history: a UTC day and how much of it the component answered.</summary>
/// <param name="Date">The UTC day.</param>
/// <param name="UptimePercent">The share of that day's checks that succeeded, 0 to 100, or <c>null</c> when no check ran that day.</param>
public sealed record StatusUptimeDayDto(DateOnly Date, double? UptimePercent);
