using Innovayse.Hostpanel.Application.Plugins.Common;

namespace Innovayse.Hostpanel.Application.Plugins.Commands.CallClientPlugin;

/// <summary>
/// Forwards a client area request to the plugin that declares the route: matches it against the manifest, checks the
/// caller's account and the route's permission, and calls the plugin.
/// </summary>
/// <param name="PluginId">The plugin id from the URL.</param>
/// <param name="Input">The request, as the controller read it.</param>
public sealed record CallClientPluginCommand(string PluginId, PluginHttpInput Input);
