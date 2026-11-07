using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Manifest;

namespace Innovayse.Hostpanel.Application.Plugins.Common;

/// <summary>
/// The one place the host reads what the plugin SDK says about public callbacks: which routes a manifest declares for
/// them and which <see cref="PluginArea"/> the plugin is told the call came through. Everything else about callbacks
/// (the gate, the use case, the door, the limits) is written against this class alone.
/// </summary>
public static class PluginCallbackContract
{
    /// <summary>The caller reference the plugin sees on a callback: a callback has no user, so it is a fixed marker, not a hash of anyone.</summary>
    public const string CallerRef = "callback";

    /// <summary>The area the plugin is told a callback came through.</summary>
    public const PluginArea Area = PluginArea.Callback;

    /// <summary>The callback routes a manifest declares.</summary>
    /// <param name="manifest">The plugin's manifest.</param>
    /// <returns>The routes of <c>api.callbacks</c>.</returns>
    public static IReadOnlyList<PluginRoute> RoutesOf(PluginManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        return manifest.Api.Callbacks;
    }
}
