namespace Innovayse.Hostpanel.Application.StatusPage.Common;

/// <summary>The uptime of one component over a window: the overall figure and one entry per day.</summary>
/// <param name="Id">The component's primary key.</param>
/// <param name="Name">The display name.</param>
/// <param name="UptimePercent">The share of the window's checks that succeeded, 0 to 100, or <c>null</c> when no check ran in the window.</param>
/// <param name="Daily">One entry per UTC day of the window, oldest first, ending with today.</param>
public sealed record StatusComponentUptimeDto(int Id, string Name, double? UptimePercent, IReadOnlyList<StatusUptimeDayDto> Daily);
