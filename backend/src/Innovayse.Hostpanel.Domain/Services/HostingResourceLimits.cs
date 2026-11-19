namespace Innovayse.Hostpanel.Domain.Services;

/// <summary>How many hosting resources one service may hold at once; removed ones do not count.</summary>
public static class HostingResourceLimits
{
    /// <summary>The most websites one service may hold.</summary>
    public const int MaxWebsitesPerService = 25;

    /// <summary>The most databases one service may hold.</summary>
    public const int MaxDatabasesPerService = 25;
}
