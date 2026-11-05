using Hostpanel.Sdk.Packaging.Icons;
using Hostpanel.Sdk.Plugins.Manifest;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>
/// Reads and checks the icon a plugin manifest names (<see cref="PluginManifest.Icon"/>), once, for both the startup
/// load and the install. The rules (a package-relative <c>.svg</c> or <c>.png</c> path, no link on the way, the size
/// cap, the PNG header, an SVG with no active content) are the plugin packaging package's
/// <see cref="PluginIconChecker"/>, the code a plugin author runs before publishing; this adapter only turns the
/// answer into the host's <see cref="PluginIconDto"/> and violation. The bytes are kept in memory so serving the
/// icon reads no file.
/// </summary>
/// <remarks>
/// Every refusal detail names the package-relative path only: an absolute host path or an exception message never
/// reaches a log line or an install response.
/// </remarks>
internal static class PluginIconReader
{
    /// <summary>Reads the icon the manifest names, if any, and checks it against the icon rules.</summary>
    /// <param name="pluginDirectory">The plugin folder: the installed folder at load, the unpacked staging folder at install.</param>
    /// <param name="manifest">The plugin's manifest.</param>
    /// <returns>The checked icon, nothing when the manifest names none, or the first rule the icon breaks.</returns>
    public static PluginIconRead Read(string pluginDirectory, PluginManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var check = PluginIconChecker.Check(pluginDirectory, manifest.Icon);
        if (check.Violation is not null)
        {
            return new PluginIconRead(null, PluginRuleMap.ToHost(check.Violation));
        }

        var icon = check.Icon is null ? null : new PluginIconDto(check.Icon.ContentType, check.Icon.Content, check.Icon.ETag);
        return new PluginIconRead(icon, null);
    }
}
