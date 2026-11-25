namespace Innovayse.Hostpanel.Application.Admin.Plugins.Common;

/// <summary>Builds the address of the admin icon endpoint, once, for every DTO that carries an <c>iconUrl</c>.</summary>
public static class PluginIconPaths
{
    /// <summary>The query parameter that carries the plugin version, so a new version is a new address.</summary>
    public const string VersionParameter = "v";

    /// <summary>Gets the address of the icon of one plugin.</summary>
    /// <param name="pluginId">The plugin id, already a valid plugin identifier (lowercase letters, digits and hyphens).</param>
    /// <param name="version">
    /// The plugin's version. The icon endpoint is cached by address, so the version is part of the address: after an
    /// update the browser asks for a new address instead of showing the old icon until its cache entry expires.
    /// </param>
    /// <returns>The path, for example <c>/api/admin/plugins/my-plugin/icon?v=1.2.0</c>.</returns>
    public static string For(string pluginId, string version)
    {
        return $"/api/admin/plugins/{pluginId}/icon?{VersionParameter}={Uri.EscapeDataString(version)}";
    }
}
