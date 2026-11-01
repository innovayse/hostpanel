using Innovayse.SDK.Plugins.Fulfilment;

namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>An enabled fulfilment plugin, activated with its own settings, and the id its deliveries are recorded under.</summary>
/// <param name="PluginId">The plugin id from its manifest; what a fulfilment record names as the plugin to tell.</param>
/// <param name="Plugin">The activated plugin, ready to be called.</param>
public sealed record ActiveFulfilmentPlugin(string PluginId, IFulfilmentPlugin Plugin);
