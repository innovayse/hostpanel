using Innovayse.Hostpanel.Application.Plugins.Common;

namespace Innovayse.Hostpanel.Application.Plugins.Commands.CallPluginCallback;

/// <summary>
/// Forwards a public callback, such as a payment gateway's webhook, to the plugin that declares the route in
/// <c>api.callbacks</c>. There is no signed-in caller: the plugin checks the call itself (a signature, a checksum or a shared
/// secret) from the raw body and headers the host passes on unchanged.
/// </summary>
/// <param name="PluginId">The plugin id from the URL.</param>
/// <param name="Input">The request, as the controller read it, headers included.</param>
public sealed record CallPluginCallbackCommand(string PluginId, PluginHttpInput Input);
