namespace Innovayse.Hostpanel.Infrastructure.Plugins.Registry;

/// <summary>The JSON body sent to the registry to look up the newest version of several plugins.</summary>
/// <param name="Ids">The plugin ids.</param>
internal sealed record PluginRegistryLatestWire(IReadOnlyCollection<string> Ids);
