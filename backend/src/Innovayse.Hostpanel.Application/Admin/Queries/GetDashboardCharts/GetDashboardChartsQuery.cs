using Innovayse.Hostpanel.Application.Admin.Common.Enums;

namespace Innovayse.Hostpanel.Application.Admin.Queries.GetDashboardCharts;

/// <summary>Query that returns the series the admin dashboard charts draw for a period.</summary>
/// <param name="Period">How far back the series reach, today included; 30 days when omitted.</param>
public sealed record GetDashboardChartsQuery(DashboardPeriod Period = DashboardPeriod.Days30);
