namespace Innovayse.Hostpanel.Application.Admin.Plugins.Common;

/// <summary>Builds the address of the admin icon endpoint, once, for every DTO that carries an <c>iconUrl</c>.</summary>
public static class PluginIconPaths
{
    /// <summary>Gets the address of the icon of one plugin.</summary>
    /// <param name="pluginId">The plugin id, already a valid plugin identifier (lowercase letters, digits and hyphens).</param>
    /// <returns>The path, for example <c>/api/admin/plugins/my-plugin/icon</c>.</returns>
    public static string For(string pluginId)
    {
        return $"/api/admin/plugins/{pluginId}/icon";
    }
}
