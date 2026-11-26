using Innovayse.Hostpanel.Application.Plugins.Common;

namespace Innovayse.Hostpanel.Application.Plugins.Commands.CallAdminPlugin;

/// <summary>
/// Forwards an admin area request to the plugin that declares the route: matches it against the manifest and calls the
/// plugin. Who may call is decided before the bus by the admin policy.
/// </summary>
/// <param name="PluginId">The plugin id from the URL.</param>
/// <param name="Input">The request, as the controller read it.</param>
/// <param name="ServiceId">
/// The service the administrator's page shows, sent by the page as the <c>serviceId</c> query value. The host reads the
/// client that owns it from its own records and hands that client's reference to the plugin as the caller's client, the
/// way the client area does for its own client; <see langword="null"/> when the call is not made in the context of a
/// service, and then the plugin sees no client.
/// </param>
public sealed record CallAdminPluginCommand(string PluginId, PluginHttpInput Input, int? ServiceId = null);
