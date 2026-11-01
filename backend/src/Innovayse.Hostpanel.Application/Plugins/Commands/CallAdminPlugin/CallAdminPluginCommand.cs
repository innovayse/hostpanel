using Innovayse.Hostpanel.Application.Plugins.Common;

namespace Innovayse.Hostpanel.Application.Plugins.Commands.CallAdminPlugin;

/// <summary>
/// Forwards an admin area request to the plugin that declares the route: matches it against the manifest and calls the
/// plugin. Who may call is decided before the bus by the admin policy.
/// </summary>
/// <param name="PluginId">The plugin id from the URL.</param>
/// <param name="Input">The request, as the controller read it.</param>
public sealed record CallAdminPluginCommand(string PluginId, PluginHttpInput Input);
