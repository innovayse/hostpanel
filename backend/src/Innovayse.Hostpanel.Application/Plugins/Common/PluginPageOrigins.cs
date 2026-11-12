using Hostpanel.Sdk.Plugins.Manifest;

namespace Innovayse.Hostpanel.Application.Plugins.Common;

/// <summary>
/// Works out the origins an HTML page that a plugin's callback answers may load from and talk to: exactly the pure
/// <c>https</c> origins the plugin's manifest declares in <c>externalOrigins</c>, so a checkout page can load its gateway's
/// script and nothing else. A wildcard, an <c>http</c> origin or anything that is not a bare origin names no source.
/// </summary>
public static class PluginPageOrigins
{
    /// <summary>Lists the origins of a plugin's page.</summary>
    /// <param name="manifest">The plugin's manifest.</param>
    /// <returns>The distinct canonical origins within <see cref="PluginFrameOriginResolver.MaxJoinedLength"/>; empty when none is declared.</returns>
    public static IReadOnlyList<string> From(PluginManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        return PluginFrameOriginResolver.Bound(
            manifest.ExternalOrigins.Select(PluginFrameOrigin.FromHttps).OfType<string>());
    }
}
