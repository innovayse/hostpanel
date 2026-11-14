namespace Innovayse.Hostpanel.Application.Admin.Common.Enums;

/// <summary>The periods the admin dashboard charts can cover, each named for and valued by its number of days.</summary>
public enum DashboardPeriod
{
    /// <summary>The last 30 days, today included.</summary>
    Days30 = 30,

    /// <summary>The last 90 days, today included.</summary>
    Days90 = 90,
}
