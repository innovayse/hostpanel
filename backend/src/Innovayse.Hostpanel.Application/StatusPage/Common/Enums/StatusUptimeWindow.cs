namespace Innovayse.Hostpanel.Application.StatusPage.Common.Enums;

/// <summary>The spans the status page reports uptime over. The value of each member is its length in days.</summary>
public enum StatusUptimeWindow
{
    /// <summary>The last 30 days.</summary>
    Days30 = 30,

    /// <summary>The last 60 days.</summary>
    Days60 = 60,

    /// <summary>The last 90 days.</summary>
    Days90 = 90,
}
