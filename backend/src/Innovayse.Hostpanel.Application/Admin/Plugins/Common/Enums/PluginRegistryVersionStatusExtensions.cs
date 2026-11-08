namespace Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;

/// <summary>Orders registry version statuses by how strongly they stand against an install.</summary>
public static class PluginRegistryVersionStatusExtensions
{
    /// <summary>
    /// How strict a status is; a higher number refuses more. Yanked is the strictest, then Rejected, InReview, Deprecated
    /// and Published. Used when a registry lists the same version twice with different statuses.
    /// </summary>
    /// <param name="status">The status.</param>
    /// <returns>The strictness rank.</returns>
    public static int Strictness(this PluginRegistryVersionStatus status)
    {
        return status switch
        {
            PluginRegistryVersionStatus.Yanked => 4,
            PluginRegistryVersionStatus.Rejected => 3,
            PluginRegistryVersionStatus.InReview => 2,
            PluginRegistryVersionStatus.Deprecated => 1,
            _ => 0,
        };
    }
}
