using Innovayse.SDK.Plugins.Enums;
using Innovayse.SDK.Plugins.Manifest;

namespace Innovayse.Hostpanel.Infrastructure.Plugins.Extensions;

/// <summary>
/// The one answer to "does the host serve this plugin's routes, pages, menus and slots", asked by the manifest
/// validator, the loader, the endpoint resolver and the UI source alike, so the four can never disagree (#818).
/// </summary>
public static class PluginManifestCapabilityExtensions
{
    /// <summary>
    /// Whether the plugin answers HTTP-shaped calls and shows UI: an <see cref="PluginType.App"/> plugin always does, any
    /// other type only when it declares <see cref="PluginCapabilities.Endpoints"/>.
    /// </summary>
    /// <param name="manifest">The plugin's manifest, already validated.</param>
    /// <returns><see langword="true"/> when the host serves the plugin's <c>api</c> and <c>ui</c> blocks.</returns>
    public static bool ServesEndpoints(this PluginManifest manifest)
    {
        return manifest.Type == PluginType.App || manifest.Declares(PluginCapabilities.Endpoints);
    }

    /// <summary>Whether the manifest lists a capability, compared exactly as the SDK spells it.</summary>
    /// <param name="manifest">The plugin's manifest.</param>
    /// <param name="capability">A name from <see cref="PluginCapabilities"/>.</param>
    /// <returns><see langword="true"/> when <c>capabilities</c> holds that exact name.</returns>
    public static bool Declares(this PluginManifest manifest, string capability)
    {
        return manifest.Capabilities.Contains(capability, StringComparer.Ordinal);
    }
}
