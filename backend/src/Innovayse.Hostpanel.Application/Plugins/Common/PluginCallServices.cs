using Hostpanel.Sdk.Plugins.Host;
using Hostpanel.Sdk.Plugins.Store;

namespace Innovayse.Hostpanel.Application.Plugins.Common;

/// <summary>The services bound to one plugin call, as the plugin receives them on its call.</summary>
/// <param name="Host">The read-only host facts about the call's client, filtered by the caller's permissions.</param>
/// <param name="Store">The plugin's own store, scoped to the plugin and the call's client.</param>
/// <param name="Quota">
/// Raised by the store when it refused a write over the scope's quota. Host state, never the plugin's: the runner reads it
/// when the plugin throws, so only a refusal the host itself made is answered <c>PLUGIN_STORE_QUOTA_EXCEEDED</c>.
/// </param>
public sealed record PluginCallServices(IPluginHostApi Host, IPluginStore Store, PluginStoreQuotaSignal Quota);
