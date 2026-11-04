namespace Innovayse.Hostpanel.Application.Admin.Integrations.Common;

/// <summary>
/// The cache key of a plugin's last status check, in one place: <see cref="PluginConnectionReader"/> writes it and a
/// saved integration removes it, so a changed switch or credential is checked again on the next read.
/// </summary>
public static class PluginStatusCacheKeys
{
    /// <summary>Prefix of every key; the plugin id follows.</summary>
    private const string Prefix = "integrations:plugin-status:";

    /// <summary>The key of one plugin's check.</summary>
    /// <param name="pluginId">The plugin id from its manifest.</param>
    /// <returns>The cache key.</returns>
    public static string For(string pluginId)
    {
        return Prefix + pluginId;
    }
}
