using Innovayse.SDK.Plugins.Host;
using Innovayse.SDK.Plugins.Store;

namespace Innovayse.Hostpanel.Application.Plugins.Common;

/// <summary>The services bound to one plugin call, as the plugin receives them on its call.</summary>
/// <param name="Host">The read-only host facts about the call's client.</param>
/// <param name="Store">The plugin's own store, scoped to the plugin and the call's client.</param>
public sealed record PluginCallServices(IPluginHostApi Host, IPluginStore Store);
