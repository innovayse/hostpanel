using Innovayse.Hostpanel.Application.StatusPage.Common.Enums;

namespace Innovayse.Hostpanel.Application.StatusPage.Queries.GetStatusUptime;

/// <summary>Query to retrieve the uptime of every status page component over a window, with a daily series for the uptime bars.</summary>
/// <param name="Days">The window: 30, 60 or 90 days; 90 when omitted.</param>
public sealed record GetStatusUptimeQuery(StatusUptimeWindow Days = StatusUptimeWindow.Days90);
