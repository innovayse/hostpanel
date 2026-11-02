using Innovayse.SDK.Plugins.Endpoints;
using Innovayse.SDK.Plugins.Enums;
using Innovayse.SDK.Plugins.Manifest;

namespace Innovayse.Hostpanel.Application.Plugins.Common;

/// <summary>
/// A request that matched a declared route of an enabled plugin and has not been authorised yet. Holding one proves
/// the plugin exists and the route is declared, and still nothing of the plugin has run.
/// </summary>
/// <param name="Manifest">The plugin's manifest.</param>
/// <param name="Area">The panel the request came through.</param>
/// <param name="Match">The declared route and its parameter values.</param>
/// <param name="Build">Builds the plugin's endpoints; called once the caller is authorised.</param>
public sealed record PluginTarget(PluginManifest Manifest, PluginArea Area, PluginRouteMatch Match, Func<IPluginEndpoints> Build);
